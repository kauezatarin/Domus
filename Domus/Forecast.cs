using System;
using System.Collections.Generic;
using DomusSharedClasses;

namespace Domus
{
    public class Forecast
    {
        public string LocationName { get; private set; }

        public string LocationCountry { get; private set; }

        public string LocationLatitude { get; private set; }

        public string LocationLongitude { get; private set; }

        public DateTime SunRise { get; private set; }

        public DateTime SunSet { get; private set; }

        public List<ForecastData> Forecasts { get; private set; }

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

        public Forecast(Dictionary<DomusEnums.ForecastLocationParameters, string> locationData, Dictionary<DomusEnums.ForecastSunParameters, DateTime> sunData, List<ForecastData> forecastDatas)
        {
            LocationName = locationData[DomusEnums.ForecastLocationParameters.LocationName];
            LocationCountry = locationData[DomusEnums.ForecastLocationParameters.CountryName];
            LocationLatitude = locationData[DomusEnums.ForecastLocationParameters.Latitude];
            LocationLongitude = locationData[DomusEnums.ForecastLocationParameters.Longitude];

            SunRise = sunData[DomusEnums.ForecastSunParameters.Rise];
            SunSet = sunData[DomusEnums.ForecastSunParameters.Set];

            Forecasts = forecastDatas;
        }
    }
}
