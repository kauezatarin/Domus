/* Domus Base Device Code
 *  
 * É necessário alterar o valor definido em SERIAL_RX_BUFFER_SIZE para 128 no arquivo %appData%\Local\Arduino15\packages\arduino\hardware\avr\1.6.11\cores\arduino\HardwareSerial.h
 * para que seja possivel receber todas as informações necessárias para configuração do dispositivo através da serial.
 * 
 * Desenvolvido por: Kauê Zatarin
 */

#include <SPI.h>
#include <Ethernet.h>
#include <DHT.h>
#include <DHT_U.h>
#include <EEPROM.h>
#include <Arduino.h> // for type definitions

#define DHTPIN A1 // pino que estamos conectado
#define DHTTYPE DHT11 // DHT 11

#define DATA_DELAY 30 //preserva o delay original para restauração futura

char DEVICE_UNIQUE_ID[33] = "698dc19d489c4e4db73e28a713eab07b"; //id unico do device vinculado a sua conta

// pode ser convertido para bytes em decimal
byte mac[6] = {
  0x00, 0xE0, 0x4C, 0x48, 0x24, 0x9F
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

unsigned int connectionPort = 9595;

EthernetClient client;

unsigned long data_delay = DATA_DELAY;//delay para envio de dados em segundos

int reconnectDelay = 10 * 1000;//intervalo entre tentativas de conexão
unsigned long messageDelay = data_delay * 1000;//intervalo entre envio de dados
unsigned long lastTime = 0;

String inData = String(100);
String outData = String(100);
char c;

bool isDebugging = true;

//sensor de humidade e temperatura
DHT dht(DHTPIN, DHTTYPE);
float temperatura;
float humidade;

//declaração de funções
bool tryConnection();
void executeCommand(String command);
void executeSerialCommand();
void EEPROM_loadConfigs();
void EEPROM_save();
void EEPROM_loadUniqueId(int addres, int keySize);
void EEPROM_Clear();
void SerialPrint(String text, bool debug);

void setup() {
  
  Serial.begin(9600);

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
  
  dht.begin();
}

void loop() {

  // caso receba dados pela serial
  if (Serial.available() > 0)
  {
    isDebugging = false; // desliga o envio de textos de depuração através da serial
    executeSerialCommand();
  }

  // caso exista uma conexão ativa
  if (client) {
    
    if (client.connected() == false)
    {
      SerialPrint("Conexão Perdida!", true);
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

      SerialPrint(inData,true);
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
        SerialPrint("Dados enviados: ",true);
        SerialPrint(outData, true);
      }
    }   
  }
  else
  {  
    tryConnection();
    
    if (!client)
      delay(reconnectDelay);       
  }
}

//tenta se conectar ao servidor
bool tryConnection()
{
  SerialPrint("connecting...",true);
  
  if (client.connect(servidor, connectionPort)) {
    SerialPrint("connected",true);
    client.print("shakeback");
    delay(100);
    return true;
  }
  else {
    SerialPrint("connection failed",true);
    return false;
  }
}

//executa os comandos recebidos pela rede
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
    outData += data_delay;
    outData += ";";
    outData += DEVICE_UNIQUE_ID;

    client.print(outData);
    SerialPrint("Identificação realizada: ",true);
    SerialPrint(outData,true);
  }
  else if(command == "uidit")//já existe um dispositivo com esse UID conectado
  {
    SerialPrint("Uid já esta sendo usado. Tente novamente em 60 segundos. Isto pode ocorrer por conta de queda recente de conexão.",true);
    client.stop();

    delay(60000);
  }
  else if(command == "uidnf")//o UID do sispositivo não está cadastrado no servidor.
  {
    SerialPrint("Uid não cadastrado. Tentando novamente em 30 minutos. Isto ocorre quando o dispositivo não está cadastrado no servidor.",true);
    client.stop();

    delay(1800000);
  }
  else if(command == "shutdown")//recebido o comando de 'Server Shutdown'
  {
    SerialPrint("Recebido comando de desligamento de servicor. Conexão encerrada. Hibernando por 240 segundos...",true);
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
    
    SerialPrint(command,true);
  }
}

//executa comandos recebidos pela serial
void executeSerialCommand()
{
  String serial = Serial.readString();//recebe os dados da serial
  char command[serial.length()+1];
  char *p = command;
  char *str;
  int i = 0;

  serial.toCharArray(command,serial.length()+1);//converte os dados para um vetor de caracteres.

  if(command[0] == '0')//caso receba o handshake
  {
    Serial.println("domus");
  }
  else if(command[0] == '1')//caso seja solicitado o envio das configurações
  {
    outData = "";

    //ip do servidor
    outData += serverIp[0];
    outData += ";";
    outData += serverIp[1];
    outData += ";";
    outData += serverIp[2];
    outData += ";";
    outData += serverIp[3];
    outData += ";";

    //porta do servidor
    outData += connectionPort;
    outData += ";";

    //é DHCP?
    outData += isDHCP;
    outData += ";";

    //ip do dispositivo
    outData += deviceIp[0];
    outData += ";";
    outData += deviceIp[1];
    outData += ";";
    outData += deviceIp[2];
    outData += ";";
    outData += deviceIp[3];
    outData += ";";

    //mac do dispositivo
    outData += mac[0];
    outData += ";";
    outData += mac[1];
    outData += ";";
    outData += mac[2];
    outData += ";";
    outData += mac[3];
    outData += ";";
    outData += mac[4];
    outData += ";";
    outData += mac[5];
    outData += ";";

    //UID
    outData += DEVICE_UNIQUE_ID;
  
    Serial.println(outData);
  }
  else if(command[0] == '2')//caso receba configurações a serem aplicadas
  {
    while ((str = strtok_r(p, ";", &p)) != NULL)// separa os caracteres
    {
      if(i>=1 && i < 5)
      {
        serverIp[i-1] = (byte)atoi(str);//converte a string para byte
      }
      else if(i == 5)
      {
        connectionPort = atoi(str);//converte a string para int
      }
      else if(i == 6)
      {
          isDHCP = (byte)atoi(str);//converte a string para byte
      }
      else if(i>=7 && i < 11)
      {
        deviceIp[i-7] = (byte)atoi(str);//converte a string para byte
      }
      else if(i>=11 && i < 17)
      {
        mac[i-11] = (byte)atoi(str);//converte a string para byte
      }
      else if(i == 17)
      {      
        for(int j=0; j<34; j++)
        {
          DEVICE_UNIQUE_ID[j] = *(str+j);
        }    
      }
  
      i++;
    }

    EEPROM_save();
  }
  else if(command[0] == '3')//caso receba o comando de limpeza reseta a memoria
  {
    EEPROM_Clear();
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

  SerialPrint("Configurações carregadas.",true);
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

  SerialPrint("Configurações salvas.",true);
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

  SerialPrint("EEPROM apagada",true);

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

void SerialPrint(String text, bool debug)
{
  if(isDebugging == false && debug == true)//caso seja teexto de depuração e exista uma conexão serial ativa, ignora o envio.
  {
    return; 
  }
  else
  {
    Serial.println(text);
  }
}

