using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PasadorScript.Helpers
{
    public class NoCacheConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private readonly string _metadataAddress;
        private readonly IConfigurationRetriever<OpenIdConnectConfiguration> _retriever;
        private readonly IDocumentRetriever _documentRetriever;

        public NoCacheConfigurationManager(string metadataAddress)
        {
            _metadataAddress = metadataAddress ?? throw new ArgumentNullException(nameof(metadataAddress));
            _retriever = new OpenIdConnectConfigurationRetriever();
            _documentRetriever = new HttpDocumentRetriever { RequireHttps = false }; // cambiar a true si usás HTTPS
        }

        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
        {
            return await _retriever.GetConfigurationAsync(_metadataAddress, _documentRetriever, cancel);
        }

        public void RequestRefresh()
        {
            // no hace nada porque no hay caché
        }
    }
}
