namespace DomusSharedClasses
{
    /// <summary>
    /// Extensions methods from DomusSharedClasses
    /// </summary>
    public static class Extensions
    {
        public static object GetPropertyValue<T>(this T data, string propertyName)
        {
            var prop = typeof(T).GetProperty(propertyName);
            var value = prop.GetValue(data, null);

            return value;
        }
    }
}
