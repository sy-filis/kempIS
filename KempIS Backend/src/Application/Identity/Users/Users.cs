using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using FluentValidation;
using SharedKernel;

namespace Application.Identity.Users;

public sealed record CreateUserCommand(string Username, string Name, string Role) : ICommand<Guid>;

internal sealed class CreateUserCommandHandler(IIdentityService identity)
  : ICommandHandler<CreateUserCommand, Guid>
{
  public Task<Result<Guid>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    => identity.CreateUserAsync(command.Username, command.Name, command.Role, cancellationToken);
}

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
  public CreateUserCommandValidator()
  {
    RuleFor(c => c.Username)
      .NotEmpty()
      .MaximumLength(256);

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(256);

    RuleFor(c => c.Role)
      .NotEmpty()
      .Must(Roles.All.Contains).WithMessage("Role must be one of the supported roles.");
  }
}

public sealed record ListUsersQuery(bool IncludeDisabled, string? Role)
  : IQuery<IReadOnlyList<UserSummary>>;

internal sealed class ListUsersQueryHandler(IIdentityService identity)
  : IQueryHandler<ListUsersQuery, IReadOnlyList<UserSummary>>
{
  public Task<Result<IReadOnlyList<UserSummary>>> Handle(ListUsersQuery query, CancellationToken cancellationToken)
    => identity.ListUsersAsync(query.IncludeDisabled, query.Role, cancellationToken);
}

public sealed record GetUserQuery(Guid Id) : IQuery<UserDetail>;

internal sealed class GetUserQueryHandler(IIdentityService identity)
  : IQueryHandler<GetUserQuery, UserDetail>
{
  public Task<Result<UserDetail>> Handle(GetUserQuery query, CancellationToken cancellationToken)
    => identity.GetUserAsync(query.Id, cancellationToken);
}

public sealed record UpdateUserCommand(
  Guid Id,
  string Username,
  string Name,
  IReadOnlyList<string> Roles) : ICommand;

internal sealed class UpdateUserCommandHandler(IIdentityService identity)
  : ICommandHandler<UpdateUserCommand>
{
  public Task<Result> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    => identity.UpdateUserAsync(command.Id, command.Username, command.Name, command.Roles, cancellationToken);
}

internal sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
  public UpdateUserCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();

    RuleFor(c => c.Username)
      .NotEmpty()
      .MaximumLength(256);

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(256);

    RuleFor(c => c.Roles)
      .NotEmpty()
      .Must(roles => roles.Distinct().Count() == roles.Count)
      .WithMessage("Roles must be unique.");

    RuleForEach(c => c.Roles)
      .Must(Application.Abstractions.Authentication.Roles.All.Contains)
      .WithMessage("Role must be one of the supported roles.");
  }
}

public sealed record DisableUserCommand(Guid Id) : ICommand;

internal sealed class DisableUserCommandHandler(IIdentityService identity)
  : ICommandHandler<DisableUserCommand>
{
  public Task<Result> Handle(DisableUserCommand command, CancellationToken cancellationToken)
    => identity.DisableUserAsync(command.Id, cancellationToken);
}

public sealed record ListPasskeysQuery(Guid UserId) : IQuery<IReadOnlyList<PasskeySummary>>;

internal sealed class ListPasskeysQueryHandler(IIdentityService identity)
  : IQueryHandler<ListPasskeysQuery, IReadOnlyList<PasskeySummary>>
{
  public Task<Result<IReadOnlyList<PasskeySummary>>> Handle(ListPasskeysQuery query, CancellationToken cancellationToken)
    => identity.ListPasskeysAsync(query.UserId, cancellationToken);
}

public sealed record RevokePasskeyCommand(Guid UserId, Guid PasskeyId) : ICommand;

internal sealed class RevokePasskeyCommandHandler(IIdentityService identity)
  : ICommandHandler<RevokePasskeyCommand>
{
  public Task<Result> Handle(RevokePasskeyCommand command, CancellationToken cancellationToken)
    => identity.RevokePasskeyAsync(command.UserId, command.PasskeyId, cancellationToken);
}
