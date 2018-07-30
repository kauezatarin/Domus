using System;

namespace DomusSharedClasses
{
    public class Data
    {
        public Data()
        {
            this.dataId = -1;
            this.device_id = null;
            this.createdAt = null;
            this.data1 = null;
            this.data2 = null;
            this.data3 = null;
            this.data4 = null;
        }

        public Data(int dataId, string device_id, string createdAt, string data1, string data2, string data3, string data4)
        {
            this.dataId = dataId;
            this.device_id = device_id;
            this.createdAt = createdAt;
            this.data1 = data1;
            this.data2 = data2;
            this.data3 = data3;
            this.data4 = data4;
        }

        public Data(int dataId, string deviceId)
        {
            this.dataId = dataId;
            this.device_id = deviceId;
            this.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            this.data1 = null;
            this.data2 = null;
            this.data3 = null;
            this.data4 = null;
        }

        public int dataId { get; set; }

        public string device_id { get; set; }

        public string createdAt { get; set; }

        public string data1 { get; set; }

        public string data2 { get; set; }

        public string data3 { get; set; }

        public string data4 { get; set; }
    }
}
