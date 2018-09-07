using System;
using System.Collections.Generic;
using System.Text;

namespace DomusSharedClasses
{
    [Serializable]
    public class CisternConfig
    {
        public CisternConfig(int configId = 0, int timeOfRain = 0, int minWaterLevel = 0, int minLevelAction = 0)
        {
            ConfigId = configId;
            TimeOfRain = timeOfRain;
            MinWaterLevel = minWaterLevel;
            MinLevelAction = minLevelAction;
        }

        public int ConfigId { get; set; }

        public int TimeOfRain { get; set; }

        public int MinWaterLevel { get; set; }

        public int MinLevelAction { get; set; }
    }
}
