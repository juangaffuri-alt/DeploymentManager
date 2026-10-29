using PasadorScript.Repository;
using PasadorScript.Services.Crypto;

namespace PasadorScript.Services.Pat
{
    public class PatRepositoryService : IPatRepositoryService
    {
        private readonly IDbRepository _db;
        private readonly IEncriptacionHelper _encriptacionHelper;

        public PatRepositoryService(IDbRepository db, IEncriptacionHelper encriptacionHelper)
        {
            _db = db;
            _encriptacionHelper = encriptacionHelper;
        }
        public async Task<string> GetPatByUser(string username)
        {
            var pat = await _db.GetPats(username);
            try
            {
                return _encriptacionHelper.Desencriptar(pat.First().Pat1!);
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
