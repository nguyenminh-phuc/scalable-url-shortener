using Shortener.Shared.Entities;
using Shortener.Shared.Exceptions;
using Shortener.Shared.Grpc;
using Shortener.Shared.Services;
using UserServiceClient = Shortener.Shared.Grpc.UserService.UserServiceClient;

namespace Shortener.FrontendShared.Services;

public interface IUserService
{
    public Task<(long, string token)> Create(string username, string password);

    public Task<string> GenerateToken(long shardId, string username, string password);

    public Task<bool> Update(UserId id, string password);

    public Task<bool> Delete(UserId id);
}

public sealed class UserService(IGrpcClientFactory grpcClientFactory, IShardService shardService) : IUserService
{
    public async Task<(long, string token)> Create(string username, string password)
    {
        long? shardId = shardService.GetShardIdForNewUser();
        if (shardId is null)
        {
            throw new ResourceExhaustedException(nameof(shardId), "No shard available");
        }

        UserServiceClient client = grpcClientFactory.GetUserClient((long)shardId);

        CreateUserRequest request = new() { User = new User { Username = username, Password = password } };
        CreateUserReply reply = await client.CreateAsync(request);

        return ((long)shardId, reply.Token.Token_);
    }

    public async Task<string> GenerateToken(long shardId, string username, string password)
    {
        UserServiceClient client = grpcClientFactory.GetUserClient(shardId);

        GenerateTokenRequest request = new() { User = new User { Username = username, Password = password } };
        GenerateTokenReply reply = await client.GenerateTokenAsync(request);

        return reply.Token.Token_;
    }

    public async Task<bool> Update(UserId id, string password)
    {
        UserServiceClient client = grpcClientFactory.GetUserClient(id.ShardId);

        UpdateUserRequest request = new() { UserId = id.Id, Password = password };
        UpdateUserReply reply = await client.UpdateAsync(request);

        return reply.Success;
    }

    public async Task<bool> Delete(UserId id)
    {
        UserServiceClient client = grpcClientFactory.GetUserClient(id.ShardId);

        DeleteUserRequest request = new() { UserId = id.Id };
        DeleteUserReply reply = await client.DeleteAsync(request);

        return reply.Success;
    }
}
