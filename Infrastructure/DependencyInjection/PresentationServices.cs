using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OpenIddict.Abstractions;
using PasadorScript.Handlers;
using PasadorScript.Services.PolicyConfiguration;
using PasadorScript.Services.RoleMapper;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;
using Bpn.Authentication;
using Bpn.Authentication.OpenIdConnect;

namespace PasadorScript.Infrastructure.DependencyInjection;

public static class PresentationServices
{
    public static IServiceCollection AddPresentationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {

        services.AddOauthBPNAuthentication(configuration);
        // Authentication

        // Authorization
        //services.AddScoped<IAuthorizationHandler, MultipleAdGroupsHandler>();
        //services.AddSingleton<IAuthorizationPolicyProvider, DatabaseAuthorizationPolicyProvider>();

        //// Session
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(1);
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        return services;
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.LoginPath = "/Account/Signin";
        options.LogoutPath = "/Account/SignOut";
        options.AccessDeniedPath = "/Error/Unauthorized";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = false;
    }

    private static void ConfigureOpenIdConnect(
        OpenIdConnectOptions options,
        IConfiguration configuration)
    {
        options.Authority = configuration["OauthConfig:Authority"];
        options.ClientId = configuration["OauthConfig:ClientId"];
        options.ClientSecret = configuration["OauthConfig:ClientSecret"];
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.UsePkce = true;
        options.Prompt = "login";
        options.CallbackPath = "/callback";
        options.SignedOutCallbackPath = "/signout-callback-oidc";
        options.SignedOutRedirectUri = "/Home/Index";

        // Scopes
        var scopes = configuration.GetSection($"OauthConfig:Scopes").Get<string[]>()!;
        options.Scope.Clear();
        foreach (var scope in scopes)
        {
            options.Scope.Add(scope);
        }

        // Security
        options.BackchannelHttpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        // Events
        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = OnTokenValidatedHandler,
            OnAuthenticationFailed = OnAuthenticationFailedHandler,
            OnSignedOutCallbackRedirect = OnSignedOutCallbackRedirectHandler
        };
    }

    private static Task OnTokenValidatedHandler(TokenValidatedContext context)
    {
        if (context.TokenEndpointResponse?.AccessToken != null)
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(context.TokenEndpointResponse.AccessToken);

            context.Properties?.ExpiresUtc = token.ValidTo;

            var identity = (ClaimsIdentity)context.Principal?.Identity!;
            AddJwtClaims(identity, token, context.HttpContext);
        }

        return Task.CompletedTask;
    }

    private static void AddJwtClaims(
        ClaimsIdentity identity,
        System.IdentityModel.Tokens.Jwt.JwtSecurityToken token,
        HttpContext httpContext)
    {
        identity.AddClaim(new Claim(ClaimTypes.Email,
            token.Claims.FirstOrDefault(c => c.Type == Claims.Email)?.Value!));
        identity.AddClaim(new Claim(ClaimTypes.Surname,
            token.Claims.FirstOrDefault(c => c.Type == Claims.FamilyName)?.Value!));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier,
            token.Claims.FirstOrDefault(c => c.Type == Claims.Subject)?.Value!));

        var uid = token.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
        if (!string.IsNullOrEmpty(uid))
        {
            identity.AddClaim(new Claim(ClaimTypes.PrimaryGroupSid, uid));

            //var roleMapper = httpContext.RequestServices.GetRequiredService<IRoleMapperService>();
            //var mappedRoles = roleMapper.MapGroupsToRolesAsync(uid).Result;

            //foreach (var role in mappedRoles)
            //{
            //    identity.AddClaim(new Claim(ClaimTypes.Role, role));
            //}
        }
    }

    private static Task OnAuthenticationFailedHandler(AuthenticationFailedContext context)
    {
        context.Response.Redirect("/Error/Unauthorized");
        context.HandleResponse();
        return Task.CompletedTask;
    }

    private static Task OnSignedOutCallbackRedirectHandler(
        RemoteSignOutContext context)
    {
        context.Response.Redirect("/Home/Index");
        context.HandleResponse();
        return Task.CompletedTask;
    }
}