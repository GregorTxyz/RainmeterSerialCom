/*
  Copyright (C) 2015 Gregor T.

  This program is free software; you can redistribute it and/or
  modify it under the terms of the GNU General Public License
  as published by the Free Software Foundation; either version 2
  of the License, or (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rainmeter;
using System.IO.Ports;

namespace SerialCommunication
{
    internal class Measure
    {
        //Private Class Variables
        private static bool error = false;
        private static bool connOpen = false;
        private static SerialPort _serialPort;
        private static string readSerialData = "";

        //Serial comm variables
        private string localScommand = "";                               //Variable to remember which data send on refresh, if this action is choosen
        private static List<string> receiveData = new List<string>();    //Data received form serial communication [buffer]
        private static List<string> receiveSearch = new List<string>();  //String for that we search in receiving data
        private static List<string> measureName = new List<string>();    //Name of measure that we refresh
        private static List<IntPtr> skinPointer = new List<IntPtr>();    //Pointer to the skin instance [needed for measure referesh]
        private int listLocation = -1;

        //Multiple skins fix
        private string skinName = "";                                    //Skin name of this instance
        private static List<string> allSkinNames = new List<string>();   //All skin names that use this plugin

        //Functions START
        internal Measure(){
        }
        internal void Reload(Rainmeter.API rm, ref double maxValue)  //Execute on skin meter create or refresh
        {
            //Multiple skins code
            skinName = rm.GetSkinName();
            int localPosition = allSkinNames.IndexOf(skinName);
            if (localPosition < 0)
            { 
               //If not exist, add to list
                allSkinNames.Add(skinName);
            }
   
            //Skin data processing
            string actionData = rm.ReadString("Action", "");
            actionData = actionData.ToLowerInvariant();
            if (actionData == "init")  //If action is "Init" (Initialization)
            {
                string portName = rm.ReadString("Port", "COM1");          //Get Com port name, default is COM1
                string baud = rm.ReadString("Baud", "9600");              //Get Baudrate, default is 9600
                int baudrate = Int32.Parse(baud);                  //Convert baudrate to intiger
                openConn(portName, baudrate);                      //Execute function to try open serial connection
            }

            if (actionData == "receivedata")  //If action is "Data"
            {

                //String that we search in receiving data
                receiveSearch.Add(rm.ReadString("ReceiveDataFind", ""));
                //Initialize buffer for receive data for this measure
                receiveData.Add("");
                
                //Pointer to the skin instance [needed for refresh of measure]
                skinPointer.Add(rm.GetSkin());

                //Add measure name to list, to know which measure refresh
                measureName.Add(rm.GetMeasureName());
                //Location in lists for this instance
                listLocation = (receiveData.Count) - 1;
            }
            if (actionData == "senddataonrefersh")  //If action is to send data on skin refresh
            {
                localScommand = rm.ReadString("SendData", "");
            }
        }

        internal string GetString()  //Execute on skin update (if meter is String)
        {
            string tempData = "";   //Initialize temp variable
            //Try set update data
            if (listLocation >= 0)
            {                //Check if array position is higher than -1 (Init function has array location -1)             
                if (receiveData[listLocation] != "")   //If ther is data to sen to this meter ()
                {
                    tempData = receiveData[listLocation];  //Transfer data to empty variable
                    return tempData;                    //Send data to meter
                }
                else {
                    return null;    //Send empty data, nothing to send
                }
            }
            return null;  //return default
        }
        internal double Update()  //Execute on skin update - Return double
        {
            //Send data, if not empty
            if (localScommand != "")
            {
                sendSerialCom(localScommand);
            }
            return 0.0;  //Return default
        }
        internal void Cleanup()  //Execute after skin close
        {
            //Multiple skins fix
            int size = allSkinNames.Count;
            int index = allSkinNames.IndexOf(skinName);

            if(size > 0){                           //If elements are in list
                allSkinNames.Remove(skinName);      //Erase this element, if exist in the list 
            }else{                                  //If list is empty, this mean no skin useses this plugin, so close connection and reset all static data
                closeConn();                        //Close connection
                //Data cleanup
                error = false;
                connOpen = false;
                readSerialData = "";
                receiveData.Clear();
                receiveSearch.Clear();
                skinPointer.Clear();
                measureName.Clear();
            }
        }
        internal void ExecuteBang(string args)  //Execute on skin click
        {
            //Send serial data
            sendSerialCom(args);

        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //Function to open serial connection
        public static void openConn(string portName, int baudrate){
            if ((error == false) && (connOpen == false))
            {  //If no error and connection is not open
                //Get availible COM ports
                string[] ports = SerialPort.GetPortNames();
                if (ports.Length <= 0)
                {
                    error = true;
                    API.Log(API.LogType.Error, "No serial ports found.");
                }
                //No error, get choosen COM port
                if (error == false)
                {
                    if (portName == "" || portName == null)
                    {
                        error = true;
                        API.Log(API.LogType.Error, "Chosen port is not valid.");
                    }
                }
                //Check choosen port
                if (error == false)
                {
                    if (Array.Exists(ports, element => element == portName))
                    {
                        //Try open connection
                        _serialPort = new SerialPort(portName);
                        _serialPort.BaudRate = (baudrate);
                        try
                        {
                            _serialPort.Open();
                            connOpen = true;
                        }
                        catch (Exception ex)
                        {
                            error = true;
                            API.Log(API.LogType.Error, "Error opening serial connection: " + ex.Message + ".");
                        }
                    }
                    else
                    {
                        error = true;
                        API.Log(API.LogType.Error, "Port " + portName + " do not exist.");
                    }
                }
                //Errase I/O buffer
                if (error == false) { 
                    try
                    {
                        _serialPort.DiscardOutBuffer();
                        _serialPort.DiscardInBuffer();
                    }
                    catch (Exception ex)
                    {
                        error = true;
                        API.Log(API.LogType.Error, "Empty I/O buffer: " + ex.Message + ".");
                    }
                }
                //Handler for receiving data
                if (error == false)
                {
                    try
                    {
                        _serialPort.DataReceived += new SerialDataReceivedEventHandler(receiveSerialCom);
                        API.Log(API.LogType.Notice, "Serial connection open (" + portName + ").");
                    }
                    catch (Exception ex)
                    {
                        error = true;
                        API.Log(API.LogType.Error, "Error to create receive handler.");
                    }
                }
            }
            else {
                API.Log(API.LogType.Notice, "Error open serial connection (Maybe is already open)");
            }
        }      
        //Function to send data
        public static void sendSerialCom(string data) {
            if ((connOpen == true) && (data != ""))
            {
                data = data + "\n";  //Add caracter for new line
                try
                {
                    _serialPort.Write(data);
                }
                catch (Exception ex)
                {
                    API.Log(API.LogType.Error, "Send data error: " + ex.Message);
                }
            }
            else
            {
                API.Log(API.LogType.Debug, "Error send data, serial connection not open.");
            }

        }
        //Function to receive data
        public static void receiveSerialCom(object data, SerialDataReceivedEventArgs e)
        {
            string[] dataIn = new string[0]; 

            readSerialData = (data as SerialPort).ReadExisting();
            dataIn = readSerialData.Split('\n');
            for (int y = 0; y < receiveSearch.Count; y++)
            {
                for (int x = 0; x < dataIn.Length; x++) {
                    if (dataIn[x].Contains(receiveSearch[y]))
                    { 
                        //If exist, write to variable
                        receiveData[y] = dataIn[x];
                        //Refresh measure
                        API.Execute(skinPointer[y], "!UpdateMeasure " + '"' + measureName[y] + '"');
                    }
                }
            }
            readSerialData = "";
            dataIn = null;
            return;
        }
        //Function to close serial connection
        public static void closeConn() {
            if (connOpen == true){
                try{
                    _serialPort.Close();
                    connOpen = false;
                    API.Log(API.LogType.Notice, "Serial connection closed.");
                }catch (Exception ex){
                    API.Log(API.LogType.Error, "Error close serial connection: " + ex.Message);
                }
            }
        }
    }

    /// <summary>
    /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>
    public static class Plugin
    {
        static IntPtr StringBuffer = IntPtr.Zero;

        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure()));
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            //Call finalze function
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Cleanup();

            GCHandle.FromIntPtr(data).Free();
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }
        }

        [DllExport]
        public static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Reload(new Rainmeter.API(rm), ref maxValue);
        }

        [DllExport]
        public static double Update(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            return measure.Update();
        }

        [DllExport]
        public static IntPtr GetString(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }

            string stringValue = measure.GetString();
            if (stringValue != null)
            {
                StringBuffer = Marshal.StringToHGlobalUni(stringValue);
            }

            return StringBuffer;
        }

        [DllExport]
        public static void ExecuteBang(IntPtr data, IntPtr args)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.ExecuteBang(Marshal.PtrToStringUni(args));
        }
    }
}
