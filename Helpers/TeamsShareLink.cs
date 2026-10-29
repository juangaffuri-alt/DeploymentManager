namespace PasadorScript.Helpers;

/// <summary>
/// Única representación del deep link de “Share to Teams”.
/// </summary>
public static class TeamsShareLink
{
    private const string ShareEndpoint = "https://teams.microsoft.com/share";
    private const string Referrer = "PasadorScript";

    public static string FromContentUrl(string absoluteContentUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteContentUrl);

        return $"{ShareEndpoint}?href={Uri.EscapeDataString(absoluteContentUrl)}&referrer={Uri.EscapeDataString(Referrer)}&preview=true";
    }
}
