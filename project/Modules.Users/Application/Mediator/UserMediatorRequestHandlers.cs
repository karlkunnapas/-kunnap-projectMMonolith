using Mediator;
using Modules.Users.Application.Services;
using Shared.Contracts.Users;
using Shared.Contracts.Users.Mediator;

namespace Modules.Users.Application.Mediator;

internal sealed class GetUserIdByEmailQueryHandler : IQueryHandler<GetUserIdByEmailQuery, Guid?>
{
    private readonly IUsersApplicationService _users;

    public GetUserIdByEmailQueryHandler(IUsersApplicationService users)
    {
        _users = users;
    }

    public ValueTask<Guid?> Handle(GetUserIdByEmailQuery query, CancellationToken cancellationToken)
        => new(_users.GetUserIdByEmailAsync(query.Email, cancellationToken));
}

internal sealed class RegisterBasicUserCommandHandler : ICommandHandler<RegisterBasicUserCommand, RegisterBasicUserResultContract>
{
    private readonly IUsersApplicationService _users;

    public RegisterBasicUserCommandHandler(IUsersApplicationService users)
    {
        _users = users;
    }

    public ValueTask<RegisterBasicUserResultContract> Handle(RegisterBasicUserCommand command, CancellationToken cancellationToken)
        => new(_users.RegisterBasicUserAsync(new RegisterBasicUserContract
        {
            Email = command.Email,
            Password = command.Password
        }, cancellationToken));
}

internal sealed class UpdateUserProfileCommandHandler : ICommandHandler<UpdateUserProfileCommand, bool>
{
    private readonly IUsersApplicationService _users;

    public UpdateUserProfileCommandHandler(IUsersApplicationService users)
    {
        _users = users;
    }

    public ValueTask<bool> Handle(UpdateUserProfileCommand command, CancellationToken cancellationToken)
        => new(_users.UpdateUserProfileAsync(command.UserId, new UpdateUserProfileContract
        {
            FirstName = command.FirstName,
            LastName = command.LastName,
            PhoneNumber = command.PhoneNumber
        }, cancellationToken));
}
