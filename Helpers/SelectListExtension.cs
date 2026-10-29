namespace PasadorScript.Helpers
{
    public static class SelectListExtension
    {
        public static void AddDefaultValues<T>(this ICollection<T> collection, ICollection<T> items)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (items == null) return;
            foreach (var item in items)
            {
                collection.Add(item);
            }

        }
    }
}
