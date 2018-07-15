#include <SPI.h>
#include <Ethernet.h>
#include <EEPROM.h>
#include <Arduino.h> // for type definitions
#include <EmonLib.h>

#define PINO_SCT A0 // pino que está o sensor SCT

#define DATA_DELAY 30 //preserva o delay original para restauração futura

#define DEVICE_TIPE 1; //tipo de dispositivo REMOVER
#define DEVICE_NAME "Casa"; //nome do dispositivo REMOVER

char DEVICE_UNIQUE_ID[33] = "698dc19d489c4e4db73e28a713eab07b"; //id unico do device vinculado a sua conta

// pode ser convertido para bytes em decimal
byte mac[6] = {
  0xDE, 0xAD, 0xBE, 0xEF, 0xFE, 0xED
};

byte deviceIp[4]= {
  192, 168, 1, 50
};

byte serverIp[4]= {
  192, 168, 1, 41
};

bool isDHCP = true;
IPAddress ip;
IPAddress servidor;

int connectionPort = 9595;

EthernetClient client;

unsigned long data_delay = DATA_DELAY;//delay para envio de dados em segundos

int reconnectDelay = 10 * 1000;//intervalo entre tentativas de conexão
unsigned long messageDelay = data_delay * 1000;//intervalo entre envio de dados
unsigned long lastTime = 0;

String inData = String(100);
String outData = String(100);
char c;

//sensor de corrente
EnergyMonitor emon1;

//Tensao da rede eletrica
int rede = 120;

void setup() {
  
  Serial.begin(9600);
  
  //Pino, calibracao - Cur Const= Ratio/BurdenR. 2000/33 = 60.60
  emon1.current(PINO_SCT, 60);

  if(EEPROM.read(0) != 'c')
  {
    EEPROM_Clear();
    EEPROM_save();
  }   

  EEPROM_loadConfigs(); //Carrega as configurações da EEPROM

  ip = IPAddress(deviceIp[0], deviceIp[1], deviceIp[2], deviceIp[3]);
  servidor = IPAddress(serverIp[0], serverIp[1], serverIp[2], serverIp[3]);
  
  //inicializa a placa de rede
  if(isDHCP)
  {
    Ethernet.begin(mac); //pega um IP via DHCP
  }
  else
  {
    //Ethernet.begin(mac, ip, myDns, gateway, subnet);
    Ethernet.begin(mac, ip);
  }
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
        //Calcula a corrente
		double Irms = emon1.calcIrms(1480);

        outData += Irms;//corrente
        outData += ";";
        outData += (Irms * rede);//potencia
        //outData += ";";
        //outData += dht.computeHeatIndex(temperatura, humidade, false);
        
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

//tenta se conectar ao servidor
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

//executa os comandos recebidos
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
  else if(command == "uidit")//já existe um dispositivo com esse UID conectado
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

//carrega todas as configurações
void EEPROM_loadConfigs()
{
  for(int i =0; i < 4; i++) //carrega o ip do servidor
  {
    serverIp[i] = EEPROM.read(1+i);
  }

  isDHCP = EEPROM.read(7); //carrega se o device deverá obter seu ip através de DHCP

  EEPROM_readAnything(5, connectionPort); //carrega a porta de conexão

  for(int i =0; i < 4; i++) //carrega o ip do dispositivo caso exista alteração
  {
    deviceIp[i] = EEPROM.read(8+i);
  }

  for(int i =0; i < 6; i++) //carrega o mac do dispositivo caso exista alteração
  {
    mac[i] = EEPROM.read(12+i);
  }

  EEPROM_readAnything(18, connectionPort); //carrega o id unico do dispositivo
}

//salva todas as configurações na memoria
void EEPROM_save()
{

  EEPROM.update(0,'c'); //atualiza a flag caso seja a primeira configuração
  
  for(int i =0; i < 4; i++) //atuaiza o ip do servidor caso exista alteração
  {
    EEPROM.update(1+i, serverIp[i]);
  }

  EEPROM.update(7, isDHCP); //atualiza se o device deverá obter seu ip através de DHCP

  EEPROM_writeAnything(5, connectionPort); //salva a porta de conexão

  for(int i =0; i < 4; i++) //atuaiza o ip do dispositivo caso exista alteração
  {
    EEPROM.update(8+i, deviceIp[i]);
  }

  for(int i =0; i < 6; i++) //atuaiza o mac do dispositivo caso exista alteração
  {
    EEPROM.update(12+i, mac[i]);
  }

  EEPROM_writeAnything(18, connectionPort); //salva o id unico do dispositivo
  
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

template <class T> int EEPROM_writeAnything(int ee, const T& value)
{
    const byte* p = (const byte*)(const void*)&value;
    unsigned int i;
    for (i = 0; i < sizeof(value); i++)
          EEPROM.update(ee++, *p++);
    return i;
}

template <class T> int EEPROM_readAnything(int ee, T& value)
{
    byte* p = (byte*)(void*)&value;
    unsigned int i;
    for (i = 0; i < sizeof(value); i++)
          *p++ = EEPROM.read(ee++);
    return i;
}
