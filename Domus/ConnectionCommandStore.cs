using System.Threading;

namespace Domus
{
    class ConnectionCommandStore
    {
        public ConnectionCommandStore(Thread client)
        {
            Conexao = client;
            Command = null;
            ClientIp = null;
            DeviceType = -1;
            DeviceName = null;
            DataDelay = 30;
        }

        public ConnectionCommandStore()
        {
            Conexao = null;
            Command = null;
            ClientIp = null;
            DeviceType = -1;
            DeviceName = null;
            DataDelay = 30;
        }

        public Thread Conexao { get; set; }

        public string Command { get; set; }

        public string ClientIp { get; set; }

        public string DeviceName { get; set; }

        public int DeviceType { get; set; }

        public int DataDelay { get; set; }

        public string DeviceUniqueId { get; set; }
    }
}
