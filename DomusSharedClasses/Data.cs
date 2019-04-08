using System;

namespace DomusSharedClasses
{
    [Serializable]
    public class Data
    {
        public Data()
        {
            this.DataId = -1;
            this.DeviceId = null;
            this.CreatedAt = DateTime.Now;
            this.Data1 = null;
            this.Data2 = null;
            this.Data3 = null;
            this.Data4 = null;
        }

        public Data(int dataId, string deviceId, DateTime createdAt, string data1, string data2, string data3, string data4)
        {
            this.DataId = dataId;
            this.DeviceId = deviceId;
            this.CreatedAt = createdAt;
            this.Data1 = data1;
            this.Data2 = data2;
            this.Data3 = data3;
            this.Data4 = data4;
        }

        public Data(int dataId, string deviceId)
        {
            this.DataId = dataId;
            this.DeviceId = deviceId;
            this.CreatedAt = DateTime.Now;
            this.Data1 = null;
            this.Data2 = null;
            this.Data3 = null;
            this.Data4 = null;
        }

        public int DataId { get; set; }

        public string DeviceId { get; set; }

        public DateTime CreatedAt { get; set; }

        public string Data1 { get; set; }

        public string Data2 { get; set; }

        public string Data3 { get; set; }

        public string Data4 { get; set; }
    }
}
