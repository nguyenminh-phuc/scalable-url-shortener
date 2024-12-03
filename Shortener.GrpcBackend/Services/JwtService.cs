using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Shortener.Shared.Entities;

namespace Shortener.GrpcBackend.Services;

public interface IJwtService
{
    string Issuer { get; }

    string Audience { get; }

    SymmetricSecurityKey SecretKey { get; }

    string Generate(UserId userId, string username);
}

public sealed class JwtService : IJwtService
{
    private const uint DefaultExpirationMinutes = 1440;

    private readonly SigningCredentials _credentials;
    private readonly uint _expirationMinutes;

    public JwtService(IConfiguration configuration)
    {
        _expirationMinutes = configuration.GetValue("JWT_EXPIRATION_MINUTES", DefaultExpirationMinutes);

        string? issuer = configuration["JWT_ISSUER"];
        if (string.IsNullOrEmpty(issuer))
        {
            throw new Exception("JWT_ISSUER is required");
        }

        string? audience = configuration["JWT_AUDIENCE"];
        if (string.IsNullOrEmpty(audience))
        {
            throw new Exception("JWT_AUDIENCE is required");
        }

        string? key = configuration["JWT_SECRET_KEY"];
        if (string.IsNullOrEmpty(key) || key.Length < 32)
        {
            throw new Exception("JWT_SECRET_KEY must be at least 256 bits long");
        }

        Issuer = issuer;
        Audience = audience;
        SecretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

        _credentials = new SigningCredentials(SecretKey, SecurityAlgorithms.HmacSha256);
    }

    public string Issuer { get; }

    public string Audience { get; }

    public SymmetricSecurityKey SecretKey { get; }

    public string Generate(UserId userId, string username)
    {
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username)
        ];

        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = _credentials,
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(_expirationMinutes)
        };

        JwtSecurityTokenHandler handler = new();
        SecurityToken? token = handler.CreateToken(descriptor);

        return handler.WriteToken(token);
    }
}
