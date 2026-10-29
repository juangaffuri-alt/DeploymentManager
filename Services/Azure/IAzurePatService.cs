namespace PasadorScript.Services.Azure
{
    public interface IAzurePatService
    {
        Task<bool> PatProof(string pat);
    }
}
