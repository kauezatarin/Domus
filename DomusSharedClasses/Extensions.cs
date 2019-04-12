using Newtonsoft.Json;

namespace DomusSharedClasses
{
    /// <summary>
    /// Extensions methods from DomusSharedClasses
    /// </summary>
    public static class Extensions
    {
        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
        };

        public static object GetPropertyValue<T>(this T data, string propertyName)
        {
            var prop = typeof(T).GetProperty(propertyName);
            var value = prop.GetValue(data, null);

            return value;
        }

        public static string SerializeToJsonString<T>(this T data)
        {
            string serializedObject = JsonConvert.SerializeObject(data, Formatting.None, JsonSerializerSettings);

            return serializedObject;
        }

        public static T ParseJsonStringToObject<T>(this string data)
        {
            return JsonConvert.DeserializeObject<T>(data, JsonSerializerSettings);
        }
    }
}
