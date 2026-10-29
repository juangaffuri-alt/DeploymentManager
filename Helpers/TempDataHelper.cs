using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace PasadorScript.Helpers
{
    public enum TypeNotify
    {
        Success = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
    }
    public class TempDataHelper
    {
        private static IDictionary<TypeNotify, string> _literals = new Dictionary<TypeNotify, string>()
        {
            {TypeNotify.Success, "success" },
            {TypeNotify.Info, "info" },
            {TypeNotify.Warning, "warning" },
            {TypeNotify.Error, "error" },
        };

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITempDataDictionaryFactory _tempDataFactory;

        public TempDataHelper(IHttpContextAccessor httpContextAccessor, ITempDataDictionaryFactory tempDataFactory)
        {
            _httpContextAccessor = httpContextAccessor;
            _tempDataFactory = tempDataFactory;
        }

        public async Task Message(TypeNotify typeNotify, string notifyMessage = "Los cambios han sido guardados exitosamente", string title = "Cambios")
        {
            var _tempData = _tempDataFactory.GetTempData(_httpContextAccessor.HttpContext);
            _tempData["NotifyMessage"] = notifyMessage;
            _tempData["NotifyTitle"] = title;
            _tempData["NotifyType"] = _literals[typeNotify]; // success, info, warning, danger
            await Task.FromResult(_tempData);
        }
    }
}
