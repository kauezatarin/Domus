using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace DomusSharedClasses
{
    public class Forecast
    {
        public string LocationName { get; private set; }

        public string LocationCountry { get; private set; }

        public string LocationLatitude { get; private set; }

        public string LocationLongitude { get; private set; }

        public DateTime SunRise { get; private set; }

        public DateTime SunSet { get; private set; }

        public List<ForecastData> Forecasts {get; private set;}

        public Forecast(string locationName, string locationCountry, string locationLatitude, string locationLongitude, DateTime sunRise, DateTime sunSet, List<ForecastData> forecasts)
        {
            LocationName = locationName;
            LocationCountry = locationCountry;
            LocationLatitude = locationLatitude;
            LocationLongitude = locationLongitude;

            SunRise = sunRise;
            SunSet = sunSet;

            Forecasts = forecasts;
        }

        public Forecast(List<string> locationData, List<DateTime> sunData, List<ForecastData> forecastDatas)
        {
            LocationName = locationData[0];
            LocationCountry = locationData[1];
            LocationLatitude = locationData[2];
            LocationLongitude = locationData[3];

            SunRise = sunData[0];
            SunSet = sunData[1];

            Forecasts = forecastDatas;
        }
    }
}
