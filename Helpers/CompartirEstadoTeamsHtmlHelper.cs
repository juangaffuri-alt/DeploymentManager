using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PasadorScript.ViewModels;

namespace PasadorScript.Helpers;

public static class CompartirEstadoTeamsHtmlHelper
{
    private const string PartialName = "_CompartirEstadoTeams";
    private const string ReleaseControllerName = "Release";
    private const string ViewStatusActionName = "ViewStatus";

    public static IHtmlContent CompartirEstadoTeams(
        this IHtmlHelper htmlHelper,
        int? pullRequestId,
        string? projectType = null,
        bool? serviciosGx = null)
    {
        ArgumentNullException.ThrowIfNull(htmlHelper);

        if (pullRequestId is not > 0)
        {
            return HtmlString.Empty;
        }

        var estadoUrl = CrearUrlEstadoDeploy(htmlHelper, pullRequestId.Value, projectType, serviciosGx);
        if (string.IsNullOrWhiteSpace(estadoUrl))
        {
            return HtmlString.Empty;
        }

        return htmlHelper.Partial(PartialName, new CompartirEstadoTeamsViewModel
        {
            EstadoUrl = estadoUrl,
            TeamsShareUrl = TeamsShareLink.FromContentUrl(estadoUrl)
        });
    }

    private static string? CrearUrlEstadoDeploy(
        IHtmlHelper htmlHelper,
        int pullRequestId,
        string? projectType,
        bool? serviciosGx)
    {
        var urlHelperFactory = htmlHelper.ViewContext.HttpContext.RequestServices
            .GetRequiredService<IUrlHelperFactory>();
        var urlHelper = urlHelperFactory.GetUrlHelper(htmlHelper.ViewContext);
        var protocol = htmlHelper.ViewContext.HttpContext.Request.Scheme;

        var routeValues = new RouteValueDictionary { ["id"] = pullRequestId };
        if (!string.IsNullOrWhiteSpace(projectType))
        {
            routeValues["projectType"] = projectType;
        }

        if (serviciosGx.HasValue)
        {
            routeValues["serviciosGx"] = serviciosGx.Value;
        }

        return urlHelper.Action(ViewStatusActionName, ReleaseControllerName, routeValues, protocol);
    }
}
