using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MySql.Data.MySqlClient;
using DomusSharedClasses;
using log4net;
using log4net.Config;

namespace Domus
{
    class Program
    {
        private static bool desligar = false;//kill switch utilizado para desligar o servidor
        private static BlockingCollection<ConnectionCommandStore> DeviceConnections = new BlockingCollection<ConnectionCommandStore>(new ConcurrentQueue<ConnectionCommandStore>());
        private static BlockingCollection<ConnectionCommandStore> ClientConnections = new BlockingCollection<ConnectionCommandStore>(new ConcurrentQueue<ConnectionCommandStore>());
        private static ConfigHandler config = new ConfigHandler();
        private static string connectionString;
        private static WeatherHandler Weather;
        private static Forecast forecast;
        private static TaskScheduler scheduler = new TaskScheduler();//scheduler.scheduleTask(DateTime.Now + new TimeSpan(0, 0, 10), async ()=> {Teste();}, "Hourly");
        private static ILog log;
        private static TcpListener deviceServer = null;
        private static TcpListener clientServer = null;
        private static Thread deviceListener = null;
        private static Thread clientListener = null;

        static void Main(string[] args)
        {
            Thread connectionCleaner = null;

            AppDomain.CurrentDomain.ProcessExit += onSystemshutdown;
            
            Console.Title = "Domus - " + Assembly.GetExecutingAssembly().GetName().Version;
            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);

            connectionString = DatabaseHandler.CreateConnectionString(config.databaseIP, config.databasePort, config.databaseName, config.databaseUser, config.databasePassword);
            Weather = new WeatherHandler(config.cityName, config.countryId, config.weatherApiKey);// adicionar os parametros na configuração

            #region LogStarter

            XmlDocument log4netConfig = new XmlDocument();

            log4netConfig.Load(File.OpenRead("log4net.config"));

            var repo = LogManager.CreateRepository(Assembly.GetEntryAssembly(),
                typeof(log4net.Repository.Hierarchy.Hierarchy));

            XmlConfigurator.Configure(repo, log4netConfig["log4net"]);

            log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            #endregion

            try
            {
                Console.Clear();
                log.Info("Domus Server - Version: " + Assembly.GetExecutingAssembly().GetName().Version);
                log.Info("To Exit press Ctrl + C.");

                //cria a conexão
                log.Info("Creating listeners");
                try
                {
                    deviceServer = Connect(config.deviceListeningPort);
                    clientServer = Connect(config.clientListeningPort);
                }
                catch (Exception e)//se falhar sai do programa
                {
                    log.Fatal("Fail to create listeners.", e);

                    Console.Read();
                    return;
                }

                bool databasereached = false;

                while (databasereached == false)
                {
                    //verifica se o banco de dados está ativo
                    try
                    {
                        log.Info("Testing database connection on " + config.databaseIP + ":" +config.databasePort);
                        DatabaseHandler.TestConnection(connectionString);
                        databasereached = true;
                        log.Info("Database Connection success");
                    }
                    catch (MySqlException e)
                    {
                        log.Error("Database connection Failure. " + e.Number + " - " + e.Message);
                        log.Info("Retrying in 30 seconds...");
                        Thread.Sleep(30000);
                    }
                }
                
                //Tenta resgatar a previsão do tempo
                log.Info("Acquiring forecast informations");

                try
                {
                    forecast = Weather.CheckForecast();

                    log.Info("Successfully acquired forecasts for " + forecast.Location_Name + "," +
                             forecast.Location_Country + " (" + forecast.Location_Latitude + ";" + forecast.Location_Longitude + ") ");
                }
                catch (Exception e)
                {
                    log.Warn("Fail to acquire forecast informations for "+ config.cityName  + ","+ config.countryId + " ==>" + e.Message);
                }

                try
                {
                    log.Info("Scheduling server tasks");

                    DateTime temp = DateTime.Now;
                    temp = temp.Subtract(new TimeSpan(temp.Hour, temp.Minute, temp.Second));//turns ascheduler time to 00:00:00
                    temp = temp.AddDays(1);

                    scheduler.ScheduleTask(temp, RefreshForecast, "Daily");

                    log.Info("Forecast updater scheduled to run daily at 00:00:00");

                    ScheduleIrrigationTaskts(DatabaseHandler.GetAllIrrigationSchedules(connectionString));

                    log.Info("Tasks scheduled");
                }
                catch (Exception e)
                {
                    log.Error("Fail to schedule server tasks -> " + e.Message, e);
                }

                connectionCleaner = new Thread(() => ClearConnectionList());
                connectionCleaner.IsBackground = true;
                connectionCleaner.Name = "Connection Cleaner";
                connectionCleaner.Start();

                // starts the listening loop. 
                log.Info("Starting device listener on port " + config.deviceListeningPort);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                deviceListener = new Thread(() => DeviceListenerAsync(deviceServer));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                deviceListener.Name = "Device Listener";
                deviceListener.Start();

                
                log.Info("Starting client listener on port " + config.clientListeningPort);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                clientListener = new Thread(() => ClientListenerAsync(clientServer));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                clientListener.Name = "Client Listener";
                clientListener.Start();
                
                WaitKillCommand();
            }
            catch (SocketException e)
            {
                log.Fatal("SocketException: " + e.Message, e);
            }

