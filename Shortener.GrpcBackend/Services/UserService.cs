using Grpc.Core;
using Microsoft.AspNetCore.Identity;
using Shortener.GrpcBackend.Repositories;
using Shortener.Shared.Entities;
using Shortener.Shared.Exceptions;
using Shortener.Shared.Grpc;
using Shortener.Shared.Services;
using User = Shortener.GrpcBackend.Data.User;
using UserServiceBase = Shortener.Shared.Grpc.UserService.UserServiceBase;

namespace Shortener.GrpcBackend.Services;

public sealed class UserService(
    IJwtService jwtService,
    IPasswordHasher<User> passwordHasher,
    IShardService shardService,
    IUserRepository userRepository) : UserServiceBase
{
    public override async Task<CreateUserReply> Create(CreateUserRequest request, ServerCallContext context)
    {
        if (shardService.Type != ShardType.ReadWrite)
        {
            throw RpcExceptionUtils.ResourceExhausted(nameof(UserId));
        }

        User? user = null;
        string hashedPassword = passwordHasher.HashPassword(user!, request.User.Password);
        user = await userRepository.Add(request.User.Username, hashedPassword, context.CancellationToken);

        UserId userId = new(shardService.Id, user.Id);
        string token = jwtService.Generate(userId, user.Username);
        return new CreateUserReply { Token = new Token { Username = user.Username, Token_ = token } };
    }

    public override async Task<GenerateTokenReply> GenerateToken(
        GenerateTokenRequest request,
        ServerCallContext context)
    {
        User? user = await userRepository.Get(request.User.Username, context.CancellationToken);
        if (user is null)
        {
            throw RpcExceptionUtils.Unauthenticated(new Dictionary<string, string>
            {
                { nameof(request.User.Username), request.User.Username }
            });
        }

        PasswordVerificationResult result = passwordHasher.VerifyHashedPassword(
            user,
            user.HashedPassword, request.User.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw RpcExceptionUtils.Unauthenticated(new Dictionary<string, string>
            {
                { nameof(request.User.Username), request.User.Username }
            });
        }

        UserId userId = new(shardService.Id, user.Id);
        string token = jwtService.Generate(userId, user.Username);

        return new GenerateTokenReply { Token = new Token { Username = user.Username, Token_ = token } };
    }

    public override async Task<UpdateUserReply> Update(UpdateUserRequest request, ServerCallContext context)
    {
        User? user = null;
        string hashedPassword = passwordHasher.HashPassword(user!, request.Password);

        bool success = await userRepository.Update(request.UserId, hashedPassword, context.CancellationToken);

        return new UpdateUserReply { Success = success };
    }

    public override async Task<DeleteUserReply> Delete(DeleteUserRequest request, ServerCallContext context)
    {
        bool success = await userRepository.Delete(request.UserId, context.CancellationToken);
        return new DeleteUserReply { Success = success };
    }
}
