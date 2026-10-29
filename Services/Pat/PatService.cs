using PasadorScript.Repository;
using PasadorScript.Services.Azure;
using PasadorScript.Services.Crypto;

namespace PasadorScript.Services.Pat
{
    public class PatService : IPatService
    {
        private readonly IDbRepository _db;
        private readonly IAzurePatService _serviciosAzure;
        private readonly IEncriptacionHelper _encriptacionHelper;

        // Inyectar repositorios, etc

        public PatService(IDbRepository db, IAzurePatService serviciosAzure, IEncriptacionHelper encriptacionHelper)
        {
            _db = db;
            _serviciosAzure = serviciosAzure;
            _encriptacionHelper = encriptacionHelper;
        }

        public async Task<bool> UserHasValidPatAsync(string username)
        {
            var patRes = await _db.GetPats(username);
            var patRecord = patRes.FirstOrDefault();
            if (patRecord == null)
                return false;
            if (patRecord.Pat1 != null)
            {
                var plainPat = _encriptacionHelper.Desencriptar(patRecord.Pat1);
                // Opcional: validar el PAT contra la API de Azure DevOps
                return await ValidatePatAsync(plainPat);
            }
            else
                return false;
        }

        private async Task<bool> ValidatePatAsync(string pat)
        {
            return await _serviciosAzure.PatProof(pat);
        }


    }
}
