using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using RSACypher;

namespace Domus
{
    class Program
    {
        private static bool desligar = false;//kill switch utilizado para desligar o servidor
        private static BlockingCollection<ConnectionCommandStore> DeviceConnections = new BlockingCollection<ConnectionCommandStore>(new ConcurrentQueue<ConnectionCommandStore>());
        private static BlockingCollection<ConnectionCommandStore> ClientConnections = new BlockingCollection<ConnectionCommandStore>(new ConcurrentQueue<ConnectionCommandStore>());
        private static ConfigHandler config = new ConfigHandler();
        private static string connectionString;

        static void Main(string[] args)
        {
            TcpListener deviceServer = null;
            TcpListener clientServer = null;
            Thread deviceListener = null;
            Thread clientListener = null;
            Thread connectionCleaner = null;
            connectionString = DatabaseHandler.CreateConnectionString(config.databaseIP, config.databasePort, config.databaseName, config.databaseUser, config.databasePassword);

            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);

            try
            {
                ConsoleWrite("Domus Server - Version: {0}", true, Assembly.GetExecutingAssembly().GetName().Version);
                ConsoleWrite("To Exit press Ctrl + C.",false);

                //cria a conexão
                deviceServer = Connect(config.deviceListeningPort);
                clientServer = Connect(config.clientListeningPort);
                
                if (deviceServer == null || clientServer == null)//se falhar sai do programa
                {
                    ConsoleWrite("Fail to start listeners.",true);
                    
                    Console.Read();
                    return;
                }
                
                //verifica se o banco de dados está ativo
                try
                {
                    ConsoleWrite("Testing database connection on {0}:{1}", true, config.databaseIP, config.databasePort);
                    DatabaseHandler.TestConnection(connectionString);
                }
                catch (Exception e)
                {
                    ConsoleWrite("Database connection Failure. {0}", true, e.Message);
                    Console.Read();
                    return;
                }

                connectionCleaner = new Thread(() => ClearConnectionList());
                connectionCleaner.IsBackground = true;
                connectionCleaner.Name = "Connection Cleaner";
                connectionCleaner.Start();

                // starts the listening loop. 
                ConsoleWrite("Starting device listener on port {0}", true, config.deviceListeningPort);
                deviceListener = new Thread(() => DeviceListener(deviceServer));
                deviceListener.Name = "Device Listener";
                deviceListener.Start();

                
                ConsoleWrite("Starting client listener on port {0}", true, config.clientListeningPort);
                clientListener = new Thread(() => ClientListener(clientServer));
                clientListener.Name = "Client Listener";
                clientListener.Start();
                ConsoleWrite("Client connection length set to {0} with hash type {1}", true, config.RSAlength, config.HashTypeName());
                
                WaitKillCommand();
            }
            catch (SocketException e)
            {
                ConsoleWrite("SocketException: {0}", true, e);
            }
            finally
            {
                ConsoleWrite("Disconnecting all clients and devices...", true);
                desligar = true;

                // Stop listening for new clients.
                ConsoleWrite("Stopping listeners...", true);

                deviceServer.Stop();
                clientServer.Stop();

                JoinAllConnections();//Wait for all clients and devices to disconnect

                if (deviceListener != null && deviceListener.IsAlive)
                    deviceListener.Join();
                if (clientListener != null && clientListener.IsAlive)
                    clientListener.Join();

                ConsoleWrite("Stopped", true);

                ConsoleWrite("Saving configs...", true);
                config.SaveConfigs();
                ConsoleWrite("Saved", true);

                ConsoleWrite("Server Stoped.", true);

                config.stopWorkers = true;

            }

            return;
        }

        //função que cria a conexão com a rede
        private static TcpListener Connect(Int32 port, bool intranet = false)
        {

            IPAddress localAddr = IPAddress.Parse(GetLocalIPAddress());//iplocal
            TcpListener server = null;

            try
            {
                if (intranet)
                    server = new TcpListener(localAddr, port);
                else
                    server = new TcpListener(IPAddress.Any, port);

                return server;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }

        }

