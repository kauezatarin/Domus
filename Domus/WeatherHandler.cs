using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Xml;

namespace Domus
{
    /* USO:
       WeatherHandler Weather = new WeatherHandler("Piracicaba", "br");
       Weather.CheckWeather();
       System.Console.WriteLine(Weather.Temperature);

        https://openweathermap.org/weather-data
     
     */

    class WeatherHandler
    {
        public WeatherHandler(string City, string Country, string apiKey)
        {
            this.City = City;
            this.Country = Country;
            this.APIKey = apiKey;
        }

        public string City { get; private set; }

        public string Country { get; private set; }

        private string APIKey { get; set; }

        public Forecast CheckWeather()
        {
            Forecast forecast = null;

            try
            {
                WeatherAPI DataAPI = new WeatherAPI(City + "," + Country, APIKey);

                forecast = DataAPI.GetForecast();
            }
            catch (Exception e)
            {
                throw e;
            }

            return forecast;
        }
    }

    class WeatherAPI
    {
        private static string APIKEY;
        private string CurrentURL;
        private XmlDocument xmlDocument;

        public WeatherAPI(string location, string apiKey)
        {
            APIKEY = apiKey;
            SetCurrentURL(location);
            xmlDocument = GetXML(CurrentURL);
        }

        private void SetCurrentURL(string location)
        {
            CurrentURL = "http://api.openweathermap.org/data/2.5/forecast?q="
                         + location + "&mode=xml&lang=pt&units=metric&APPID=" + APIKEY;

        }

        private XmlDocument GetXML(string CurrentURL)
        {
            using (WebClient client = new WebClient())
            {
                string xmlContent = client.DownloadString(CurrentURL);
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(xmlContent);
                return xmlDocument;
            }
        }

        public Forecast GetForecast()
        {
            List<string> locationData = getLocationData();
            List<DateTime> sunData = getSunData();
            List<ForecastData> forecastDatas = getForecastDatas();

            return new Forecast(locationData,sunData,forecastDatas);
        }

        private List<string> getLocationData()
        {
            List<string> data = new List<string>();

            data.Add(xmlDocument.SelectSingleNode("//location//name").FirstChild.Value);//resgata o nome da cidade
            data.Add(xmlDocument.SelectSingleNode("//location//country").FirstChild.Value);//resgata o nome do país

            data.Add(xmlDocument.SelectSingleNode("//location//location").Attributes["latitude"].Value);//resgata a latitude da localização
            data.Add(xmlDocument.SelectSingleNode("//location//location").Attributes["longitude"].Value);//resgata a longitude da localização

            return data;
        }

        private List<DateTime> getSunData()
        {
            List<DateTime> data = new List<DateTime>();

            data.Add(GenerateDatetime(xmlDocument.SelectSingleNode("//sun").Attributes["rise"].Value));//resgata a latitude da localização
            data.Add(GenerateDatetime(xmlDocument.SelectSingleNode("//sun").Attributes["set"].Value));//resgata a longitude da localização

            return data;
        }

        private List<ForecastData> getForecastDatas()
        {
            List<ForecastData> data = new List<ForecastData>();
            XmlNodeList nodes = xmlDocument.SelectSingleNode("//forecast").ChildNodes;

            string fromData;
            string toData;
            string value;
            string type;
            
            foreach (XmlNode node in nodes)
            {
                fromData = node.Attributes["from"].Value;
                toData = node.Attributes["to"].Value;

                XmlNodeList tempnodes = node.ChildNodes;

                if(tempnodes[1].Attributes.Count > 0)
                {
                    value = tempnodes[1].Attributes["value"].Value.Replace(".", ",");
                    type = tempnodes[1].Attributes["type"].Value;
                }
                else
                {
                    value = "-1";
                    type = "none";
                }

                data.Add(new ForecastData(GenerateDatetime(fromData),GenerateDatetime(toData), Convert.ToSingle(value), type));
            }


            return data;
        }

        private DateTime GenerateDatetime(string dataString, bool returnGMT = false)
        {
            string[] dateTime = dataString.Split("T");
            string[] date = dateTime[0].Split("-");
            string[] time = dateTime[1].Split(":");

            DateTime tempDateTime = new DateTime(Convert.ToInt32(date[0]), Convert.ToInt32(date[1]), Convert.ToInt32(date[2]), Convert.ToInt32(time[0]), Convert.ToInt32(time[1]), Convert.ToInt32(date[2]));

            if (!returnGMT)//retorna o horário convertido para 
            {
                tempDateTime = TimeZoneInfo.ConvertTimeFromUtc(tempDateTime,TimeZoneInfo.Local);
            }

            return tempDateTime;
        }
    }
}
