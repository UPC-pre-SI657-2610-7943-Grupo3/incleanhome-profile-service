using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace InCleanHome.ProfileService.Infrastructure.Pipeline;

/// <summary>
/// Marks an endpoint as accessible without a valid JWT.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AllowAnonymousAttribute : Attribute { }

/// <summary>
/// Lightweight authenticated-user info extracted from the JWT.
/// Profile Service does not own the User aggregate (IAM does), so we only
/// extract the user id and role from the token's claims. If we need more
/// data we would call IAM Service via HTTP.
/// </summary>
public record AuthenticatedUser(int UserId, string Role)
{
    public bool IsClient() => string.Equals(Role, "client", StringComparison.OrdinalIgnoreCase);
    public bool IsWorker() => string.Equals(Role, "worker", StringComparison.OrdinalIgnoreCase);
    public bool IsAdmin()  => string.Equals(Role, "admin",  StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Validates the JWT signature/issuer/audience/expiration and extracts the user id and role.
/// On success, stores an <see cref="AuthenticatedUser"/> in <c>HttpContext.Items["User"]</c>.
/// Endpoints decorated with <see cref="AllowAnonymousAttribute"/> bypass this check.
/// </summary>
public class JwtAuthMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path == "/" ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }
        
        var endpoint = context.Request.HttpContext.GetEndpoint();
        var allowAnonymous = endpoint?.Metadata
            .Any(m => m.GetType() == typeof(AllowAnonymousAttribute)) ?? false;

        if (allowAnonymous)
        {
            await next(context);
            return;
        }

        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid token" });
            return;
        }

        var jwtKey      = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY") ?? string.Empty;
        var jwtIssuer   = configuration["Jwt:Issuer"]   ?? "incleanhome";
        var jwtAudience = configuration["Jwt:Audience"] ?? "incleanhome-api";

        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "JWT_SIGNING_KEY is not configured on the server" });
            return;
        }

        var key     = Encoding.UTF8.GetBytes(jwtKey);
        var handler = new JsonWebTokenHandler();

        try
        {
            var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = new SymmetricSecurityKey(key),
                ValidateIssuer           = true,
                ValidIssuer              = jwtIssuer,
                ValidateAudience         = true,
                ValidAudience            = jwtAudience,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.FromMinutes(1)
            });

            if (!result.IsValid)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid token" });
                return;
            }

            var jwt    = (JsonWebToken)result.SecurityToken;
            var sidStr = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Sid)?.Value;
            var role   = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? string.Empty;

            if (!int.TryParse(sidStr, out var userId))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Malformed token (no user id)" });
                return;
            }

            context.Items["User"] = new AuthenticatedUser(userId, role);
            await next(context);
        }
        catch (Exception)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Token validation failed" });
        }
    }
}

public static class JwtAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseJwtAuth(this IApplicationBuilder builder)
        => builder.UseMiddleware<JwtAuthMiddleware>();
}