        private static void WaitKillCommand()
        {
            while (!desligar)
            {
                Thread.Sleep(100);
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (e.SpecialKey == ConsoleSpecialKey.ControlC)
            {
                desligar = true;
                e.Cancel = true;
            }
        }

        //thread responsavel por aguardar conexões de devices
        private static void DeviceListener(TcpListener deviceServer)
        {

            // Start listening for client requests.
            deviceServer.Start();
            Thread newDevice;
            ConnectionCommandStore temp;

            ConsoleWrite("Waiting for device connection... ", true);

            while (desligar == false)
            {
                // Perform a blocking call to accept requests. 
                try
                {
                    TcpClient device = deviceServer.AcceptTcpClient();

                    if (config.maxDevicesConnections > DeviceConnections.Count || config.maxDevicesConnections == -1)
                    {
                        temp = new ConnectionCommandStore();

                        newDevice = new Thread(() => DeviceThread(device, temp)); //cria uma nova thread para tratar o device
                        newDevice.IsBackground = true;

                        temp.conexao = newDevice;

                        DeviceConnections.Add(temp);//adiciona para a lista de conexões

                        newDevice.Start();
                    }
                    else
                    {
                        ClientWrite(device.GetStream(), "The Server is Full.");
                        device.Close();
                    }

                }
                catch (Exception)
                {
                    break;
                }
            }
        }
        
        //thread responsavel por aguardar conexões de clientes
        private static void ClientListener(TcpListener clientServer)
        {

            // Start listening for client requests.
            clientServer.Start();
            Thread newClient;
            ConnectionCommandStore temp;

            ConsoleWrite("Waiting for client connection... ", true);

            while (desligar == false)
            {
                // Perform a blocking call to accept requests. 
                // You could also user server.AcceptSocket() here.
                try
                {
                    TcpClient client = clientServer.AcceptTcpClient();

                    if (config.maxClientsConnections > ClientConnections.Count || config.maxClientsConnections == -1)
                    {
                        temp = new ConnectionCommandStore();

                        newClient = new Thread(() => ClientThread(client, temp)); //cria uma nova thread para tratar o cliente
                        newClient.IsBackground = true;

                        temp.conexao = newClient;

                        ClientConnections.Add(temp);//adiciona para a lista de conexões

                        newClient.Start();
                    }
                    else
                    {
                        ClientWrite(client.GetStream(), "The Server is Full.");
                        client.Close();
                    }

                }
                catch (Exception)
                {
                    break;
                }
            }
        }
        
        //Thread que cuida dos dispositivos após conectado
        private static void DeviceThread(TcpClient device, ConnectionCommandStore me)
        {
            // Buffer for reading data
            Byte[] bytes = new Byte[256];
            String data = null;
            NetworkStream stream;
            bool lostConnection = false, getingDeviceInfos = false;
            int i, timeOutCounter = 0;

            me.clientIP = device.Client.RemoteEndPoint.ToString().Split(':')[0];

            if (!config.bannedIPs.Contains(me.clientIP))
            {
                ConsoleWrite("Device " + me.clientIP +
                             " connected on port {0}", true, device.Client.RemoteEndPoint.ToString().Split(':')[1]);
            }

            while (device.Connected)
            {
                data = null;

                // Get a stream object for reading and writing
                stream = device.GetStream();

                if (config.bannedIPs.Contains(me.clientIP))//if device is banned
                {
                    ClientWrite(stream, "banned");
                    lostConnection = true;
                }

                if (desligar)//se receber o sinal de desligar
                {
                    ClientWrite(stream, "shutdown");

                    if (me.deviceUniqueID != null)
                        ConsoleWrite("Device {0} disconnected by server shutting down command.", true, me.deviceUniqueID);
                    else
                        ConsoleWrite("Unknown Device from IP {0} disconnected by server shutting down command.", true, me.clientIP);

                    lostConnection = true;
                }

                if (lostConnection)//se a conexão for perdida
                {
                    stream.Close();// end stream
                    device.Close(); // end connection
                    break;
                }

                if (stream.DataAvailable)//se houver dados a serem lidos
                {
                    try
                    {
                        // Loop to receive all the data sent by the client
                        while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            // Translate data bytes to a ASCII string.
                            data = Encoding.ASCII.GetString(bytes, 0, i);

                            if (getingDeviceInfos && data.Contains("infos"))//set device infos to the memory
                            {
                                ConnectionCommandStore temp = DeviceConnections.FirstOrDefault(ConnectionCommandStore =>
                                        ConnectionCommandStore.deviceUniqueID == data.Split(';')[3]);

                                if(temp != null)//verify if the device already has an connection on the list.
                                {
                                    lostConnection = true; //derruba o cliente

                                    ClientWrite(stream, "uidit");//send UID is taken to device
                                    ConsoleWrite("Device {0} is tying to connect using an UID that is already taken.", true, me.clientIP);
                                }
                                else//if it has not, then accepts the connection.
                                {
                                    me.deviceName = data.Split(';')[1];
                                    me.deviceType = data.Split(';')[2];
                                    me.dataDelay = (Convert.ToInt32(data.Split(';')[3]) * 10) + 100;//pega o tempo do delay e adicionar 10 segundos
                                    me.deviceUniqueID = data.Split(';')[4];

                                    if (me.dataDelay < config.minDataDelay)//if delay < minDataDelay segundos (30 + 10)
                                    {
                                        me.dataDelay = config.minDataDelay;//sets the delay to 30+10 segundos
                                        ClientWrite(stream, "changeTimer " + (config.minDataDelay - 100) / 10);
                                    }

                                    getingDeviceInfos = false;

                                    ConsoleWrite("Device {0} was identified as '{1}'", true, me.clientIP, me.deviceUniqueID);
                                }
                            }
                            else if (data == "??\u001f?? ??\u0018??'??\u0001??\u0003??\u0003")//se um cliente tentar se conectar na porta de devices
                            {
                                ConsoleWrite("Connection denied to client {0}. Wrong connection port.", true, me.clientIP, data);
                                ClientWrite(stream, "Connection Denied. You will be disconnected in 5 seconds.");
                                Thread.Sleep(5000);
                                lostConnection = true;
                            }
                            else if (data == "shakeback")
                            {
                                ConsoleWrite("Device {0} has shaked back.", false, me.clientIP, data);
                                getingDeviceInfos = true;
                                ClientWrite(stream, "SendInfos");//get device infos from device
                            }
                            else if (data != "imhr")
                            {
                                Data deviceData = new Data(0, me.deviceUniqueID);
                                string[] datas = data.Split(';');

                                for (i = 0; i < datas.Length; i++)
                                {
                                    if (i == 0)
                                        deviceData.data1 = data.Split(';')[i];
                                    else if (i == 1)
                                        deviceData.data2 = data.Split(';')[i];
                                    else if (i == 2)
                                        deviceData.data3 = data.Split(';')[i];
                                    else if (i == 3)
                                        deviceData.data4 = data.Split(';')[i];
                                }

                                ConsoleWrite("Device '{0}' has sent: {1}", false, me.deviceUniqueID, data);

                                //inserir dados no banco
                                try
                                {
                                    DatabaseHandler.InsertData(connectionString, deviceData);
                                }
                                catch (Exception e)
                                {
                                    ConsoleWrite("ERROR on insert device {0} data: {1}", true, me.deviceUniqueID, e.Message);
                                }

                            }

                            timeOutCounter = 0;

                            if (stream.DataAvailable == false)
                                break;
                        }
                    }
                    catch (Exception)//caso a leitura falhe
                    {
                        ConsoleWrite("Device {0} disconnected. Connection timeout.", true, me.deviceUniqueID);
                        lostConnection = true;
                    }
                }
                else
                {
                    if (timeOutCounter == me.dataDelay)//se após x segundos não houver comunicação, verifica se o cliente esta online
                    {
                        if (getingDeviceInfos)//if connection if timming out and the defice isn't indentificated yet
                            ClientWrite(stream, "SendInfos");//get device infos from device

                        ClientWrite(stream, "ayt");//envia um are you there
                    }
                    else if (timeOutCounter > me.dataDelay + 50)//espera 5 segundos para resposta
                    {
                        ConsoleWrite("Device {0} disconnected.", true, me.deviceUniqueID);
                        lostConnection = true;
                    }

                    timeOutCounter++;

                    Thread.Sleep(100);
                }

            }

            return;
        }
        
