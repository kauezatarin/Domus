using System;
using System.Collections.Generic;
using System.Text;

namespace DomusSharedClasses
{
    public class ForecastData
    {
        public DateTime Time_From { get; private set; }

        public DateTime Time_To { get; private set; }

        public float Precipitation_Value { get; private set; }

        public string Precipitation_Type { get; private set; }

        public ForecastData(DateTime timeFrom, DateTime timeTo, float precipitationValue, string precipitationType)
        {
            Time_From = timeFrom;
            Time_To = timeTo;
            Precipitation_Value = precipitationValue;
            Precipitation_Type = precipitationType;
        }
    }
}
