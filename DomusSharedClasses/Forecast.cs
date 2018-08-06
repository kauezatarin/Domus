using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace DomusSharedClasses
{
    public class Forecast
    {
        public string Location_Name { get; private set; }

        public string Location_Country { get; private set; }

        public string Location_Latitude { get; private set; }

        public string Location_Longitude { get; private set; }

        public string Icon { get; private set; }

        public DateTime Sun_Rise { get; private set; }

        public DateTime Sun_Set { get; private set; }

        public List<ForecastData> Forecasts {get; private set;}

        public WeatherData Weather { get; private set; }

        public Forecast(string locationName, string locationCountry, string locationLatitude, string locationLongitude, DateTime sunRise, DateTime sunSet, List<ForecastData> forecasts, string icon = null)
        {
            Location_Name = locationName;
            Location_Country = locationCountry;
            Location_Latitude = locationLatitude;
            Location_Longitude = locationLongitude;

            Sun_Rise = sunRise;
            Sun_Set = sunSet;

            Icon = icon;

            Forecasts = forecasts;
        }

        public Forecast(List<string> locationData, List<DateTime> sunData, List<ForecastData> forecastDatas, string icon = null)
        {
            Location_Name = locationData[0];
            Location_Country = locationData[1];
            Location_Latitude = locationData[2];
            Location_Longitude = locationData[3];

            Sun_Rise = sunData[0];
            Sun_Set = sunData[1];

            Icon = icon;

            Forecasts = forecastDatas;
        }

        public Forecast(List<string> locationData, WeatherData weatherData, string icon)
        {
            Location_Name = locationData[0];
            Location_Country = locationData[1];
            Location_Latitude = locationData[2];
            Location_Longitude = locationData[3];

            Icon = icon;

            Weather = weatherData;
        }

    }
}