        //Thread que cuida dos clientes após conectado
        private static void ClientThread(TcpClient client, ConnectionCommandStore me)
        {
            // Buffer for reading data
            Byte[] bytes = new Byte[1024];
            String data = null;
            NetworkStream stream;
            bool lostConnection = false, login = false, isLoggedIn = false, keysChanged = false;
            int i, timeOutCounter = 0;
            int timeOutTime = 10 * 60 * 1000;//10 minutos
            RsaHandler rsa = new RsaHandler(config.RSAlength, config.RSAHashType);//chave 2048 bits com SHA1

            me.clientIP = client.Client.RemoteEndPoint.ToString().Split(':')[0];

            if (!config.bannedIPs.Contains(me.clientIP))
            {
                ConsoleWrite("Client " + me.clientIP +
                             " connected on port {0}", true, client.Client.RemoteEndPoint.ToString().Split(':')[1]);
            }

            while (client.Connected)
            {
                data = null;

                // Get a stream object for reading and writing
                stream = client.GetStream();

                if (config.bannedIPs.Contains(me.clientIP))
                {
                    ConsoleWrite("Can't connect. The IP {0} is banned.", false, me.clientIP);
                    ClientWrite(stream, "Can't connect. The client is banned.");
                    Thread.Sleep(5000);
                    lostConnection = true;
                }

                if (desligar)//se receber o sinal de desligar
                {
                    ConsoleWrite("Client {0} disconnected by server shutting down command.", false, me.clientIP);
                    lostConnection = true;
                }

                if (lostConnection)//se a conexão for perdida
                {
                    stream.Close();// end stream
                    client.Close(); // end connection
                    break;
                }

                if (stream.DataAvailable)//se houver dados a serem lidos
                {
                    try
                    {
                        // Loop to receive all the data sent by the client
                        while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            // Translate data bytes to a ASCII string.
                            data = Encoding.ASCII.GetString(bytes, 0, i);

                            if (rsa.OuterPublicKey != null)
                            {
                                data = rsa.Decrypt(data);
                            }

                            if (!keysChanged && data.Contains("<RSAKeyValue>"))
                            {
                                ConsoleWrite("Started secure connection with client {0}", true, me.clientIP);
                                rsa.OuterPublicKey = data;//salva a chave publica

                                ClientWrite(stream, rsa.PublicKey);//envia a chave publica

                                keysChanged = true;
                            }
                            else if (isLoggedIn)
                            {
                                if (data == "stop\r\n")
                                    desligar = true;
                                else if (data.StartsWith("banip"))
                                {
                                    config.AddBannedIp(data.Split(' ')[1].Replace("\r\n", ""));
                                    ConsoleWrite("Client {0} has banned the ip {1}", true, me.clientIP, data.Split(' ')[1]);
                                    ClientWrite(stream, "You have banned " + rsa.Encrypt(data).Split(' ')[1]);
                                }
                                else if (data.StartsWith("unbanip"))
                                {
                                    config.RemoveBannedIp(data.Split(' ')[1].Replace("\r\n", ""));
                                    ConsoleWrite("Client {0} has unbanned the ip {1}", true, me.clientIP, data.Split(' ')[1]);
                                    ClientWrite(stream, "You have unbanned " + rsa.Encrypt(data).Split(' ')[1]);
                                }
                                else if (data.Contains("exit"))
                                {
                                    ConsoleWrite("Client {0} has exited.", true, me.clientIP);
                                    lostConnection = true;
                                }
                                else if (data.Contains("getdata"))
                                {
                                    string device = data.Split(' ')[1].Replace("\r\n", "");
                                    string deviceData = null;

                                    try
                                    {
                                        foreach (var DeviceConection in DeviceConnections.ToList())
                                        {
                                            if (DeviceConection.deviceName == device)
                                            {
                                                deviceData = DeviceConection.command;
                                                break;
                                            }
                                        }

                                        if (deviceData != null)
                                            ClientWrite(stream, rsa.Encrypt("The last received data was: " + deviceData + "\r\n"));
                                        else
                                            ClientWrite(stream, rsa.Encrypt("No data Available.\r\n"));
                                    }
                                    catch (Exception)
                                    {
                                        ClientWrite(stream, rsa.Encrypt("Failed to retrive data. Verify if Device Name is correct.\r\n"));
                                    }

                                }
                                else if (data.Contains("listdevices"))
                                {
                                    int cont = 0;

                                    foreach (var device in DeviceConnections.ToList())
                                    {
                                        if (device.conexao.IsAlive)
                                        {
                                            ClientWrite(stream, rsa.Encrypt(device.deviceName + Environment.NewLine));
                                            cont++;
                                        }
                                    }

                                    if (cont == 0)
                                        ClientWrite(stream, rsa.Encrypt("There are no devices connected.\r\n"));
                                }
                                else
                                {
                                    ClientWrite(stream, rsa.Encrypt("Invalid command.\r\n"));
                                    ConsoleWrite("Client {0} has send: {1}", false, me.clientIP, data);
                                }
                                timeOutCounter = 0;

                                if (stream.DataAvailable == false)
                                    break;
                            }
                            else if (!isLoggedIn && rsa.OuterPublicKey != null)
                            {
                                login = true;

                                if (data.Contains("<Login>"))
                                {
                                    data = data.Replace("<Login>", "");

                                    string[] userdata = data.Split(';');

                                    if (userdata[0] == "admin" && userdata[1] == "admin")
                                    {
                                        login = false;
                                        isLoggedIn = true;

                                        ClientWrite(stream, rsa.Encrypt("sucessfullLogin"));

                                        ConsoleWrite("Client has logged in as {0}", true, "admin");
                                    }
                                    else
                                    {
                                        ClientWrite(stream, rsa.Encrypt("wrongLogin"));
                                    }

                                }
                            }
                        }

                    }
                    catch (Exception)//caso a leitura falhe
                    {
                        ConsoleWrite("Client {0} disconnected. Connection timeout.", true, me.clientIP);
                        lostConnection = true;
                    }
                }
                else
                {
                    if (timeOutCounter == timeOutTime)//se após 10 minutos não houver comunicação, verifica se o cliente esta online
                    {
                        ConsoleWrite("Client {0} disconnected.", true, me.clientIP);
                        lostConnection = true;
                    }

                    timeOutCounter++;

                    Thread.Sleep(100);
                }

            }
            return;
        }

        //Função para printar no console
        private static void ConsoleWrite(string message, bool logThis, params object[] args)
        {
            message = DateTime.Now.ToString(new CultureInfo("pt-BR")) + " - " + message;

            Console.WriteLine(message, args);

            if ((config.logEnabled && logThis) || config.forceLog)
            {
                if (config.maxLogSize != -1 && config.GetLogSize() >= config.maxLogSize)
                {
                    config.AddLog("Log is full.");
                    config.DisableLog();
                    ConsoleWrite("Log File is full!!!", false);
                }
                else
                    config.AddLog(message, args);
            }

        }

        //Função que envia mensagens ao cliente conectado
        private static bool ClientWrite(NetworkStream stream, String message)
        {
            try
            {
                byte[] msg = System.Text.Encoding.ASCII.GetBytes(message);
                stream.Write(msg, 0, msg.Length);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        //Garbage colector que limpa a lista de clientes e devices conectados
        private static void ClearConnectionList()
        {
            while (desligar == false)
            {

                int DevicesInMemory = DeviceConnections.Count();
                ConnectionCommandStore temp;

                //remove as conexões de devices encerradas da lista
                for (int i = 0; i < DevicesInMemory; i++)
                {
                    temp = DeviceConnections.Take();

                    if (temp.conexao.IsAlive)
                    {
                        DeviceConnections.Add(temp);
                    }
                    else
                    {
                        temp = null;
                    }
                }

                //remove as conexões de clientes encerradas da lista
                for (int i = 0; i < DevicesInMemory; i++)
                {
                    temp = ClientConnections.Take();

                    if (temp.conexao.IsAlive)
                    {
                        ClientConnections.Add(temp);
                    }
                    else
                    {
                        temp = null;
                    }
                }

                if (!desligar)//se estiver desligando pula o timer
                    Thread.Sleep(30000);
            }
        }

        private static void JoinAllConnections()
        {
            //Join Devices
            foreach (var device in DeviceConnections)
            {
                if (device.conexao.IsAlive)
                    device.conexao.Join();
            }

            //Join Clients
            foreach (var client in ClientConnections)
            {
                if (client.conexao.IsAlive)
                    client.conexao.Join();
            }
        }

        //função que resgata o IP local do servidor
        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("Local IP Address Not Found!");
        }
    }
}
