using System;
using System.Collections.Concurrent;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Domus
{
    class ConfigHandler
    {
        public BlockingCollection<string> BannedIPs = new BlockingCollection<string>();

        public int MaxClientsConnections { get; private set; } = -1;

        public int MaxDevicesConnections { get; private set; } = -1;

        public int ClientListeningPort { get; private set; } = 9090;

        public int DeviceListeningPort { get; private set; } = 9595;

        public int MinDataDelay { get; private set; } = 30;

        public string DatabaseIp { get; private set; } = "localhost";

        public int DatabasePort { get; private set; } = 3306;

        public string DatabaseUser { get; private set; } = "root";

        public string DatabasePassword { get; private set; } = "";

        public string DatabaseName { get; private set; } = "domus";

        public string CityName { get; private set; } = "piracicaba";

        public string CountryId { get; private set; } = "br";

        public string WeatherApiKey { get; private set; } = "API_KEY";

        private string _banpath = @"bannedip";

        private string _configpath = @"serverConf";

        private string _logConfPath = @"log4net.config";

        public ConfigHandler()
        {
            if (!File.Exists(_banpath))
            {
                // Create a file to write to.
                using (StreamWriter bannedIpWrite = File.CreateText(_banpath))
                {
                    bannedIpWrite.WriteLine("#Write one ip per line like the exemple above:");
                    bannedIpWrite.WriteLine("#127.0.0.1");
                    bannedIpWrite.WriteLine("#127.0.0.2");
                }

            }
            if (!File.Exists(_configpath))
            {
                // Create a file to write to.
                using (StreamWriter configWrite = File.CreateText(_configpath))
                {
                    configWrite.WriteLine("#Server Configuration file" + configWrite.NewLine);
                    configWrite.WriteLine("#Max Connections per type. Set -1 to unlimited.");
                    configWrite.WriteLine("maxClientsConnections: -1");
                    configWrite.WriteLine("maxDevicesConnections: -1");
                    configWrite.WriteLine(configWrite.NewLine + "#Connection Settings. minDataDelay is in seconds:");
                    configWrite.WriteLine("clientListeningPort: 9090");
                    configWrite.WriteLine("deviceListeningPort: 9595");
                    configWrite.WriteLine("minDataDelay: 30");
                    configWrite.WriteLine(configWrite.NewLine + "#Database settings:");
                    configWrite.WriteLine("databaseIP: localhost");
                    configWrite.WriteLine("databasePort: 3306");
                    configWrite.WriteLine("databaseUser: root");
                    configWrite.WriteLine("databasePassword: root");
                    configWrite.WriteLine("databaseName: domus");
                    configWrite.WriteLine(configWrite.NewLine + "#Forecast service configuratiosn. Obtain a API Key at: https://home.openweathermap.org");
                    configWrite.WriteLine("cityName: piracicaba");
                    configWrite.WriteLine("countryId: br");
                    configWrite.WriteLine("weatherApiKey: API_KEY");
                }

            }
            if (!File.Exists(_logConfPath))
            {
                // Create a file to write to.
                using (StreamWriter logConfWrite = File.CreateText(_logConfPath))
                {
                    logConfWrite.Write("<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n<log4net>\r\n  \r\n  <appender name=\"RollingLogFileAppender\" type=\"log4net.Appender.RollingFileAppender\">\r\n    <lockingModel type=\"log4net.Appender.FileAppender+MinimalLock\"/>\r\n    <file value=\"ServerLogs\\\" />\r\n    <datePattern value=\"dd-MM-yyyy.\'txt\'\"/>\r\n    <staticLogFileName value=\"false\"/>\r\n    <appendToFile value=\"true\"/>\r\n    <rollingStyle value=\"Date\"/>\r\n    <maxSizeRollBackups value=\"60\"/>\r\n    <maximumFileSize value=\"15MB\"/>\r\n    <layout type=\"log4net.Layout.PatternLayout\">\r\n      <conversionPattern value=\"%date - [%level] [%thread] - %message%newline%exception\"/>\r\n    </layout>\r\n  </appender>\r\n\r\n  <appender name=\"ConsoleAppender\" type=\"log4net.Appender.ManagedColoredConsoleAppender\">\r\n    <mapping>\r\n      <level value=\"ERROR\" />\r\n      <foreColor value=\"DarkRed\" />\r\n    </mapping>\r\n    <mapping>\r\n      <level value=\"FATAL\" />\r\n      <foreColor value=\"White\" />\r\n      <backColor value=\"Red\" />\r\n    </mapping>\r\n    <mapping>\r\n      <level value=\"WARN\" />\r\n      <foreColor value=\"Yellow\" />\r\n    </mapping>\r\n    <mapping>\r\n      <level value=\"INFO\" />\r\n      <foreColor value=\"White\" />\r\n    </mapping>\r\n    <mapping>\r\n      <level value=\"DEBUG\" />\r\n      <foreColor value=\"Blue\" />\r\n    </mapping>\r\n\r\n    <layout type=\"log4net.Layout.PatternLayout\">\r\n      <conversionPattern value=\"%date - [%level] [%thread] - %message%newline\" />\r\n    </layout>\r\n  </appender>\r\n  \r\n  <root>\r\n    <level value=\"ALL\"/>\r\n    <appender-ref ref=\"RollingLogFileAppender\"/>\r\n    <appender-ref ref=\"ConsoleAppender\" />\r\n  </root>\r\n</log4net>");
                }

            }


            LoadConfigs();//must be before log creation

        }

        private void LoadConfigs()
        {
            // Open the file and add banned ips to the list
            using (StreamReader bannedIpRead = File.OpenText(_banpath))
            {
                string line = "";

                while ((line = bannedIpRead.ReadLine()) != null)
                {
                    if (line.Contains('#') != true && line.Trim(' ') != "")
                    {
                        BannedIPs.Add(line.Trim(' '));
                    }
                }
            }

            // Open the file and set the config to the properties
            using (StreamReader configRead = File.OpenText(_configpath))
            {
                string line = "";

                while ((line = configRead.ReadLine()) != null)
                {
                    if (line.Contains('#'))
                    {
                        /*do nothing*/
                    }
                    else if (line.Contains("maxClientsConnections"))
                    {
                        MaxClientsConnections = Convert.ToInt32(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("maxDevicesConnections"))
                    {
                        MaxDevicesConnections = Convert.ToInt32(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("clientListeningPort"))
                    {
                        ClientListeningPort = Convert.ToInt32(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("deviceListeningPort"))
                    {
                        DeviceListeningPort = Convert.ToInt32(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("minDataDelay"))
                    {
                        MinDataDelay = (Convert.ToInt32(line.Split(':')[1].Trim(' ')) * 10) + 100;
                    }
                    else if (line.Contains("databaseIP"))
                    {
                        DatabaseIp = line.Split(':')[1].Trim(' ');
                    }
                    else if (line.Contains("databasePort"))
                    {
                        DatabasePort = Convert.ToInt32(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("databaseUser"))
                    {
                        DatabaseUser = line.Split(':')[1].Trim(' ');
                    }
                    else if (line.Contains("databasePassword"))
                    {
                        DatabasePassword = line.Split(':')[1].Trim(' ');
                    }
                    else if (line.Contains("databaseName"))
                    {
                        DatabaseName = line.Split(':')[1].Trim(' ');
                    }
                    else if (line.Contains("cityName"))
                    {
                        CityName = line.Split(':')[1].Trim(' ');
                    }
                    else if (line.Contains("countryId"))
                    {
                        CountryId = line.Split(':')[1].Trim(' ');
                    }
                    else if (line.Contains("weatherApiKey"))
                    {
                        WeatherApiKey = line.Split(':')[1].Trim(' ');
                    }
                }
            }
        }

        public void AddBannedIp(string ip)
        {
            if (!BannedIPs.Contains(ip))
                BannedIPs.Add(ip);
        }

        public void SaveBannedIPs()
        {
            using (StreamWriter bannedIpWrite = File.CreateText(_banpath))
            {
                bannedIpWrite.WriteLine("#Write one ip per line like the exemple above:");
                bannedIpWrite.WriteLine("#127.0.0.1");
                bannedIpWrite.WriteLine("#127.0.0.2");

                foreach (var bannedip in BannedIPs)
                {
                    bannedIpWrite.WriteLine(bannedip);
                }
            }
        }

        public void RemoveBannedIp(string ip)
        {
            int banIpsCount = BannedIPs.Count;
            string temp;

            for (int i = 0; i < banIpsCount; i++)
            {
                temp = BannedIPs.Take();

                if(temp != ip)
                    BannedIPs.Add(temp);
                else
                    break;
                
            }

            SaveBannedIPs();
        }

        public void SaveConfigs()
        {
            SaveBannedIPs();
        }
    }
}
