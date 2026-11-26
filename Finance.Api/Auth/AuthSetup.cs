using System.Text;
using Finance.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.IdentityModel.Tokens;

namespace Finance.Api.Auth;

public static class Policies
{
    // BF-10: only a Finance Manager can change budgets or decide funding requests.
    public const string Manager = "ManagerOnly";
}

public static class AuthSetup
{
    // BF-10: login and roles from the Registration team (JWT).
    // Turned on when Jwt:Key is set (App Service setting Jwt__Key).
    // When it is not set (local runs, CI tests), the Manager policy lets every call through,
    // so nothing changes until Registration's key is configured.
    public static bool AddFinanceAuth(this IServiceCollection services, IConfiguration config)
    {
        string? key = config["Jwt:Key"];
        bool enabled = !string.IsNullOrWhiteSpace(key);

        if (enabled)
        {
            string? issuer = config["Jwt:Issuer"];
            string? audience = config["Jwt:Audience"];

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!)),
                        ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
                        ValidIssuer = issuer,
                        ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                        ValidAudience = audience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                });
        }
        else
        {
            services.AddAuthentication();
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.Manager, policy =>
            {
                if (enabled)
                    policy.RequireAuthenticatedUser().RequireRole("Manager");
                else
                    policy.RequireAssertion(_ => true);
            });
        });

        // 401 and 403 use the class error format and are logged as security events (BF-22).
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiErrorAuthorizationResultHandler>();

        return enabled;
    }
}

public class ApiErrorAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged || authorizeResult.Forbidden)
        {
            bool forbidden = authorizeResult.Forbidden;

            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Finance.Security");

            logger.LogWarning(
                "Security: {Outcome} on {Method} {Path} by {User}",
                forbidden ? "403 forbidden" : "401 unauthorized",
                context.Request.Method,
                context.Request.Path.Value,
                context.User.Identity?.Name ?? "anonymous");

            context.Response.StatusCode = forbidden
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status401Unauthorized;

            if (!forbidden)
                context.Response.Headers.WWWAuthenticate = "Bearer";

            await context.Response.WriteAsJsonAsync(new ApiError(
                forbidden ? "forbidden" : "unauthorized",
                forbidden
                    ? "Only a Finance Manager can do this."
                    : "Log in first: send the Bearer token from Registration.",
                context.TraceIdentifier));
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
