using PasadorScript.DTO.Sql;

namespace PasadorScript.Services.SqlTemplates
{
    public interface ITemplateValidator
    {
        ScriptType ScriptType { get; }
        bool CanHandle(string sqlScript);
        SqlValidationResult ValidateTemplate(string sqlScript);
        string GetTemplateName();

    }
}
