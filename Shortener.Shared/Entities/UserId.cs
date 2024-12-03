using System.Diagnostics.CodeAnalysis;

namespace Shortener.Shared.Entities;

public sealed record UserId(long ShardId, int Id)
{
    public override string ToString() => ShardId.ToString() + Constants.JwtSubSeparator + Id;

    public static bool TryParse(string jwtSub, [NotNullWhen(true)] out UserId? id)
    {
        id = default;

        string[] substrings = jwtSub.Split(Constants.JwtSubSeparator);
        if (substrings.Length != 2)
        {
            return false;
        }

        if (!long.TryParse(substrings[0], out long shardId))
        {
            return false;
        }

        if (!int.TryParse(substrings[1], out int userId))
        {
            return false;
        }

        id = new UserId(shardId, userId);

        return true;
    }
}
