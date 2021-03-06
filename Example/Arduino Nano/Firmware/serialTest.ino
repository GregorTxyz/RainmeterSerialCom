//Global variables
String inputString = "";
bool ledOn = false;

void setup() {
  // put your setup code here, to run once:
  //Serial config
  Serial.begin(9600);           
  Serial.println("It's runing");
  //L Led
  pinMode(13, OUTPUT);
}

void loop() {
  // put your main code here, to run repeatedly:
  
   
  //Serial.println("Hello world!");  // prints hello with ending line break
  //Serial read:
  inputString = serialRead();
  if(inputString == "DHT11\n"){
    inputString = readDHT(3,11);
    Serial.println(inputString);
    inputString = "";
  }else if(inputString == "DHT22\n"){
    inputString = readDHT(2,22);
    Serial.println(inputString);
    inputString = "";
  }else if(inputString == "LED1\n"){
     if(ledOn == false){
        ledOn = true;
        digitalWrite(13, HIGH);
        Serial.println("LED ON");
     }else if(ledOn == true){
        ledOn = false;
         digitalWrite(13, LOW);
         Serial.println("LED OFF");
     }
    
  }else if(inputString != ""){
    Serial.println("Unknown string:" + String(inputString));
  }

  delay(200);
}



///////////////////////////////////////////////////////////////////
// FUNCTIONS //
// Read serial data
String serialRead(){
    String data = "";
    while(Serial.available()){
      char inChar = (char)Serial.read();
      data += inChar;
      if (inChar == '\n') {
        break;
      }
    }
    return data;
}

//Function to read DHT11 sensor
String readDHT(int pin, int type){
    String data = "";
    uint8_t bits[5];
    uint8_t cnt = 7;
    uint8_t idx = 0;
    uint8_t error = 0;
    double humidity;
    double temperature;
      
    // EMPTY BUFFER
    for (int i=0; i< 5; i++) bits[i] = 0;
    // REQUEST SAMPLE
    pinMode(pin, OUTPUT);
    digitalWrite(pin, LOW);
    delay(18);
    digitalWrite(pin, HIGH);
    delayMicroseconds(40);
    pinMode(pin, INPUT);
  
    // ACKNOWLEDGE or TIMEOUT
    unsigned int loopCnt = 10000;
    while(digitalRead(pin) == LOW){
      if(loopCnt-- == 0) error = 1;
    }
    loopCnt = 10000;
    while(digitalRead(pin) == HIGH){
      if (loopCnt-- == 0){
        error = 2;
        break;  
      }
    }
  
    // READ OUTPUT - 40 BITS => 5 BYTES or TIMEOUT
    for (int i=0; i<40; i++){
      loopCnt = 10000;
      while(digitalRead(pin) == LOW){
        if(loopCnt-- == 0){
          error = 3;
           break;  
        }
      }
      //error handler
      if(error > 1) break;
      
      unsigned long t = micros();
      loopCnt = 10000;
      while(digitalRead(pin) == HIGH){
        if (loopCnt-- == 0){
          error = 4;
          break;  
        }
      }
      if ((micros() - t) > 40) bits[idx] |= (1 << cnt);
      if (cnt == 0){
        cnt = 7;    // restart at MSB
        idx++;      // next byte!
      }
      else cnt--;
    }
    //check data
    uint8_t sum = bits[0] + bits[1] + bits[2] + bits[3];  
    if ((bits[4] != sum) && (error < 1)) error = 5;
    //Check if error
    if(error > 0){
       data = "E:"+String(error);
    }else{
      if(type == 11){
          data = "d11Temp:" + String(bits[2]) + '\n' + "d11Humi:" + String(bits[0]);  //DHT11
       }
         
      if(type == 22){
        humidity = word(bits[0], bits[1]) * 0.1;
        temperature = word(bits[2] & 0x7F, bits[3]) * 0.1;
        if (bits[2] & 0x80)  // negative temperature
        {
            temperature = -temperature;
        }
        data = "d22Humi:" + String(humidity) + '\n' + "d22Temp:" + String(temperature);
      }
    }
    
    //return data
    return data;
}

