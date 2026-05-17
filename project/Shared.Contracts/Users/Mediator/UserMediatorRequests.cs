using Mediator;

namespace Shared.Contracts.Users.Mediator;

public sealed record GetUserIdByEmailQuery(string Email) : IQuery<Guid?>;

public sealed record RegisterBasicUserCommand(string Email, string Password) : ICommand<RegisterBasicUserResultContract>;

public sealed record UpdateUserProfileCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string PhoneNumber) : ICommand<bool>;
