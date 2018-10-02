using System;
using System.Collections.Generic;
using System.Text;

namespace DomusSharedClasses
{
    [Serializable]

    class IrrigationConfig
    {
        public IrrigationConfig(int configId, int maxSoilHumidity, int minAirTemperature, int maxAirTemperature, bool useForecast)
        {
            ConfigId = configId;
            MaxSoilHumidity = maxSoilHumidity;
            MinAirTemperature = minAirTemperature;
            MaxAirTemperature = maxAirTemperature;
            UseForecast = useForecast;
        }

        public int ConfigId { get; set; }

        public int MaxSoilHumidity { get; set; }

        public int MinAirTemperature { get; set; }

        public int MaxAirTemperature { get; set; }

        public bool UseForecast { get; set; }

    }
}
