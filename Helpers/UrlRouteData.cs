namespace PasadorScript.Helpers
{
    public static class UrlRouteData
    {
        public static Dictionary<string, string> GetRouteData(string aspAction)
        {
            if (string.IsNullOrEmpty(aspAction) || !aspAction.Contains("?"))
                return new Dictionary<string, string>();

            var queryString = aspAction.Split('?')[1];
            var parameters = new Dictionary<string, string>();

            foreach (var param in queryString.Split('&'))
            {
                var keyValue = param.Split('=');
                if (keyValue.Length == 2)
                {
                    parameters[keyValue[0]] = keyValue[1];
                }
            }

            return parameters;
        }
    }
}
