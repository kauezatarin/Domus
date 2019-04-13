namespace DomusSharedClasses
{
    /// <summary>
    /// Class that contains all enums of the Domus project
    /// </summary>
    public static class DomusEnums
    {
        /// <summary>
        /// Parameters for the dictionary returned by the <see cref="Domus.WeatherHandler.GetForecastLocationData"/> method
        /// </summary>
        public enum ForecastLocationParameters
        {
            LocationName,
            CountryName,
            Latitude,
            Longitude
        }

        /// <summary>
        /// Parameters for the dictionary returned by the <see cref="Domus.WeatherHandler.GetForecastSunData"/> method
        /// </summary>
        public enum ForecastSunParameters
        {
            Rise,
            Set
        }
    }
}
