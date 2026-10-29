namespace PasadorScript.Extensions
{
    public static class SqlTemplateExtension
    {
        public static IConfigurationBuilder AddSqlTemplateSettings(
            this IConfigurationBuilder builder,
            bool optional = false,
            bool reloadOnChange = true)
        {
            return builder.AddJsonFile("sqlTemplateSettings.json", optional, reloadOnChange);
        }
    }
}
