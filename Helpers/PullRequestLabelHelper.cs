namespace PasadorScript.Helpers
{
    /// <summary>
    /// Builds Azure DevOps pull request label names from envío metadata.
    /// </summary>
    public static class PullRequestLabelHelper
    {
        /// <summary>
        /// The user-facing warning when the PR was created but one or more labels could not be applied.
        /// </summary>
        public const string MensajeEtiquetasNoAplicadas =
            "El pull request se generó, pero no se pudieron aplicar las etiquetas.";

        /// <summary>
        /// Builds the list of pull request labels for an envío.
        /// </summary>
        /// <param name="isHotfix"><see langword="true" /> to include the <c>Hotfix</c> label; otherwise, <see langword="false" />.</param>
        /// <param name="tipoCambioDescripcion">The classification description from the catalog, or <see langword="null" /> if it was not resolved.</param>
        /// <param name="nroIncidente">The reference number of the envío.</param>
        /// <returns>A list with the optional <c>Hotfix</c> label and the reference label, without empty or duplicate entries.</returns>
        public static IReadOnlyList<string> ArmarLabels(bool isHotfix, string? tipoCambioDescripcion, int nroIncidente)
        {
            var labels = new List<string>(2);

            if (isHotfix)
            {
                labels.Add("Hotfix");
            }

            var tipo = tipoCambioDescripcion?.Trim();
            var referencia = string.IsNullOrEmpty(tipo)
                ? nroIncidente.ToString()
                : $"{tipo} {nroIncidente}";

            if (!string.IsNullOrEmpty(referencia)
                && !labels.Contains(referencia, StringComparer.Ordinal))
            {
                labels.Add(referencia);
            }

            return labels;
        }
    }
}
