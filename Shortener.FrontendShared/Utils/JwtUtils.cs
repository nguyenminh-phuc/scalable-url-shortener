using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Shortener.FrontendShared.Utils;

public static class JwtUtils
{
    public static void Configure(WebApplicationBuilder builder)
    {
        string? key = builder.Configuration["JWT_SECRET_KEY"];
        if (string.IsNullOrEmpty(key))
        {
            throw new Exception("JWT_SECRET_KEY is required");
        }

        string? issuer = builder.Configuration["JWT_ISSUER"];
        if (string.IsNullOrEmpty(issuer))
        {
            throw new Exception("JWT_ISSUER is required");
        }

        string? audience = builder.Configuration["JWT_AUDIENCE"];
        if (string.IsNullOrEmpty(audience))
        {
            throw new Exception("JWT_AUDIENCE is required");
        }

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateAudience = true,
                        ValidAudience = audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                        ValidateLifetime = true
                    };
            });
    }
}