            return;
        }

        //rotina executada quando o servidor está sendo encerrado
        static void StopRoutine()
        {
            try
            {
                log.Info("Disconnecting all clients and devices...");
                desligar = true;

                // Stop listening for new clients.
                log.Info("Stopping listeners...");

                deviceServer.Stop();
                clientServer.Stop();

                JoinAllConnections();//Wait for all clients and devices to disconnect

                if (deviceListener != null && deviceListener.IsAlive)
                    deviceListener.Join();
                if (clientListener != null && clientListener.IsAlive)
                    clientListener.Join();

                log.Info("Stopped");

                log.Info("Cleaning scheduled tasks...");

                scheduler.DeleteAllTasks();

                while (scheduler.TasksCount() != 0)
                {
                    Thread.Sleep(100);
                }

                log.Info("Cleared");

                log.Info("Server Stoped.");
            }
            catch (Exception e)
            {
                log.Error("Error on server close routine - " + e.Message, e);
            }
        }

        //metodo chamado quando o servidor recebe um SIGterm
        private static void onSystemshutdown(object sender, EventArgs e)
        {
                StopRoutine();   
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
                throw e;
            }

        }

        //função que insere os agendamentos de irrigação
        private static void ScheduleIrrigationTaskts(List<IrrigationSchedule> schedules)
        {
            foreach (IrrigationSchedule schedule in schedules)
            {
                DateTime temp = schedule.scheduleTime;

                temp = temp.AddDays(DateTime.Now.Day - temp.Day);
                temp = temp.AddDays(DateTime.Now.Month - temp.Month);
                temp = temp.AddDays(DateTime.Now.Year - temp.Year);

                schedule.scheduleTime = temp;

                if (schedule.active)
                {
                    if (schedule.sunday)
                    {
                        temp = scheduler.GetNextWeekday(schedule.scheduleTime, DayOfWeek.Sunday);

                        scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.runFor), "weekly");

                        log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }

                    if (schedule.moonday)
                    {
                        temp = scheduler.GetNextWeekday(schedule.scheduleTime, DayOfWeek.Monday);

                        scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.runFor), "weekly");

                        log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }

                    if (schedule.tuesday)
                    {
                        temp = scheduler.GetNextWeekday(schedule.scheduleTime, DayOfWeek.Tuesday);

                        scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.runFor), "weekly");

                        log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }

                    if (schedule.wednesday)
                    {
                        temp = scheduler.GetNextWeekday(schedule.scheduleTime, DayOfWeek.Wednesday);

                        scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.runFor), "weekly");

                        log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }

                    if (schedule.thursday)
                    {
                        temp = scheduler.GetNextWeekday(schedule.scheduleTime, DayOfWeek.Thursday);

                        scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.runFor), "weekly");

                        log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }

                    if (schedule.friday)
                    {
                        temp = scheduler.GetNextWeekday(schedule.scheduleTime, DayOfWeek.Friday);

                        scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.runFor), "weekly");

                        log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }

                    if (schedule.saturday)
                    {
                        temp = scheduler.GetNextWeekday(schedule.scheduleTime, DayOfWeek.Saturday);

                        scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.runFor), "weekly");

                        log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }
                }
            }
        }

        //função que impede que o servidor morra antes de receber o comando de desligamento
        private static void WaitKillCommand()
        {
            while (!desligar)
            {
                Thread.Sleep(100);
            }
        }

        //função que aguarda o comando de encerramento do servidor
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (e.SpecialKey == ConsoleSpecialKey.ControlC)
            {
                desligar = true;
                e.Cancel = true;
            }
        }

        //thread responsavel por aguardar conexões de devices
        private static async Task DeviceListenerAsync(TcpListener deviceServer)
        {

            // Start listening for client requests.
            deviceServer.Start();
            Thread newDevice;
            ConnectionCommandStore temp;

            log.Info("Waiting for device connection... ");

            while (desligar == false)
            {
                // Perform a blocking call to accept requests. 
                try
                {
                    TcpClient device = null;
                    device = await deviceServer.AcceptTcpClientAsync();

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
        private static async Task ClientListenerAsync(TcpListener clientServer)
        {

            // Start listening for client requests.
            clientServer.Start();
            Thread newClient;
            ConnectionCommandStore temp;

            log.Info("Waiting for client connection... ");

            while (desligar == false)
            {
                // Perform a blocking call to accept requests. 
                // You could also user server.AcceptSocket() here.
                try
                {
                    TcpClient client = await clientServer.AcceptTcpClientAsync();

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
                log.Info("Device at " + me.clientIP +
                             " connected on port " + device.Client.RemoteEndPoint.ToString().Split(':')[1]);
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
                        log.Info("Device "+ me.deviceUniqueID + " disconnected by server shutting down command.");
                    else
                        log.Info("Unknown Device from IP " + me.clientIP + " disconnected by server shutting down command.");

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
                                        ConnectionCommandStore.deviceUniqueID == data.Split(';')[2]);

                                if(temp != null)//verify if the device already has an connection on the list.
                                {
                                    lostConnection = true; //drop the client

                                    ClientWrite(stream, "uidit");//send UID is taken to device
                                    log.Info("Device at "+ me.clientIP + " is tying to connect using an UID that is already taken.");
                                }
                                else if (!DatabaseHandler.IsAuthenticDevice(connectionString, data.Split(';')[2]))//verify if the device is listed at the database
                                {
                                    lostConnection = true; //drop the client

                                    ClientWrite(stream, "uidnf");//send UID not found to device
                                    log.Info("Device at "+ me.clientIP + " is tying to connect using an UID that is not registered.");
                                }
                                else//if it has not, then accepts the connection.
                                {
                                    Device tempDevice = DatabaseHandler.GetDeviceByUid(connectionString, data.Split(';')[2]); //get device infos from database

                                    me.deviceName = tempDevice.deviceName;
                                    me.deviceType = tempDevice.deviceType;
                                    me.dataDelay = (Convert.ToInt32(data.Split(';')[1]) * 10) + 100;//gets delay time and adds 10 seconds
                                    me.deviceUniqueID = data.Split(';')[2];

                                    if (me.dataDelay < config.minDataDelay && me.deviceType !=2)//if delay < minDataDelay segundos (30 + 10)
                                    {
                                        me.dataDelay = config.minDataDelay;//sets the delay to 30+10 segundos
                                        ClientWrite(stream, "changeTimer " + (config.minDataDelay - 100) / 10);
                                    }
                                    else if (me.deviceType == 2)//case it's a plug device MODIFICAR
                                    {
                                        me.dataDelay = 120;//sets the delay to 2+10 segundos
                                        ClientWrite(stream, "changeTimer " + (120 - 100) / 10);
                                    }

                                    getingDeviceInfos = false;

                                    tempDevice = null; //free var from memory

                                    log.Info("Device "+ me.clientIP + " was identified as '" + me.deviceUniqueID + "'");
                                }
                            }
                            else if (data == "??\u001f?? ??\u0018??'??\u0001??\u0003??\u0003")//se um cliente tentar se conectar na porta de devices
                            {
                                log.Error("Connection denied to client "+ me.clientIP + ". Wrong connection port.");
                                ClientWrite(stream, "WrongPort");
                                Thread.Sleep(5000);
                                lostConnection = true;
                            }
                            else if (data == "shakeback")
                            {
                                log.Info("Device "+ me.clientIP + " has shaked back.");
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

                                log.Info("Device '"+ me.deviceUniqueID + "' has sent: " +  data);

                                //inserir dados no banco
                                try
                                {
                                    DatabaseHandler.InsertData(connectionString, deviceData);
                                }
                                catch (Exception e)
                                {
                                    log.Error("ERROR on insert device "+ me.deviceUniqueID + " data: " + e.Message, e);
                                }

                            }

                            timeOutCounter = 0;

                            if (stream.DataAvailable == false)
                                break;
                        }
                    }
                    catch (Exception)//caso a leitura falhe
                    {
                        log.Warn("Device "+ me.deviceUniqueID + " disconnected. Connection timeout.");
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
                        log.Info("Device "+ me.deviceUniqueID + " disconnected.");
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
            User user = null;
            bool lostConnection = false, isLoggedIn = false, isClient = false;
            int i, timeOutCounter = 0;
            int timeOutTime = 10 * 60 * 1000;//10 minutos

            me.clientIP = client.Client.RemoteEndPoint.ToString().Split(':')[0];

            if (!config.bannedIPs.Contains(me.clientIP))//verifica se o IP não está banido e aceita a conexão
            {
                log.Info("Client at "+ me.clientIP + " connected on port " + client.Client.RemoteEndPoint.ToString().Split(':')[1]);
            }

            while (client.Connected)
            {
                data = null;

                // Get a stream object for reading and writing
                stream = client.GetStream();

                //verifica se o IP não foi banido durante a conexão ativa
                if (config.bannedIPs.Contains(me.clientIP))
                {
                    log.Info("Can't connect. The IP "+ me.clientIP + " is banned.");
                    ClientWrite(stream, "Can't connect. The client is banned.");
                    Thread.Sleep(5000);
                    lostConnection = true;
                }

                //se receber o sinal de desligar
                if (desligar)
                {
                    log.Info("Client "+ me.clientIP + " disconnected by server shutting down command.");
                    lostConnection = true;
                }

                //se a conexão for perdida
                if (lostConnection)
                {
                    stream.Close();// end stream
                    client.Close(); // end connection
                    break;
                }

                //se houver dados a serem lidos
                if (stream.DataAvailable)
                {
                    try
                    {
                        // Loop para receber todos os dados recebidos pelo cliente
                        while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            // traduz bytes de dados para uma string ASCII.
                            data = Encoding.ASCII.GetString(bytes, 0, i);

                            //se o cliente já estiver logado
                            if (isLoggedIn)
                            {
                                if (data.Contains("<exit>"))
                                {
                                    log.Info("User "+ user.username + " has loggedout.");

                                    log.Info("Client at "+ me.clientIP + " has exited.");
                                    lostConnection = true;
                                }
                                else
                                {
                                    ExecuteClientAction(stream,data, me, user);//executa os comandos enviados pelo cliente
                                }

                                timeOutCounter = 0;

                                //impede o lock do ciclo
                                if (stream.DataAvailable == false)
                                    break;
                            }
                            //procedimento de login
                            else if (!isLoggedIn)
                            {
                                
                                if (data.Contains("<exit>"))
                                {
                                    log.Info("Client at "+ me.clientIP + " has exited.");
                                    lostConnection = true;
                                }
                                else if (data.Contains("<Login>"))
                                {
                                    data = data.Replace("<Login>", "");

                                    string[] userdata = data.Split(';');

                                    try
                                    {
                                        user = DatabaseHandler.LoginRequest(connectionString, userdata[0]);
                                    }
                                    catch(Exception e)
                                    {
                                        log.Error("Error to login "+ me.clientIP + " - " + e.Message);
                                        user = null;
                                    }

                                    try
                                    {
                                        if (user != null && BCrypt.Net.BCrypt.Verify(userdata[1], user.password))
                                        {
                                            try
                                            {
                                                DatabaseHandler.UpdateUserLastLogin(connectionString, user.userId);

                                                ClientWrite(stream, "sucessfullLogin");

                                                log.Info("Client at "+ me.clientIP + " has started to login as " + user.username);
                                            }
                                            catch (Exception e)
                                            {
                                                log.Error("Error to login "+ user.username + "@"+ me.clientIP + " - " + e.Message);

                                                ClientWrite(stream, "wrongLogin");
                                            }
                                        }
                                        else
                                        {
                                            ClientWrite(stream, "wrongLogin");
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        log.Error("Error to login " + user.username + "@" + me.clientIP + " - " + e.Message);

                                        ClientWrite(stream, "wrongLogin");
                                    }
                                }
                                else if (data.Contains("<SendUser>"))
                                {
                                    isLoggedIn = true;

                                    user.password = null; //remove a senha do objeto antes que o memso seja enviado para o cliente

                                    //serializa o objeto User e envia para o cliente
                                    ClientWriteSerialized(stream, user);

                                    log.Info("Client at "+ me.clientIP + " has logged in as " + user.username);
                                }
                                else if (data == "shakeback")
                                {
                                    ClientWrite(stream, "SendInfos");
                                }
                                else if (data == "??\u001f?? ??\u0018??'??\u0001??\u0003??\u0003")
                                {
                                    ClientWrite(stream, "ConnectionAccepted");
                                }

                                //impede o lock do ciclo
                                if (stream.DataAvailable == false)
                                    break;
                            }
                        }

                    }
                    catch//caso a leitura falhe
                    {
                        log.Error("Client "+ me.clientIP + " disconnected. Connection timeout.");
                        lostConnection = true;
                    }
                }
                else
                {
                    if (timeOutCounter == timeOutTime)//se após 10 minutos não houver comunicação, verifica se o cliente esta online
                    {
                        log.Info("Client "+ me.clientIP + " disconnected.");
                        lostConnection = true;
                    }

                    timeOutCounter++;

                    Thread.Sleep(100);
                }

            }
            return;
        }

        private static void ExecuteClientAction(NetworkStream stream, string data, ConnectionCommandStore me, User user)
        {
            if (data.StartsWith("banip"))
            {
                config.AddBannedIp(data.Split(' ')[1].Replace("\r\n", ""));
                log.Info("User "+ user.username + "@"+ me.clientIP + " has banned the ip " + data.Split(' ')[1]);
                ClientWrite(stream, "You have banned " + data.Split(' ')[1]);
            }
            else if (data.StartsWith("unbanip"))
            {
                config.RemoveBannedIp(data.Split(' ')[1].Replace("\r\n", ""));
                log.Info("User " + user.username + "@" + me.clientIP + " has unbanned the ip " + data.Split(' ')[1]);
                ClientWrite(stream, "You have unbanned " + data.Split(' ')[1]);
            }
            else if (data.Contains("listdevices"))
            {
                if (!user.isAdmin)
                {
                    log.Warn(user.username + "@" + me.clientIP + "is trying to list users but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                int cont = 0;

                foreach (var device in DeviceConnections.ToList())
                {
                    if (device.conexao.IsAlive)
                    {
                        ClientWrite(stream, device.deviceName + Environment.NewLine);
                        cont++;
                    }
                }

                if (cont == 0)
                    ClientWrite(stream, "noDevices");
            }
            else if (data.Contains("listUsers"))
            {
                if (!user.isAdmin)
                {
                    log.Warn(user.username + "@" + me.clientIP +"is trying to list users but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    List<User> Users = DatabaseHandler.GetAllUsers(connectionString);

                    ClientWriteSerialized(stream, Users);

                    log.Info("Listed all clients to user " + user.username + "@" + me.clientIP);
                }
                catch(Exception e)
                {
                    log.Error("Fail to list all clients to user " + user.username + "@" + me.clientIP + " - " + e.Message, e);
                }
            }
            else if (data.Contains("UpdateUser"))
            {
                if (!user.isAdmin)
                {
                    log.Warn(user.username + "@" + me.clientIP + "is trying to list users but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    ClientWrite(stream, "sendUser");

                    log.Info("User " + user.username + "@" + me.clientIP + " has sent an UpdateUser request.");

                    User temp = (User) ClientReadSerilized(stream, 30000);

                    DatabaseHandler.UpdateUser(connectionString,temp);

                    ClientWrite(stream, "UserUpdated");

                    log.Info("The UpdateUser request from " + user.username + "@" + me.clientIP + " was successfullycompleted and updated the user " + temp.username + ".");
                }
                catch (Exception e)
                {
                    log.Error("Error on complete UpdateUser request from client " + user.username + "@" + me.clientIP + " - " + e.Message, e);

                    ClientWrite(stream, "FailToUpdate");
                }
            }
            else if (data.Contains("AddUser"))
            {
                if (!user.isAdmin)
                {
                    log.Warn(user.username + "@" + me.clientIP + "is trying to list users but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    ClientWrite(stream, "sendNewUser");

                    log.Info("User " + user.username + "@" + me.clientIP + " has sent an AddUser request.");

                    User temp = (User) ClientReadSerilized(stream, 30000);

                    DatabaseHandler.InsertUser(connectionString, temp);

                    ClientWrite(stream, "UserAdded");

                    log.Info("The AddUser request from " + user.username + "@" + me.clientIP +
                             " was successfullycompleted and created the user " + temp.username + ".");
                }
                catch (MySqlException e)
                {
                    if (e.Number == 1062)
                    {
                        log.Warn("Error on complete AddUser request from client " + user.username + "@" + me.clientIP + " - " + e.Number + " - " + e.Message);
                        ClientWrite(stream, "UserAlreadyExists");
                    }
                    else
                    {
                        log.Error("Error on complete AddUser request from client " + user.username + "@" + me.clientIP + " - " + e.Number + " - " + e.Message, e);
                        ClientWrite(stream, "FailToAdd");
                    }
                }
                catch (Exception e)
                {
                    log.Error("Error on complete AddUser request from client " + user.username + "@" + me.clientIP + " - " + e.Message, e);

                    ClientWrite(stream, "FailToAdd");
                }
            }
            else if (data.Contains("DeleteUser"))
            {
                if (!user.isAdmin)
                {
                    log.Warn(user.username + "@" + me.clientIP + "is trying to list users but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    log.Info("User " + user.username + "@" + me.clientIP + " has sent an DeleteUser request.");

                    DatabaseHandler.DeleteUser(connectionString, Convert.ToInt32(data.Split(";")[1]));

                    ClientWrite(stream, "UserDeleted");

                    log.Info("The DeleteUser request from " + user.username + "@" + me.clientIP + " was successfullycompleted and deleted the userId " + data.Split(";")[1] + ".");
                }
                catch (Exception e)
                {
                    log.Error("Error on complete DeleteUser request from client " + user.username + "@" + me.clientIP + " - " + e.Message, e);

                    ClientWrite(stream, "FailToDelete");
                }
            }
            else if (data.Contains("ChangePasswd"))
            {
                try
                {
                    log.Info("User " + user.username + "@" + me.clientIP + " has sent an ChangePasswd request.");

                    if (!BCrypt.Net.BCrypt.Verify(data.Split(";")[1], DatabaseHandler.GetUserById(connectionString, user.userId).password))
                    {
                        ClientWrite(stream, "InvalidOldPasswd");

                        log.Error("Error on complete ChangePasswd request from client " + user.username + "@" + me.clientIP + " - Wrong password.");

                        return;
                    }

                    DatabaseHandler.ChangeUserPasswd(connectionString, user.userId, data.Split(";")[2]);

                    ClientWrite(stream, "PasswdChanged");

                    log.Info("The ChangePasswd request from " + user.username + "@" + me.clientIP + " was successfullycompleted.");
                }
                catch (Exception e)
                {
                    log.Error("Error on complete ChangePasswd request from client " + user.username + "@" + me.clientIP + " - " + e.Message, e);

                    ClientWrite(stream, "FailToChangePasswd");
                }
            }
            else if (data.Contains("ResetPasswd"))
            {
                if (!user.isAdmin)
                {
                    log.Warn(user.username + "@" + me.clientIP + "is trying to list users but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    log.Info("User " + user.username + "@" + me.clientIP + " has sent an ResetPasswd request.");

                    DatabaseHandler.ChangeUserPasswd(connectionString, Convert.ToInt32(data.Split(";")[1]), data.Split(";")[2]);

                    ClientWrite(stream, "PasswdReseted");

                    log.Info("The ResetPasswd request from " + user.username + "@" + me.clientIP + " was successfullycompleted and reseted the passwrod for userId " + data.Split(";")[1] + ".");
                }
                catch (Exception e)
                {
                    log.Error("Error on complete ResetPasswd request from client " + user.username + "@" + me.clientIP + " - " + e.Message, e);

                    ClientWrite(stream, "FailToResetPasswd");
                }
            }
            else if (data.Contains("getWeather"))
            {
                try
                {
                    WeatherData weather = Weather.CheckWeather();

                    ClientWriteSerialized(stream, weather);

                    log.Info("Weather sent to user " + user.username + "@" + me.clientIP);
                }
                catch (Exception e)
                {
                    log.Error("Fail to sent weather to user " + user.username + "@" + me.clientIP + " - " + e.Message, e);
                }
            }
            else
            {
                ClientWrite(stream, "InvalidCommand");
                log.Warn("User " + user.username + "@" + me.clientIP + " has send: " + data);
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

        //Função que envia objetos serializados para o cliente
        private static void ClientWriteSerialized(NetworkStream stream, object sendObj)
        {
            byte[] userDataBytes;
            MemoryStream ms = new MemoryStream();
            BinaryFormatter bf1 = new BinaryFormatter();

            bf1.Serialize(ms, sendObj);
            userDataBytes = ms.ToArray();
            byte[] userDataLen = BitConverter.GetBytes((Int32)userDataBytes.Length);

            //primeiro envia o tamanho dos dados a serem enviados para que o cliente se prepare
            stream.Write(userDataLen, 0, 4);

            //envia os dados para o cliente
            stream.Write(userDataBytes, 0, userDataBytes.Length);
        }

        //Função que le um objeto serializado
        private static object ClientReadSerilized(NetworkStream stream, int timeout = -1)
        {
            byte[] readMsgLen = new byte[4];
            int dataLen;
            byte[] readMsgData;
            BinaryFormatter bf1 = new BinaryFormatter();
            MemoryStream ms;

            //seta o timeout de leitura dos dados para 30 segundos
            stream.ReadTimeout = timeout;

            //le o tamanho dos dados que serão recebidos
            stream.Read(readMsgLen, 0, 4);
            dataLen = BitConverter.ToInt32(readMsgLen, 0);
            readMsgData = new byte[dataLen];

            //le os dados que estão sendo recebidos
            stream.Read(readMsgData, 0, dataLen);

            ms = new MemoryStream(readMsgData);
            ms.Position = 0;

            //converte os dados recebidos para um objeto
            object objeto = bf1.Deserialize(ms);

            //seta o timeout para o valor padrão (infinito)
            stream.ReadTimeout = -1;

            return objeto;
        }

        //Função que atualiza a previsão do tempo
        static async Task RefreshForecast()
        {
            log.Info("Updating forecast informations");

            try
            {
                forecast = Weather.CheckForecast();

                log.Info("Successfully updated forecasts for "+ forecast.Location_Name + ","+ forecast.Location_Country + " ("+ forecast.Location_Latitude + ";"+ forecast.Location_Longitude + ") ");
            }
            catch (Exception e)
            {
                log.Error("Fail to update forecast informations for "+ config.cityName + ","+ config.countryId + " ==> " + e.Message);
            }
        }

        //Função que liga a irrigação
        static async Task TurnOnIrrigation(int time)
        {
            log.Info("Irrigation turned on.");

            Thread.Sleep(time * 1000);

            log.Info("Irrigation turned off.");
        }

        //Garbage colector que limpa a lista de clientes e devices conectados
        private static void ClearConnectionList()
        {
            while (desligar == false)
            {

                int DevicesInMemory = DeviceConnections.Count();
                int ClientsInMemory = ClientConnections.Count();
                ConnectionCommandStore temp;

                //remove as conexões de devices encerradas da lista
                for (int i = 0; i < DevicesInMemory; i++)
                {
                    if (!DeviceConnections.TryTake(out temp))
                    {
                        log.Error("Não foi possivel limpar a fila de dispositivos. ("+ DevicesInMemory + ")");
                    }
                    else
                    {
                        if (temp.conexao.IsAlive)
                        {
                            DeviceConnections.Add(temp);
                        }
                        else
                        {
                            temp = null;
                        }
                    }
                }

                //remove as conexões de clientes encerradas da lista
                for (int i = 0; i < ClientsInMemory; i++)
                {
                    if (!ClientConnections.TryTake(out temp))
                    {
                        log.Error("Não foi possivel limpar a fila de clientes. ("+ ClientsInMemory + ")");
                    }
                    else
                    {
                        if (temp.conexao.IsAlive)
                        {
                            ClientConnections.Add(temp);
                        }
                        else
                        {
                            temp = null;
                        }
                    }
                }

                //ConsoleWrite("Limpou -- Devices Ativos: {0} -- Devices Removidos: {1}", true, DeviceConnections.Count, DevicesInMemory - DeviceConnections.Count);

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
