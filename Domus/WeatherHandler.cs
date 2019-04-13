using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Xml;
using DomusSharedClasses;

namespace Domus
{
    /* USO:
       WeatherHandler Weather = new WeatherHandler("Piracicaba", "br");
       Weather.CheckWeather();
       System.Console.WriteLine(Weather.Temperature);

        https://openweathermap.org/weather-data
     
     */

    /// <summary>
    /// Class that deals with forecast data 
    /// </summary>
    class WeatherHandler
    {
        /// <summary>
        /// Constructor for the class <see cref="WeatherHandler"/>
        /// </summary>
        /// <param name="city">The name of the city that the data will be looked up</param>
        /// <param name="country">The ISO 3166 country code</param>
        /// <param name="apiKey">API Key used to authenticate with at OpenWeatherMap.org</param>
        public WeatherHandler(string city, string country, string apiKey)
        {
            this.City = city;
            this.Country = country;
            this.ApiKey = apiKey;
        }

        /// <summary>
        /// Gets the city name
        /// </summary>
        public string City { get; private set; }

        /// <summary>
        /// Gets the use ISO 3166 country code
        /// </summary>
        public string Country { get; private set; }

        /// <summary>
        /// Gets or sets the API Key
        /// </summary>
        private string ApiKey { get; set; }

        /// <summary>
        /// Gets a five days forecast
        /// </summary>
        /// <returns>The forecast for the next five days</returns>
        public Forecast CheckForecast()
        {
            Forecast forecast = null;

            try
            {
                WeatherApi dataApi = new WeatherApi(City + "," + Country, ApiKey);

                forecast = dataApi.GetForecast();
            }
            catch (Exception e)
            {
                throw e;
            }

            return forecast;
        }

        /// <summary>
        /// Gets the current Weather status
        /// </summary>
        /// <returns>The current weather data</returns>
        public WeatherData CheckWeather()
        {
            WeatherData weather = null;

            try
            {
                WeatherApi dataApi = new WeatherApi(City + "," + Country, ApiKey, true);//gets current weather instead of 5 days forecast

                weather = dataApi.GetWeather();
            }
            catch (Exception e)
            {
                throw e;
            }

            return weather;
        }
    }

    /// <summary>
    /// Class that deals with the forecast API requisitions
    /// </summary>
    class WeatherApi
    {
        private static string _apikey;
        private string _currentForecastUrl;
        private string _currentWeatherUrl;
        private XmlDocument _xmlDocument;

        /// <summary>
        /// Constructor for the class <see cref="WeatherApi"/>
        /// </summary>
        /// <param name="location">City name and country code divided by comma, use ISO 3166 country codes</param>
        /// <param name="apiKey">API Key used to authenticate at OpenWeatherMap.org</param>
        /// <param name="getWeather">Set true to get current weather, false to get five days forecast</param>
        public WeatherApi(string location, string apiKey, bool getWeather = false)
        {
            _apikey = apiKey;
            SetCurrentUrl(location);
            _xmlDocument = getWeather ? GetXml(_currentWeatherUrl) : GetXml(_currentForecastUrl);
        }

        /// <summary>
        /// Creates the requisition URL to the given city
        /// </summary>
        /// <param name="location">City name and country code divided by comma, use ISO 3166 country codes</param>
        private void SetCurrentUrl(string location)
        {
            _currentForecastUrl = "http://api.openweathermap.org/data/2.5/forecast?q="
                         + location + "&mode=xml&lang=pt&units=metric&APPID=" + _apikey;

            _currentWeatherUrl = "http://api.openweathermap.org/data/2.5/weather?q="
                                + location + "&mode=xml&lang=pt&units=metric&APPID=" + _apikey;

        }

        /// <summary>
        /// Request the needed data to the API and gets the XML response
        /// </summary>
        /// <param name="currentUrl">Requisition URL</param>
        /// <returns>A XML document with the API's response</returns>
        private XmlDocument GetXml(string currentUrl)
        {
            using (WebClient client = new WebClient())
            {
                string xmlContent = client.DownloadString(currentUrl);
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(xmlContent);
                return xmlDocument;
            }
        }

        #region ForecastMethods

        /// <summary>
        /// Gets the five days forecast
        /// </summary>
        /// <returns>The Forecast for the next five days</returns>
        public Forecast GetForecast()
        {
            Dictionary<DomusEnums.ForecastLocationParameters, string> locationData = GetForecastLocationData();
            Dictionary<DomusEnums.ForecastSunParameters, DateTime> sunData = GetForecastSunData();
            List<ForecastData> forecastDatas = GetForecastDatas();

            return new Forecast(locationData, sunData, forecastDatas);
        }

        /// <summary>
        /// Gets the location data of teh forecast
        /// </summary>
        /// <returns>A dictionary containing the location data</returns>
        private Dictionary<DomusEnums.ForecastLocationParameters, string> GetForecastLocationData()
        {
            Dictionary<DomusEnums.ForecastLocationParameters, string> data = new Dictionary<DomusEnums.ForecastLocationParameters, string>();

            data.Add(DomusEnums.ForecastLocationParameters.LocationName, _xmlDocument.SelectSingleNode("//location//name").FirstChild.Value);//resgata o nome da cidade
            data.Add(DomusEnums.ForecastLocationParameters.CountryName, _xmlDocument.SelectSingleNode("//location//country").FirstChild.Value);//resgata o nome do país

            data.Add(DomusEnums.ForecastLocationParameters.Latitude, _xmlDocument.SelectSingleNode("//location//location").Attributes["latitude"].Value);//resgata a latitude da localização
            data.Add(DomusEnums.ForecastLocationParameters.Longitude, _xmlDocument.SelectSingleNode("//location//location").Attributes["longitude"].Value);//resgata a longitude da localização

            return data;
        }

