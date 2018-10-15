using System;
using System.Collections.Generic;
using System.Text;

namespace DomusSharedClasses
{
    public class ForecastData
    {
        public DateTime TimeFrom { get; private set; }

        public DateTime TimeTo { get; private set; }

        //"none" for nothing, "rain" for rain, and "snow" for snow
        public float PrecipitationValue { get; private set; }

        public string PrecipitationType { get; private set; }

        public ForecastData(DateTime timeFrom, DateTime timeTo, float precipitationValue, string precipitationType)
        {
            TimeFrom = timeFrom;
            TimeTo = timeTo;
            PrecipitationValue = precipitationValue;
            PrecipitationType = precipitationType;
        }
    }
}
