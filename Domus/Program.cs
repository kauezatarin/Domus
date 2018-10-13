using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private static bool _desligar = false;//kill switch utilizado para desligar o servidor
        private static BlockingCollection<ConnectionCommandStore> _deviceConnections = new BlockingCollection<ConnectionCommandStore>(new ConcurrentQueue<ConnectionCommandStore>());
        private static BlockingCollection<ConnectionCommandStore> _clientConnections = new BlockingCollection<ConnectionCommandStore>(new ConcurrentQueue<ConnectionCommandStore>());
        private static BlockingCollection<Service> _services = new BlockingCollection<Service>(new ConcurrentQueue<Service>());
        private static ConfigHandler _config = new ConfigHandler();
        private static IrrigationConfig _irrigationConfig;
        private static CisternConfig _cisternConfig;
        private static string _connectionString;
        private static WeatherHandler _weather;
        private static Forecast _forecast;
        private static TaskScheduler _scheduler;//scheduler.scheduleTask(DateTime.Now + new TimeSpan(0, 0, 10), async ()=> {Teste();}, "Hourly");
        private static ILog _log;
        private static TcpListener _deviceServer = null;
        private static TcpListener _clientServer = null;
        private static Thread _deviceListener = null;
        private static Thread _clientListener = null;
        private static Thread _decisionMaker = null;
        private static bool _canRunIrrigation = true;

        static void Main(string[] args)
        {
            Thread connectionCleaner = null;

            AppDomain.CurrentDomain.ProcessExit += OnSystemShutdown;
            
            Console.Title = "Domus - " + Assembly.GetExecutingAssembly().GetName().Version;
            Console.CancelKeyPress += new ConsoleCancelEventHandler(ConsoleCancelKeyPress);

            _connectionString = DatabaseHandler.CreateConnectionString(_config.DatabaseIp, _config.DatabasePort, _config.DatabaseName, _config.DatabaseUser, _config.DatabasePassword);
            _weather = new WeatherHandler(_config.CityName, _config.CountryId, _config.WeatherApiKey);// adicionar os parametros na configuração

            #region LogStarter

            XmlDocument log4NetConfig = new XmlDocument();

            log4NetConfig.Load(File.OpenRead("log4net.config"));

            var repo = LogManager.CreateRepository(Assembly.GetEntryAssembly(),
                typeof(log4net.Repository.Hierarchy.Hierarchy));

            XmlConfigurator.Configure(repo, log4NetConfig["log4net"]);

            _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            #endregion

            _scheduler = new TaskSchedulerHandler(_log);

            try
            {
                Console.Clear();
                _log.Info("Domus Server - Version: " + Assembly.GetExecutingAssembly().GetName().Version);
                _log.Info("To Exit press Ctrl + C.");

                //cria a conexão
                _log.Info("Creating listeners");
                try
                {
                    _deviceServer = Connect(_config.DeviceListeningPort);
                    _clientServer = Connect(_config.ClientListeningPort);
                }
                catch (Exception e)//se falhar sai do programa
                {
                    _log.Fatal("Fail to create listeners.", e);

                    Console.Read();
                    return;
                }

                bool databasereached = false;

                while (databasereached == false)
                {
                    //verifica se o banco de dados está ativo
                    try
                    {
                        _log.Info("Testing database connection on " + _config.DatabaseIp + ":" +_config.DatabasePort);
                        DatabaseHandler.TestConnection(_connectionString);
                        databasereached = true;
                        _log.Info("Database Connection success");
                    }
                    catch (MySqlException e)
                    {
                        _log.Error("Database connection Failure. " + e.Number + " - " + e.Message);
                        _log.Info("Retrying in 30 seconds...");
                        Thread.Sleep(30000);
                    }
                }

                _log.Info("Loading systems configuration");

                LoadConfigurations();

                if (_irrigationConfig.UseForecast)
                {
                    //Tenta resgatar a previsão do tempo
                    _log.Info("Acquiring forecast informations");

                    try
                    {
                        _forecast = _weather.CheckForecast();

                        _log.Info("Successfully acquired forecasts for " + _forecast.LocationName + "," +
                                  _forecast.LocationCountry + " (" + _forecast.LocationLatitude + ";" + _forecast.LocationLongitude + ") ");
                    }
                    catch (Exception e)
                    {
                        _log.Warn("Fail to acquire forecast informations for " + _config.CityName + "," + _config.CountryId + " ==>" + e.Message);
                    }
                }
                else
                {
                    _log.Warn("Forecast system is disabled. This may reduce the irrigation system turn on decision.");
                }

                try
                {
                    _log.Info("Scheduling server tasks");

                    DateTime temp = DateTime.Now;
                    temp = temp.Subtract(new TimeSpan(temp.Hour, temp.Minute, temp.Second));//turns a scheduler time to 00:00:00
                    temp = temp.AddDays(1);

                    _scheduler.ScheduleTask(temp, RefreshForecast, "Daily");

                    _log.Info("Forecast updater scheduled to run daily at 00:00:00");

                    ScheduleIrrigationTaskts(DatabaseHandler.GetAllIrrigationSchedules(_connectionString));

                    _log.Info("Tasks scheduled");
                }
                catch (Exception e)
                {
                    _log.Error("Fail to schedule server tasks -> " + e.Message, e);
                }

                connectionCleaner = new Thread(() => ClearConnectionList());
                connectionCleaner.IsBackground = true;
                connectionCleaner.Name = "Connection Cleaner";
                connectionCleaner.Start();

                _log.Info("Starting decision maker thread");
                _decisionMaker = new Thread(() => DecisionMakerThread());
                _decisionMaker.IsBackground = true;
                _decisionMaker.Name = "Decision Maker";
                _decisionMaker.Start();
                _log.Info("Decision maker thread started");

                // starts the listening loop. 
                _log.Info("Starting device listener on port " + _config.DeviceListeningPort);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                _deviceListener = new Thread(() => DeviceListenerAsync(_deviceServer));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                _deviceListener.Name = "Device Listener";
                _deviceListener.Start();

                
                _log.Info("Starting client listener on port " + _config.ClientListeningPort);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                _clientListener = new Thread(() => ClientListenerAsync(_clientServer));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                _clientListener.Name = "Client Listener";
                _clientListener.Start();
                
                WaitKillCommand();
            }
            catch (SocketException e)
            {
                _log.Fatal("SocketException: " + e.Message, e);
            }

            return;
        }

        //metodo que carrega as configurações dos sistemas na inicialização
        static void LoadConfigurations()
        {
            try
            {
                _irrigationConfig = DatabaseHandler.GetIrrigationConfig(_connectionString);

                _log.Info("Irrigation configurations loaded.");
            }
            catch (MySqlException e)
            {
                _log.Warn("Could not load the irrigation configurations on database, trying to create new configurations.");

                IrrigationConfig temp = new IrrigationConfig(1, 80, 10, 33, true);

                if (DatabaseHandler.InsertIrrigationConfig(_connectionString, temp) == 0)
                {
                    _log.Error("Error on create the irrigation configurations. - " + e.Message, e);
                    _log.Error("Using default configurations.");
                    _irrigationConfig = temp;
                }
                else
                {
                    _log.Info("The configurations to the irrigation system were created.");

                    _irrigationConfig = DatabaseHandler.GetIrrigationConfig(_connectionString);

                    _log.Info("Irrigation configurations loaded.");
                }
            }

            try
            {
                _cisternConfig = DatabaseHandler.GetCisternConfig(_connectionString);

                _log.Info("Cistern configurations loaded.");
            }
            catch (MySqlException e)
            {
                _log.Warn("Could not load the cistern configurations on database, trying to create new configurations.");

                CisternConfig temp = new CisternConfig(1, 10, 10, 0);

                if (DatabaseHandler.InsertCisternConfig(_connectionString, temp) == 0)
                {
                    _log.Error("Error on create the cistern configurations. - " + e.Message, e);
                    _log.Error("Using default configurations.");
                    _cisternConfig = temp;
                }
                else
                {

                    _log.Info("The configurations to the cistern system were created.");

                    _cisternConfig = DatabaseHandler.GetCisternConfig(_connectionString);

                    _log.Info("Cistern configurations loaded.");
                }
            }

            try
            {
                List<Service> services = DatabaseHandler.GetAllServices(_connectionString);

                foreach (Service service in services)
                {
                    _services.Add(service);

                    _log.Info("The link to the service " + service.ServiceName + " was loaded.");
                }

                _log.Info("Services links loaded.");
            }
            catch (MySqlException e)
            {
                _log.Fatal("Services links could not be loaded." + e.Message, e);
            }
        }

        //rotina executada quando o servidor está sendo encerrado
        static void StopRoutine()
        {
            try
            {
                _log.Info("Disconnecting all clients and devices...");
                _desligar = true;

                // Stop listening for new clients.
                _log.Info("Stopping listeners...");

                _deviceServer.Stop();
                _clientServer.Stop();

                JoinAllConnections();//Wait for all clients and devices to disconnect

                if (_deviceListener != null && _deviceListener.IsAlive)
                    _deviceListener.Join();
                if (_clientListener != null && _clientListener.IsAlive)
                    _clientListener.Join();

                _log.Info("Stopped");

                _log.Info("Cleaning scheduled tasks...");

                _scheduler.DeleteAllTasks();

                while (_scheduler.TasksCount() != 0)
                {
                    Thread.Sleep(100);
                }

                _log.Info("Cleared");

                _log.Info("Server Stoped.");
            }
            catch (Exception e)
            {
                _log.Error("Error on server close routine - " + e.Message, e);
            }
        }

        //metodo chamado quando o servidor recebe um SIGterm
        private static void OnSystemShutdown(object sender, EventArgs e)
        {
                StopRoutine();   
        }

        //função que cria a conexão com a rede
        private static TcpListener Connect(Int32 port, bool intranet = false)
        {

            IPAddress localAddr = IPAddress.Parse(GetLocalIpAddress());//iplocal
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
                DateTime temp = schedule.ScheduleTime;

                temp = temp.AddDays(DateTime.Now.Day - temp.Day);
                temp = temp.AddMonths(DateTime.Now.Month - temp.Month);
                temp = temp.AddYears(DateTime.Now.Year - temp.Year);

                schedule.ScheduleTime = temp;

                if (schedule.Active)
                {
                    if (schedule.Sunday)
                    {
                        temp = _scheduler.GetNextWeekday(schedule.ScheduleTime, DayOfWeek.Sunday);

                        _scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.RunFor), "Weekly");

                        _log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }

                    if (schedule.Monday)
                    {
                        temp = _scheduler.GetNextWeekday(schedule.ScheduleTime, DayOfWeek.Monday);

                        _scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.RunFor), "Weekly");

                        _log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }

                    if (schedule.Tuesday)
                    {
                        temp = _scheduler.GetNextWeekday(schedule.ScheduleTime, DayOfWeek.Tuesday);

                        _scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.RunFor), "Weekly");

                        _log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }

                    if (schedule.Wednesday)
                    {
                        temp = _scheduler.GetNextWeekday(schedule.ScheduleTime, DayOfWeek.Wednesday);

                        _scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.RunFor), "Weekly");

                        _log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }

                    if (schedule.Thursday)
                    {
                        temp = _scheduler.GetNextWeekday(schedule.ScheduleTime, DayOfWeek.Thursday);

                        _scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.RunFor), "Weekly");

                        _log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }

                    if (schedule.Friday)
                    {
                        temp = _scheduler.GetNextWeekday(schedule.ScheduleTime, DayOfWeek.Friday);

                        _scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.RunFor), "Weekly");

                        _log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }

                    if (schedule.Saturday)
                    {
                        temp = _scheduler.GetNextWeekday(schedule.ScheduleTime, DayOfWeek.Saturday);

                        _scheduler.ScheduleTask(temp, () => TurnOnIrrigation(schedule.RunFor), "Weekly");

                        _log.Info("Irrigation scheduled to " + temp.ToString(new CultureInfo("pt-BR")));
                    }
                }
            }
        }

        //função que impede que o servidor morra antes de receber o comando de desligamento
        private static void WaitKillCommand()
        {
            while (!_desligar)
            {
                Thread.Sleep(100);
            }
        }

        //função que aguarda o comando de encerramento do servidor
        private static void ConsoleCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (e.SpecialKey == ConsoleSpecialKey.ControlC)
            {
                _desligar = true;
                e.Cancel = true;
            }
        }

        //função de tomada de decisões
        private static void DecisionMakerThread()
        {
            bool canRunIrrigation = true;
            Data tempData;
            Service tempService;
            bool forecastNoRain = true;
            DateTime lastForecastAnalyzedTime = DateTime.Now.AddDays(-1);

            while (_desligar == false)
            {
                try
                {
                    canRunIrrigation = true;

                    //valida a previsão do tempo a cada 24h
                    if (_irrigationConfig.UseForecast && _forecast != null && (DateTime.Now - lastForecastAnalyzedTime).TotalDays >= 1)
                    {
                        List<ForecastData> forecastDatas = _forecast.Forecasts;
                        lastForecastAnalyzedTime = DateTime.Today.AddMinutes(5);

                        for (int i = 0; i < 24; i++)
                        {
                            if (forecastDatas[i].PrecipitationType != "none")
                            {
                                canRunIrrigation = false;
                                forecastNoRain = false;
                            }
                        }
                    }
                    //caso ainda não tenha dado 24h apenas replica a ultima decisão
                    else
                    {
                        if (forecastNoRain == false)
                        {
                            canRunIrrigation = false;
                        }

                    }

                    //descobre o dispositivo onde o sensor de chuva está atrelado
                    tempService = _services.FirstOrDefault(Service => Service.ServiceName == "cistern.RainSensor");
                    if (tempService.DeviceId.ToLower() != "null")
                    {
                        try
                        {
                            tempData = DatabaseHandler.GetLastRainData(_connectionString, tempService.DeviceId, "data" + (tempService.DevicePortNumber + 1).ToString("0"));

                            //caso seja encontrado um registro de chuva
                            if (tempData != null)
                            {
                                //se estiver chovendo não liga a irrigação
                                if (Data.GetData(tempData, "Data" + (tempService.DevicePortNumber + 1).ToString("0")) == 1.ToString())
                                {
                                    canRunIrrigation = false;
                                }

                                //se a ultima chuva registrada for a mais de 7 dias e a previsão do tempo não registrar chuva, ignora as demais vaiáveis e liga a irrigação.
                                else if ((DateTime.Now - tempData.CreatedAt).TotalDays > 7 && forecastNoRain)
                                {
                                    canRunIrrigation = true;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error("Error on compute rain sensor data to decision. " + e.Message, e);
                        }
                    }

                    //descobre o dispositivo onde o sensor de humidade do solo está conectado
                    tempService = _services.FirstOrDefault(Service => Service.ServiceName == "irrigation.SoilHumidity");
                    if (tempService.DeviceId.ToLower() != "null")
                    {
                        try
                        {
                            tempData = DatabaseHandler.GetLastData(_connectionString, tempService.DeviceId);

                            if (tempData != null)
                            {
                                //se a humidade do solo estiver muito alta não liga a irrigação
                                if (Convert.ToDouble(Data.GetData(tempData,"Data" + (tempService.DevicePortNumber + 1).ToString("0")), CultureInfo.InvariantCulture) >= _irrigationConfig.MaxSoilHumidity)
                                {
                                    canRunIrrigation = false;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error("Error on compute soil humidity data to decision. " + e.Message, e);
                        }
                    }

                    //descobre o dispositivo onde o sensor de temperatura está conectado
                    tempService = _services.FirstOrDefault(Service => Service.ServiceName == "irrigation.Temperature");
                    if (tempService.DeviceId.ToLower() != "null")
                    {
                        try
                        {
                            tempData = DatabaseHandler.GetLastData(_connectionString, tempService.DeviceId);

                            if (tempData != null)
                            {
                                //se a temperatura do àr estiver muito alta não liga a irrigação
                                if (Convert.ToDouble(Data.GetData(tempData,"Data" + (tempService.DevicePortNumber + 1).ToString("0")), CultureInfo.InvariantCulture) >=_irrigationConfig.MaxAirTemperature)
                                {
                                    canRunIrrigation = false;
                                }
                                //se a temperatura do àr estiver muito baixa não liga a irrigação
                                else if (Convert.ToDouble(Data.GetData(tempData,"Data" + (tempService.DevicePortNumber + 1).ToString("0")), CultureInfo.InvariantCulture) <=_irrigationConfig.MinAirTemperature)
                                {
                                    canRunIrrigation = false;
                                }
                            }
                        }
                        catch(Exception e)
                        {
                            _log.Error("Error on compute temperature data to decision. " + e.Message, e);
                        }
                    }

                    //se alterar o valor do gatilho
                    if (_canRunIrrigation != canRunIrrigation)
                    {
                        _canRunIrrigation = canRunIrrigation;

                        if (canRunIrrigation)
                        {
                            _log.Info("The environment conditions are perfect to tur on the irrigation system. Enabling it...");
                        }
                        else
                        {
                            _log.Info("The environment conditions aren't perfect to tur on the irrigation system. Disabling it...");
                        }

                    }
                }
                catch (Exception e)
                {
                    _log.Error("Error on make decisions. " + e.Message, e);
                }

                Thread.Sleep(10000);
            }
        }

        //thread responsavel por aguardar conexões de devices
        private static async Task DeviceListenerAsync(TcpListener deviceServer)
        {

            // Start listening for client requests.
            deviceServer.Start();
            Thread newDevice;
            ConnectionCommandStore temp;

            _log.Info("Waiting for device connection... ");

            while (_desligar == false)
            {
                // Perform a blocking call to accept requests. 
                try
                {
                    TcpClient device = null;
                    device = await deviceServer.AcceptTcpClientAsync();

                    if (_config.MaxDevicesConnections > _deviceConnections.Count || _config.MaxDevicesConnections == -1)
                    {
                        temp = new ConnectionCommandStore();

                        newDevice = new Thread(() => DeviceThread(device, temp)); //cria uma nova thread para tratar o device
                        newDevice.IsBackground = true;

                        temp.Conexao = newDevice;

                        _deviceConnections.Add(temp);//adiciona para a lista de conexões

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

            _log.Info("Waiting for client connection... ");

            while (_desligar == false)
            {
                // Perform a blocking call to accept requests. 
                // You could also user server.AcceptSocket() here.
                try
                {
                    TcpClient client = await clientServer.AcceptTcpClientAsync();

                    if (_config.MaxClientsConnections > _clientConnections.Count || _config.MaxClientsConnections == -1)
                    {
                        temp = new ConnectionCommandStore();

                        newClient = new Thread(() => ClientThread(client, temp)); //cria uma nova thread para tratar o cliente
                        newClient.IsBackground = true;

                        temp.Conexao = newClient;

                        _clientConnections.Add(temp);//adiciona para a lista de conexões

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

            me.ClientIp = device.Client.RemoteEndPoint.ToString().Split(':')[0];

            if (!_config.BannedIPs.Contains(me.ClientIp))
            {
                _log.Info("Device at " + me.ClientIp +
                             " connected on port " + device.Client.RemoteEndPoint.ToString().Split(':')[1]);
            }

            while (device.Connected)
            {
                data = null;

                // Get a stream object for reading and writing
                stream = device.GetStream();

                if (_config.BannedIPs.Contains(me.ClientIp))//if device is banned
                {
                    ClientWrite(stream, "banned");
                    lostConnection = true;
                }

                if (_desligar)//se receber o sinal de desligar
                {
                    ClientWrite(stream, "shutdown");

                    if (me.DeviceUniqueId != null)
                        _log.Info("Device "+ me.DeviceUniqueId + " disconnected by server shutting down command.");
                    else
                        _log.Info("Unknown Device from IP " + me.ClientIp + " disconnected by server shutting down command.");

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
                                ConnectionCommandStore temp = _deviceConnections.FirstOrDefault(connectionCommandStore =>
                                        connectionCommandStore.DeviceUniqueId == data.Split(';')[2]);

                                if(temp != null)//verify if the device already has an connection on the list.
                                {
                                    lostConnection = true; //drop the client

                                    ClientWrite(stream, "uidit");//send UID is taken to device
                                    _log.Info("Device at "+ me.ClientIp + " is tying to connect using an UID that is already taken.");
                                }
                                else if (!DatabaseHandler.IsAuthenticDevice(_connectionString, data.Split(';')[2]))//verify if the device is listed at the database
                                {
                                    lostConnection = true; //drop the client

                                    ClientWrite(stream, "uidnf");//send UID not found to device
                                    _log.Info("Device at "+ me.ClientIp + " is tying to connect using an UID that is not registered.");
                                }
                                else//if it has not, then accepts the connection.
                                {
                                    Device tempDevice = DatabaseHandler.GetDeviceByUid(_connectionString, data.Split(';')[2]); //get device infos from database

                                    me.DeviceName = tempDevice.DeviceName;
                                    me.DeviceType = tempDevice.DeviceType;
                                    me.DataDelay = (Convert.ToInt32(data.Split(';')[1]) * 10) + 100;//gets delay time and adds 10 seconds
                                    me.DeviceUniqueId = data.Split(';')[2];

                                    if (me.DataDelay < _config.MinDataDelay && me.DeviceType !=2)//if delay < minDataDelay segundos (30 + 10)
                                    {
                                        me.DataDelay = _config.MinDataDelay;//sets the delay to 30+10 segundos
                                        ClientWrite(stream, "changeTimer " + (_config.MinDataDelay - 100) / 10);
                                    }
                                    else if (me.DeviceType == 2)//case it's a plug device MODIFICAR
                                    {
                                        me.DataDelay = 120;//sets the delay to 2+10 segundos
                                        ClientWrite(stream, "changeTimer " + (120 - 100) / 10);
                                    }

                                    getingDeviceInfos = false;

                                    tempDevice = null; //free var from memory

                                    _log.Info("Device "+ me.ClientIp + " was identified as '" + me.DeviceUniqueId + "'");
                                }
                            }
                            else if (data == "??\u001f?? ??\u0018??'??\u0001??\u0003??\u0003")//se um cliente tentar se conectar na porta de devices
                            {
                                _log.Error("Connection denied to client "+ me.ClientIp + ". Wrong connection port.");
                                ClientWrite(stream, "WrongPort");
                                Thread.Sleep(5000);
                                lostConnection = true;
                            }
                            else if (data == "shakeback")
                            {
                                _log.Info("Device "+ me.ClientIp + " has shaked back.");
                                getingDeviceInfos = true;
                                ClientWrite(stream, "SendInfos");//get device infos from device
                            }
                            else if (data != "imhr")
                            {
                                Data deviceData = new Data(0, me.DeviceUniqueId);
                                string[] datas = data.Split(';');

                                for (i = 0; i < datas.Length; i++)
                                {
                                    if (i == 0)
                                        deviceData.Data1 = data.Split(';')[i];
                                    else if (i == 1)
                                        deviceData.Data2 = data.Split(';')[i];
                                    else if (i == 2)
                                        deviceData.Data3 = data.Split(';')[i];
                                    else if (i == 3)
                                        deviceData.Data4 = data.Split(';')[i];
                                }

                                _log.Info("Device '"+ me.DeviceUniqueId + "' has sent: " +  data);

                                //inserir dados no banco
                                try
                                {
                                    DatabaseHandler.InsertData(_connectionString, deviceData);
                                }
                                catch (Exception e)
                                {
                                    _log.Error("ERROR on insert device "+ me.DeviceUniqueId + " data: " + e.Message, e);
                                }

                            }

                            timeOutCounter = 0;

                            if (stream.DataAvailable == false)
                                break;
                        }
                    }
                    catch (Exception)//caso a leitura falhe
                    {
                        _log.Warn("Device "+ me.DeviceUniqueId + " disconnected. Connection timeout.");
                        lostConnection = true;
                    }
                }
                else
                {
                    if (timeOutCounter == me.DataDelay)//se após x segundos não houver comunicação, verifica se o cliente esta online
                    {
                        if (getingDeviceInfos)//if connection if timming out and the defice isn't indentificated yet
                            ClientWrite(stream, "SendInfos");//get device infos from device

                        ClientWrite(stream, "ayt");//envia um are you there
                    }
                    else if (timeOutCounter > me.DataDelay + 50)//espera 5 segundos para resposta
                    {
                        _log.Info("Device "+ me.DeviceUniqueId + " disconnected.");
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
            bool lostConnection = false, isLoggedIn = false;
            int i, timeOutCounter = 0;
            int timeOutTime = 10 * 60 * 1000;//10 minutos

            me.ClientIp = client.Client.RemoteEndPoint.ToString().Split(':')[0];

            if (!_config.BannedIPs.Contains(me.ClientIp))//verifica se o IP não está banido e aceita a conexão
            {
                _log.Info("Client at "+ me.ClientIp + " connected on port " + client.Client.RemoteEndPoint.ToString().Split(':')[1]);
            }

            while (client.Connected)
            {
                data = null;

                // Get a stream object for reading and writing
                stream = client.GetStream();

                //verifica se o IP não foi banido durante a conexão ativa
                if (_config.BannedIPs.Contains(me.ClientIp))
                {
                    _log.Info("Can't connect. The IP "+ me.ClientIp + " is banned.");
                    ClientWrite(stream, "Can't connect. The client is banned.");
                    Thread.Sleep(5000);
                    lostConnection = true;
                }

                //se receber o sinal de desligar
                if (_desligar)
                {
                    _log.Info("Client "+ me.ClientIp + " disconnected by server shutting down command.");
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
                                    _log.Info("User "+ user.Username + " has loggedout.");

                                    _log.Info("Client at "+ me.ClientIp + " has exited.");
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
                                    _log.Info("Client at "+ me.ClientIp + " has exited.");
                                    lostConnection = true;
                                }
                                else if (data.Contains("<Login>"))
                                {
                                    data = data.Replace("<Login>", "");

                                    string[] userdata = data.Split(';');

                                    try
                                    {
                                        user = DatabaseHandler.LoginRequest(_connectionString, userdata[0]);
                                    }
                                    catch(Exception e)
                                    {
                                        _log.Error("Error to login "+ me.ClientIp + " - " + e.Message);
                                        user = null;
                                    }

                                    try
                                    {
                                        if (user != null && BCrypt.Net.BCrypt.Verify(userdata[1], user.Password))
                                        {
                                            try
                                            {
                                                DatabaseHandler.UpdateUserLastLogin(_connectionString, user.UserId);

                                                ClientWrite(stream, "sucessfullLogin");

                                                _log.Info("Client at "+ me.ClientIp + " has started to login as " + user.Username);
                                            }
                                            catch (Exception e)
                                            {
                                                _log.Error("Error to login "+ user.Username + "@"+ me.ClientIp + " - " + e.Message);

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
                                        _log.Error("Error to login " + user.Username + "@" + me.ClientIp + " - " + e.Message);

                                        ClientWrite(stream, "wrongLogin");
                                    }
                                }
                                else if (data.Contains("<SendUser>"))
                                {
                                    isLoggedIn = true;

                                    user.Password = null; //remove a senha do objeto antes que o memso seja enviado para o cliente

                                    //serializa o objeto User e envia para o cliente
                                    ClientWriteSerialized(stream, user);

                                    _log.Info("Client at "+ me.ClientIp + " has logged in as " + user.Username);
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
                        _log.Error("Client "+ me.ClientIp + " disconnected. Connection timeout.");
                        lostConnection = true;
                    }
                }
                else
                {
                    if (timeOutCounter == timeOutTime)//se após 10 minutos não houver comunicação, verifica se o cliente esta online
                    {
                        _log.Info("Client "+ me.ClientIp + " disconnected.");
                        lostConnection = true;
                    }

                    timeOutCounter++;

                    Thread.Sleep(100);
                }

            }
            return;
        }

        //Metodo que interpreta e executa os comandos recebidos do cliente
        private static void ExecuteClientAction(NetworkStream stream, string data, ConnectionCommandStore me, User user)
        {
            if (data.Contains("ListDevices"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to list devices but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    List<Device> devices = DatabaseHandler.GetAllDevices(_connectionString);

                    ClientWriteSerialized(stream, devices);

                    _log.Info("Listed all devices to user " + user.Username + "@" + me.ClientIp);
                }
                catch (Exception e)
                {
                    _log.Error("Fail to list all devices to user " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);
                }
            }
            else if (data.Contains("AddDevice"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to add a device but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    ClientWrite(stream, "sendNewDevice");

                    _log.Info("Device " + user.Username + "@" + me.ClientIp + " has sent an AddDevice request.");

                    Device temp = (Device)ClientReadSerilized(stream, 30000);

                    DatabaseHandler.InsertDevice(_connectionString, temp);

                    ClientWrite(stream, "DeviceAdded");

                    _log.Info("The AddDevice request from " + user.Username + "@" + me.ClientIp +
                             " was successfully completed and created the device " + temp.DeviceId + ".");
                }
                catch (MySqlException e)
                {
                    if (e.Number == 1062)
                    {
                        _log.Warn("Error on complete AddDevice request from client " + user.Username + "@" + me.ClientIp + " - " + e.Number + " - " + e.Message);
                        ClientWrite(stream, "DeviceAlreadyExists");
                    }
                    else
                    {
                        _log.Error("Error on complete AddDevice request from client " + user.Username + "@" + me.ClientIp + " - " + e.Number + " - " + e.Message, e);
                        ClientWrite(stream, "FailToAdd");
                    }
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete AddDevice request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToAdd");
                }
            }
            else if (data.Contains("UpdateDevice"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to update a device but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    ClientWrite(stream, "SendDevice");

                    _log.Info("User " + user.Username + "@" + me.ClientIp + " has sent an UpdateDevice request.");

                    Device temp = (Device)ClientReadSerilized(stream, 30000);

                    DatabaseHandler.UpdateDevice(_connectionString, temp);

                    ClientWrite(stream, "DeviceUpdated");

                    _log.Info("The UpdateDevice request from " + user.Username + "@" + me.ClientIp + " was successfully completed and updated the device " + temp.DeviceName + ".");
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete UpdateUser request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToUpdate");
                }
            }
            else if (data.Contains("DeleteDevice"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to delete an user but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    _log.Info("User " + user.Username + "@" + me.ClientIp + " has sent an DeleteDevice request.");

                    DatabaseHandler.DeleteDevice(_connectionString, data.Split(";")[1]);

                    ClientWrite(stream, "DeviceDeleted");

                    _log.Info("The DeleteDevice request from " + user.Username + "@" + me.ClientIp + " was successfully completed and deleted the deviceId " + data.Split(";")[1] + ".");
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete DeleteDevice request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToDelete");
                }
            }
            else if (data.Contains("ListUsers"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp +"is trying to list users but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    List<User> users = DatabaseHandler.GetAllUsers(_connectionString);

                    ClientWriteSerialized(stream, users);

                    _log.Info("Listed all clients to user " + user.Username + "@" + me.ClientIp);
                }
                catch(Exception e)
                {
                    _log.Error("Fail to list all clients to user " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);
                }
            }
            else if (data.Contains("UpdateUser"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to update an user but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    ClientWrite(stream, "sendUser");

                    _log.Info("User " + user.Username + "@" + me.ClientIp + " has sent an UpdateUser request.");

                    User temp = (User) ClientReadSerilized(stream, 30000);

                    DatabaseHandler.UpdateUser(_connectionString,temp);

                    ClientWrite(stream, "UserUpdated");

                    _log.Info("The UpdateUser request from " + user.Username + "@" + me.ClientIp + " was successfully completed and updated the user " + temp.Username + ".");
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete UpdateUser request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToUpdate");
                }
            }
            else if (data.Contains("AddUser"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to add an user but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    ClientWrite(stream, "sendNewUser");

                    _log.Info("User " + user.Username + "@" + me.ClientIp + " has sent an AddUser request.");

                    User temp = (User) ClientReadSerilized(stream, 30000);

                    DatabaseHandler.InsertUser(_connectionString, temp);

                    ClientWrite(stream, "UserAdded");

                    _log.Info("The AddUser request from " + user.Username + "@" + me.ClientIp +
                             " was successfullycompleted and created the user " + temp.Username + ".");
                }
                catch (MySqlException e)
                {
                    if (e.Number == 1062)
                    {
                        _log.Warn("Error on complete AddUser request from client " + user.Username + "@" + me.ClientIp + " - " + e.Number + " - " + e.Message);
                        ClientWrite(stream, "UserAlreadyExists");
                    }
                    else
                    {
                        _log.Error("Error on complete AddUser request from client " + user.Username + "@" + me.ClientIp + " - " + e.Number + " - " + e.Message, e);
                        ClientWrite(stream, "FailToAdd");
                    }
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete AddUser request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToAdd");
                }
            }
            else if (data.Contains("DeleteUser"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to delete an user but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    _log.Info("User " + user.Username + "@" + me.ClientIp + " has sent an DeleteUser request.");

                    DatabaseHandler.DeleteUser(_connectionString, Convert.ToInt32(data.Split(";")[1]));

                    ClientWrite(stream, "UserDeleted");

                    _log.Info("The DeleteUser request from " + user.Username + "@" + me.ClientIp + " was successfully completed and deleted the userId " + data.Split(";")[1] + ".");
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete DeleteUser request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToDelete");
                }
            }
            else if (data.Contains("ChangePasswd"))
            {
                try
                {
                    _log.Info("User " + user.Username + "@" + me.ClientIp + " has sent an ChangePasswd request.");

                    if (!BCrypt.Net.BCrypt.Verify(data.Split(";")[1], DatabaseHandler.GetUserById(_connectionString, user.UserId).Password))
                    {
                        ClientWrite(stream, "InvalidOldPasswd");

                        _log.Error("Error on complete ChangePasswd request from client " + user.Username + "@" + me.ClientIp + " - Wrong password.");

                        return;
                    }

                    DatabaseHandler.ChangeUserPasswd(_connectionString, user.UserId, data.Split(";")[2]);

                    ClientWrite(stream, "PasswdChanged");

                    _log.Info("The ChangePasswd request from " + user.Username + "@" + me.ClientIp + " was successfully completed.");
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete ChangePasswd request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToChangePasswd");
                }
            }
            else if (data.Contains("ResetPasswd"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to reset the a user's password but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    _log.Info("User " + user.Username + "@" + me.ClientIp + " has sent an ResetPasswd request.");

                    DatabaseHandler.ChangeUserPasswd(_connectionString, Convert.ToInt32(data.Split(";")[1]), data.Split(";")[2]);

                    ClientWrite(stream, "PasswdReseted");

                    _log.Info("The ResetPasswd request from " + user.Username + "@" + me.ClientIp + " was successfully completed and reseted the passwrod for userId " + data.Split(";")[1] + ".");
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete ResetPasswd request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToResetPasswd");
                }
            }
            else if (data.Contains("GetWeather"))
            {
                if (_irrigationConfig.UseForecast)
                {
                    try
                    {
                        WeatherData weather = _weather.CheckWeather();

                        ClientWriteSerialized(stream, weather);

                        _log.Info("Weather sent to user " + user.Username + "@" + me.ClientIp);
                    }
                    catch (WebException e)
                    {
                        _log.Warn("Fail to sent weather to user " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);
                        ClientWrite(stream, "failToGetWeather");
                    }
                    catch (Exception e)
                    {
                        _log.Error("Fail to sent weather to user " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);
                    }
                }
                else
                {
                    _log.Warn("Weather is disabled, fail to send data to " + user.Username + "@" + me.ClientIp);
                    ClientWrite(stream, "failToGetWeather");
                }
                
            }
            else if (data.Contains("GetCisternConfig"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to get cistern configs but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    CisternConfig config = DatabaseHandler.GetCisternConfig(_connectionString);

                    ClientWriteSerialized(stream, config);

                    _log.Info("Sent cistern configuration to user " + user.Username + "@" + me.ClientIp);
                }
                catch (MySqlException e)
                {
                    _log.Warn("Could not load cistern configuration on database, trying to create a new configuration.");

                    CisternConfig temp = new CisternConfig(1,10,10,0);

                    if (DatabaseHandler.InsertCisternConfig(_connectionString, temp) == 0)
                    {
                        _log.Error("Error on insert cistern configuration. - " + e.Message, e);
                        ClientWrite(stream, "ErrorOnLoadConfig");
                    }
                    else
                    {
                        ClientWriteSerialized(stream, temp);

                        _log.Info("Sent cistern configuration to user " + user.Username + "@" + me.ClientIp);
                    }
                }
                catch (Exception e)
                {
                    _log.Error("Fail to send cistern configurations to user " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);
                }
            }
            else if (data.Contains("SaveCisternConfig"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to update the cistern configurations but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    ClientWrite(stream, "SendConfig");

                    _log.Info("User " + user.Username + "@" + me.ClientIp + " has sent an SaveCisternConfig request.");

                    CisternConfig temp = (CisternConfig)ClientReadSerilized(stream, 30000);

                    if (DatabaseHandler.UpdateCisternConfig(_connectionString, temp) == 0)
                    {
                        _log.Warn("Could not update cistern configuration on database, trying to create a new configuration.");

                        if(DatabaseHandler.InsertCisternConfig(_connectionString, temp) == 0)
                            throw new Exception("Error on insert cistern configuration.");
                    }

                    _cisternConfig = DatabaseHandler.GetCisternConfig(_connectionString);

                    ClientWrite(stream, "ConfigSaved");

                    _log.Info("The SaveCisternConfig request from " + user.Username + "@" + me.ClientIp + " was successfully completed");
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete SaveCisternConfig request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToSave");
                }
            }
            else if (data.Contains("ListServices"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to list services but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    List<Service> services = DatabaseHandler.GetAllServices(_connectionString);

                    ClientWriteSerialized(stream, services);

                    _log.Info("Listed all services to user " + user.Username + "@" + me.ClientIp);
                }
                catch (Exception e)
                {
                    _log.Error("Fail to list all services to user " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);
                }
            }
            else if (data.Contains("UpdateLink"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to update a service link but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    ClientWrite(stream, "SendService");

                    _log.Info("User " + user.Username + "@" + me.ClientIp + " has sent an UpdateLink request.");

                    Service temp = (Service)ClientReadSerilized(stream, 30000);

                    if(temp.IsSensor)
                        DatabaseHandler.UnlinkDevicePort(_connectionString, temp.DeviceId, temp.DevicePortNumber);

                    DatabaseHandler.UpdateService(_connectionString, temp);

                    ClientWrite(stream, "LinkUpdated");

                    _log.Info("The UpdateLink request from " + user.Username + "@" + me.ClientIp + " was successfully completed and updated the service " + temp.ServiceName + ".");
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete UpdateLink request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToUpdate");
                }
            }
            else if (data.Contains("ListIrrigationSchedules"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to list the irrigation schedules but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    List<IrrigationSchedule> schedules = DatabaseHandler.GetAllIrrigationSchedules(_connectionString);

                    ClientWriteSerialized(stream, schedules);

                    _log.Info("Listed all irrigation schedules to user " + user.Username + "@" + me.ClientIp);
                }
                catch (Exception e)
                {
                    _log.Error("Fail to list all irrigation schedules to user " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);
                }
            }
            else if (data.Contains("AddIrrigationSchedule"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to schedule an irrigation but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    ClientWrite(stream, "sendNewSchedule");

                    _log.Info("Client " + user.Username + "@" + me.ClientIp + " has sent an AddIrrigationSchedule request.");

                    IrrigationSchedule temp = (IrrigationSchedule)ClientReadSerilized(stream, 30000);

                    DatabaseHandler.InsertIrrigationSchedule(_connectionString, temp);

                    ClientWrite(stream, "ScheduleAdded");

                    _log.Info("The AddIrrigationSchedule request from " + user.Username + "@" + me.ClientIp +
                              " was successfully completed and scheduled the irrigation to " + temp.ScheduleTime.ToString("HH:mm:ss") + ".");
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete AddIrrigationSchedule request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToAdd");
                }
            }
            else if (data.Contains("UpdateIrrigationSchedule"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to update an irrigation schedule but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    ClientWrite(stream, "SendSchedule");

                    _log.Info("User " + user.Username + "@" + me.ClientIp + " has sent an UpdateIrrigationSchedule request.");

                    IrrigationSchedule temp = (IrrigationSchedule)ClientReadSerilized(stream, 30000);

                    DatabaseHandler.UpdateIrrigationSchedule(_connectionString, temp);

                    ClientWrite(stream, "ScheduleUpdated");

                    _log.Info("The UpdateIrrigationSchedule request from " + user.Username + "@" + me.ClientIp + " was successfully completed and updated the schedule " + temp.ScheduleName + ".");
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete UpdateIrrigationSchedule request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToUpdate");
                }
            }
            else if (data.Contains("DeleteIrrigationSchedule"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to delete an irrigation schedule but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    _log.Info("User " + user.Username + "@" + me.ClientIp + " has sent an DeleteIrrigationSchedule request.");

                    DatabaseHandler.DeleteIrrigationSchedule(_connectionString, data.Split(";")[1]);

                    ClientWrite(stream, "ScheduleDeleted");

                    _log.Info("The DeleteIrrigationSchedule request from " + user.Username + "@" + me.ClientIp + " was successfully completed and deleted the ScheduleId " + data.Split(";")[1] + ".");
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete DeleteIrrigationSchedule request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToDelete");
                }
            }
            else if (data.Contains("GetIrrigationConfig"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to get irrigation configs but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    IrrigationConfig config = DatabaseHandler.GetIrrigationConfig(_connectionString);

                    ClientWriteSerialized(stream, config);

                    _log.Info("Sent irrigation configuration to user " + user.Username + "@" + me.ClientIp);
                }
                catch (MySqlException e)
                {
                    _log.Warn("Could not load irrigation configuration on database, trying to create a new configuration.");

                    IrrigationConfig temp = new IrrigationConfig(1,80, 10, 33, true);

                    if (DatabaseHandler.InsertIrrigationConfig(_connectionString, temp) == 0)
                    {
                        _log.Error("Error on insert irrigation configuration. - " + e.Message, e);
                        ClientWrite(stream, "ErrorOnLoadConfig");
                    }
                    else
                    {
                        ClientWriteSerialized(stream, temp);

                        _log.Info("Sent irrigation configuration to user " + user.Username + "@" + me.ClientIp);
                    }
                }
                catch (Exception e)
                {
                    _log.Error("Fail to send irrigation configurations to user " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);
                }
            }
            else if (data.Contains("SaveIrrigationConfig"))
            {
                if (!user.IsAdmin)
                {
                    _log.Warn(user.Username + "@" + me.ClientIp + "is trying to update the irrigation configurations but does not have permission.");

                    ClientWrite(stream, "noPermission");

                    return;
                }

                try
                {
                    ClientWrite(stream, "SendConfig");

                    _log.Info("User " + user.Username + "@" + me.ClientIp + " has sent an SaveIrrigationConfig request.");

                    IrrigationConfig temp = (IrrigationConfig)ClientReadSerilized(stream, 30000);

                    if (DatabaseHandler.UpdateIrrigationConfig(_connectionString, temp) == 0)
                    {
                        _log.Warn("Could not update irrigation configuration on database, trying to create a new configuration.");

                        if (DatabaseHandler.InsertIrrigationConfig(_connectionString, temp) == 0)
                            throw new Exception("Error on insert irrigation configuration.");
                    }

                    _irrigationConfig = DatabaseHandler.GetIrrigationConfig(_connectionString);

                    ClientWrite(stream, "ConfigSaved");

                    _log.Info("The SaveIrrigationConfig request from " + user.Username + "@" + me.ClientIp + " was successfully completed");
                }
                catch (Exception e)
                {
                    _log.Error("Error on complete SaveIrrigationConfig request from client " + user.Username + "@" + me.ClientIp + " - " + e.Message, e);

                    ClientWrite(stream, "FailToSave");
                }
            }
            else
            {
                ClientWrite(stream, "InvalidCommand");
                _log.Warn("User " + user.Username + "@" + me.ClientIp + " has sent: " + data);
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
            _log.Info("Updating forecast informations");

            if (_irrigationConfig.UseForecast)
            {
                try
                {
                    _forecast = _weather.CheckForecast();

                    _log.Info("Successfully updated forecasts for " + _forecast.LocationName + "," + _forecast.LocationCountry + " (" + _forecast.LocationLatitude + ";" + _forecast.LocationLongitude + ") ");
                }
                catch (Exception e)
                {
                    _log.Error("Fail to update forecast informations for " + _config.CityName + "," + _config.CountryId + " ==> " + e.Message);
                }
            }
        }

        //Função que liga a irrigação
        static async Task TurnOnIrrigation(int time)
        {
            if (_canRunIrrigation)
            {
                _log.Info("Irrigation turned on.");

                Thread.Sleep(time * 1000);

                _log.Info("Irrigation turned off.");
            }
            else
            {
                _log.Info("The weather does not seem to be perfect for irrigation. Skipping this schedule.");
            }
        }

        //Garbage colector que limpa a lista de clientes e devices conectados
        private static void ClearConnectionList()
        {
            while (_desligar == false)
            {

                int devicesInMemory = _deviceConnections.Count();
                int clientsInMemory = _clientConnections.Count();
                ConnectionCommandStore temp;

                //remove as conexões de devices encerradas da lista
                for (int i = 0; i < devicesInMemory; i++)
                {
                    if (!_deviceConnections.TryTake(out temp))
                    {
                        _log.Error("Não foi possivel limpar a fila de dispositivos. ("+ devicesInMemory + ")");
                    }
                    else
                    {
                        if (temp.Conexao.IsAlive)
                        {
                            _deviceConnections.Add(temp);
                        }
                        else
                        {
                            temp = null;
                        }
                    }
                }

                //remove as conexões de clientes encerradas da lista
                for (int i = 0; i < clientsInMemory; i++)
                {
                    if (!_clientConnections.TryTake(out temp))
                    {
                        _log.Error("Não foi possivel limpar a fila de clientes. ("+ clientsInMemory + ")");
                    }
                    else
                    {
                        if (temp.Conexao.IsAlive)
                        {
                            _clientConnections.Add(temp);
                        }
                        else
                        {
                            temp = null;
                        }
                    }
                }

                //ConsoleWrite("Limpou -- Devices Ativos: {0} -- Devices Removidos: {1}", true, DeviceConnections.Count, DevicesInMemory - DeviceConnections.Count);

                if (!_desligar)//se estiver desligando pula o timer
                    Thread.Sleep(30000);
            }
        }

        private static void JoinAllConnections()
        {
            //Join Devices
            foreach (var device in _deviceConnections)
            {
                if (device.Conexao.IsAlive)
                    device.Conexao.Join();
            }

            //Join Clients
            foreach (var client in _clientConnections)
            {
                if (client.Conexao.IsAlive)
                    client.Conexao.Join();
            }
        }

        //função que resgata o IP local do servidor
        private static string GetLocalIpAddress()
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
