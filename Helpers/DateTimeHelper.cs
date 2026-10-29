namespace PasadorScript.Helpers
{
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo ArgentinaTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");

        public static DateTime ToArgentinaTime(this DateTime utcDateTime)
        {
            if (utcDateTime.Kind != DateTimeKind.Utc)
            {
                // Asumir que es UTC si no está especificado
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }

            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, ArgentinaTimeZone);
        }

        public static DateTime? ToArgentinaTime(this DateTime? utcDateTime)
        {
            if (!utcDateTime.HasValue) return null;
            return utcDateTime.Value.ToArgentinaTime();
        }

        public static string ToArgentinaTimeString(this DateTime utcDateTime, string format = "dd/MM/yyyy HH:mm:ss")
        {
            return utcDateTime.ToArgentinaTime().ToString(format);
        }

        public static string ToShortArgentinaTimeString(this DateTime utcDateTime)
        {
            return utcDateTime.ToArgentinaTime().ToString("dd/MM/yyyy HH:mm");
        }

        /// <summary>
        /// Obtiene la fecha y hora actual en la zona horaria de Argentina.
        /// </summary>
        public static DateTime GetArgentinaNow()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ArgentinaTimeZone);
        }

        /// <summary>
        /// Obtiene el inicio del dia calendario (00:00:00) en Argentina para la fecha indicada.
        /// </summary>
        /// <param name="date">Fecha calendario (se ignora la hora).</param>
        public static DateTime GetArgentinaStartOfDay(DateTime date)
        {
            return DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        }

        /// <summary>
        /// Obtiene el fin inclusive del dia calendario (23:59:59.9999999) en Argentina para la fecha indicada.
        /// </summary>
        /// <param name="date">Fecha calendario (se ignora la hora).</param>
        public static DateTime GetArgentinaEndOfDay(DateTime date)
        {
            return DateTime.SpecifyKind(date.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);
        }

        /// <summary>
        /// Convierte una marca de tiempo local de Argentina a UTC.
        /// </summary>
        /// <param name="argentinaLocal">Fecha y hora local de Argentina (kind unspecified).</param>
        public static DateTime ToUtcFromArgentina(DateTime argentinaLocal)
        {
            var unspecified = DateTime.SpecifyKind(argentinaLocal, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, ArgentinaTimeZone);
        }
    }
}