        /// <summary>
        /// Gets the sun rise and sun set data
        /// </summary>
        /// <returns>A dictionary containing the sun rise and set data</returns>
        private Dictionary<DomusEnums.ForecastSunParameters, DateTime> GetForecastSunData()
        {
            Dictionary<DomusEnums.ForecastSunParameters, DateTime> data = new Dictionary<DomusEnums.ForecastSunParameters, DateTime>();

            data.Add(DomusEnums.ForecastSunParameters.Rise, GenerateDatetime(_xmlDocument.SelectSingleNode("//sun").Attributes["rise"].Value));
            data.Add(DomusEnums.ForecastSunParameters.Set, GenerateDatetime(_xmlDocument.SelectSingleNode("//sun").Attributes["set"].Value));

            return data;
        }

        /// <summary>
        /// Gets the five days forecast, day by day
        /// </summary>
        /// <returns>A list with the five days forecast</returns>
        private List<ForecastData> GetForecastDatas()
        {
            List<ForecastData> data = new List<ForecastData>();
            XmlNodeList nodes = _xmlDocument.SelectSingleNode("//forecast").ChildNodes;

            string fromData;
            string toData;
            string value;
            string type;

            foreach (XmlNode node in nodes)
            {
                fromData = node.Attributes["from"].Value;
                toData = node.Attributes["to"].Value;

                XmlNodeList tempnodes = node.ChildNodes;

                if (tempnodes[1].Attributes.Count > 0)
                {
                    value = tempnodes[1].Attributes["value"].Value.Replace(".", ",");
                    type = tempnodes[1].Attributes["type"].Value;
                }
                else
                {
                    value = "-1";
                    type = "none";
                }

                data.Add(new ForecastData(GenerateDatetime(fromData), GenerateDatetime(toData), Convert.ToSingle(value), type));
            }


            return data;
        }

        #endregion

        #region WeatherMethods

        /// <summary>
        /// Gets the current weather
        /// </summary>
        /// <returns>The current weather data</returns>
        public WeatherData GetWeather()
        {
            WeatherData forecastDatas = GetWeatherData();

            return forecastDatas;
        }

        /// <summary>
        /// Gets the current weather data
        /// </summary>
        /// <returns>The weather data</returns>
        private WeatherData GetWeatherData()
        {
            WeatherData data = new WeatherData();

            data.LocationCity = _xmlDocument.SelectSingleNode("//city").Attributes["name"].Value;//resgata o nome da cidade
            data.LocationCountry = _xmlDocument.SelectSingleNode("//city//country").FirstChild.Value;//resgata o nome do país

            data.Temperature = Convert.ToSingle(_xmlDocument.SelectSingleNode("//temperature").Attributes["value"].Value);
            data.MaxTemperature = Convert.ToSingle(_xmlDocument.SelectSingleNode("//temperature").Attributes["max"].Value);
            data.MinTemperature = Convert.ToSingle(_xmlDocument.SelectSingleNode("//temperature").Attributes["min"].Value);
            data.TemperatureUnit = _xmlDocument.SelectSingleNode("//temperature").Attributes["unit"].Value;

            data.Humidity = Convert.ToSingle(_xmlDocument.SelectSingleNode("//humidity ").Attributes["value"].Value);

            data.PrecipitationMode = _xmlDocument.SelectSingleNode("//precipitation").Attributes["mode"].Value;

            if(data.PrecipitationMode != "no")
                data.PrecipitationValue = Convert.ToInt32(_xmlDocument.SelectSingleNode("//precipitation").Attributes["value"].Value);

            data.Pressure = Convert.ToSingle(_xmlDocument.SelectSingleNode("//pressure").Attributes["value"].Value);
            data.PressureUnit = _xmlDocument.SelectSingleNode("//pressure").Attributes["unit"].Value;

            data.IconDescription = _xmlDocument.SelectSingleNode("//weather").Attributes["value"].Value;
            data.IconValue = _xmlDocument.SelectSingleNode("//weather").Attributes["icon"].Value;

            return data;
        }

        #endregion

        /// <summary>
        /// Create a <see cref="DateTime"/> object from the given string formatted data
        /// </summary>
        /// <param name="dataString">Data string, must be at the format yyyy-mm-ddThh:mm:ss</param>
        /// <param name="returnGmt">Set true to convert the time to GMT</param>
        /// <returns></returns>
        private DateTime GenerateDatetime(string dataString, bool returnGmt = false)
        {
            string[] dateTime = dataString.Split("T");
            string[] date = dateTime[0].Split("-");
            string[] time = dateTime[1].Split(":");

            DateTime tempDateTime = new DateTime(Convert.ToInt32(date[0]), Convert.ToInt32(date[1]), Convert.ToInt32(date[2]), Convert.ToInt32(time[0]), Convert.ToInt32(time[1]), Convert.ToInt32(date[2]));

            if (!returnGmt)//retorna o horário convertido para 
            {
                tempDateTime = TimeZoneInfo.ConvertTimeFromUtc(tempDateTime,TimeZoneInfo.Local);
            }

            return tempDateTime;
        }
    }
}
