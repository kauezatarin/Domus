#include <SPI.h>
#include <Ethernet.h>
#include <DHT.h>
#include <DHT_U.h>
#include <EEPROM.h>

#define DHTPIN A5 // pino que estamos conectado
#define DHTTYPE DHT11 // DHT 11

#define DEVICE_NAME "Casa" //nome do dispositivo
#define DEVICE_TIPE "weather" //tipo de dispositivo
#define DATA_DELAY 30 //preserva o deay original para restauração futura

char DEVICE_UNIQUE_ID[33] = "698dc19d489c4e4db73e28a713eab07b"; //id unico do device vinculado a sua conta

byte mac[] = {
  0xDE, 0xAD, 0xBE, 0xEF, 0xFE, 0xED
};
IPAddress ip(192, 168, 1, 50);
IPAddress servidor(192, 168, 1, 41);

int connectionPort = 9595;

EthernetClient client;

unsigned long data_delay = DATA_DELAY;//delay para envio de dados em segundos

int reconnectDelay = 10 * 1000;//intervalo entre tentativas de conexão
unsigned long messageDelay = data_delay * 1000;//intervalo entre envio de dados
unsigned long lastTime = 0;

String inData = String(100);
String outData = String(100);
char c;

//sensor var
DHT dht(DHTPIN, DHTTYPE);
float temperatura;
float humidade;

void setup() {
  // initialize the Ethernet device
  Serial.begin(9600);
  //Ethernet.begin(mac, ip, myDns, gateway, subnet);
  Ethernet.begin(mac); //get ip by DHCP

  dht.begin();
}

void loop() {
  // when the client sends the first byte, say hello:
  if (client) {
    
    if (client.connected() == false)
    {
      Serial.println("Conexão Perdida!");
      data_delay = DATA_DELAY;
      client.stop();
    }
    else if(client.available() > 0)
    {
      inData = "";//limpa a variavel de dados recebidos
      
      while(client.available() > 0)//recebe os dados disponiveis no buffer
      {
        c = client.read();
        inData+=c;
      }

      Serial.println(inData);
      executeCommand(inData);
    }
    else
    {
      if((millis() - lastTime) >= messageDelay)
      {
        lastTime = millis();
        outData = "";
        temperatura = dht.readTemperature();
        humidade = dht.readHumidity();

        outData += temperatura;//temperatura
        outData += ";";
        outData += humidade;//humidade do ar
        outData += ";";
        outData += dht.computeHeatIndex(temperatura, humidade, false);//sensação termica
        
        client.print(outData);
        Serial.print("Dados enviados: ");
        Serial.println(outData);
      }
    }   
  }
  else
  {
    while(!client)
    {      
      tryConnection();
      
      if (!client)
        delay(reconnectDelay);
    }    
  }
}

bool tryConnection()
{
  Serial.println("connecting...");
  
  if (client.connect(servidor, connectionPort)) {
    Serial.println("connected");
    client.print("shakeback");
    delay(100);
    return true;
  }
  else {
    Serial.println("connection failed");
    return false;
  }
}

void executeCommand(String command)
{
  if(command == "ayt")//responde ao servidor para informar que a conexão não caiu
  {
    client.print("imhr");
  }
  else if(command == "SendInfos")//envia as informações do dispositivo para o servidor
  {
    outData = "";
    outData += "infos;";
    outData += DEVICE_NAME;
    outData += ";";
    outData += DEVICE_TIPE;
    outData += ";";
    outData += data_delay;
    outData += ";";
    outData += DEVICE_UNIQUE_ID;

    client.print(outData);
    Serial.print("Identificação realizada: ");
    Serial.println(outData);
  }
  else if(command == "uidit")//á existe um dispositivo com esse UID conectado
  {
    Serial.println("Uid já esta sendo usado. Tente novamente em 60 segundos. Isto pode ser causado por queda de conexão.");
    client.stop();

    delay(60000);
  }
  else if(command == "shutdown")//recebido o comando de 'Server Shutdown'
  {
    Serial.println("Recebido comando de desligamento de servicor. Conexão encerrada. Hibernando por 240 segundos...");
    client.stop();
    data_delay = DATA_DELAY;

    delay(240000);
  }
  else if(command.startsWith("changeTimer"))//recebe o tempo padrão de envio do servidor e realiza os ajustes
  {
    data_delay = long(command.substring(11).toInt());//recebe o novo tempo
    messageDelay = data_delay * 1000;//aplica o novo tempo
    
    command = "Recebido pedido de alteração do intervalo de envio. Alterado para: ";
    command += data_delay;
    command +=" segundos.";
    
    Serial.println(command);
  }
}

//carrega da memoria o id unico do dispositivo
void EEPROM_loadUniqueId(int addres, int keySize)
{
  int i,j=0;
  int fim = addres + keySize;
  
  for(i=addres; i<=fim; i++)
  {
    DEVICE_UNIQUE_ID[j] = EEPROM.read(i);

    j++;
  }

  DEVICE_UNIQUE_ID[32] = '\0';

  return;
}

//limpa todos os dados da EEPROM setando o valor '0'
void EEPROM_Clear() {

  for (int i = 0 ; i < EEPROM.length() ; i++)
  {
    EEPROM.write(i, 0);
  }

  delay(10);

  Serial.println("EEPROM apagada");

  return;
}


