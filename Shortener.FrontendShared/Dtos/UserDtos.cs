namespace Shortener.FrontendShared.Dtos;

public sealed class User
{
    public required long ShardId { get; init; }

    public required string Username { get; init; }

    public required string Token { get; init; }
}

public sealed class CreateUserInput
{
    public required string Username { get; init; }

    public required string Password { get; init; }
}

public sealed class GenerateTokenInput
{
    public required long ShardId { get; init; }

    public required string Username { get; init; }

    public required string Password { get; init; }
}
