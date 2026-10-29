using Microsoft.AspNetCore.Http;
using PasadorScript.Services.AdGroups;

namespace PasadorScript.Helpers
{
    public static class UsuarioFiltroHelper
    {
        public const string QueryKeyUsuario = "usuario";
        public const string QueryKeyUsuarioExplicit = "usuarioExplicit";
        public const string GroupNameAnalistaTecnico = "DesarrolloTecnico";

        public static async Task<string?> ResolverAsync(
            string? usuarioQuery,
            IQueryCollection query,
            IAdGroupService adGroupService)
        {
            if (query.ContainsKey(QueryKeyUsuario) || EsFlagExplicit(query))
                return string.IsNullOrWhiteSpace(usuarioQuery) ? null : usuarioQuery.Trim();

            var me = await adGroupService.GetInfoUser();
            if (me == null || string.IsNullOrWhiteSpace(me.Username))
                return null;

            var esAnalistaTecnico = me.Roles.Any(r => r.GroupName == GroupNameAnalistaTecnico);
            return esAnalistaTecnico ? me.Username : null;
        }

        public static bool EsFlagExplicit(IQueryCollection query)
        {
            if (!query.ContainsKey(QueryKeyUsuarioExplicit))
                return false;

            var value = query[QueryKeyUsuarioExplicit].ToString();
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || value == "1";
        }

        public static bool? FlagPaginacion(string? usuario) =>
            string.IsNullOrWhiteSpace(usuario) ? true : null;
    }
}
