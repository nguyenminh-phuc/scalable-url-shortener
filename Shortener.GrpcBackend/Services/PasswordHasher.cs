using Microsoft.AspNetCore.Identity;
using Shortener.GrpcBackend.Data;

namespace Shortener.GrpcBackend.Services;

public sealed class PasswordHasher : IPasswordHasher<User>
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(User user, string password) => _hasher.HashPassword(user, password);

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword) =>
        _hasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
}
