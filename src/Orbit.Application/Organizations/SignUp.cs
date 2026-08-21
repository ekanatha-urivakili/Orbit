using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Identity;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Organizations;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Organizations;

public sealed record SignUpCommand(
    string DisplayName,
    string Email,
    string Password,
    string OrganizationName,
    string WorkspaceName,
    string? UserAgent,
    string? IpAddress) : ICommand<AuthSessionDto>;

public sealed class SignUpValidator : AbstractValidator<SignUpCommand>
{
    public SignUpValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().Length(2, 120);
        RuleFor(command => command.Email).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(command => command.Password)
            .NotEmpty()
            .Length(12, 128)
            .Must(password => password.Any(char.IsLower))
            .WithMessage("Password must contain a lowercase letter.")
            .Must(password => password.Any(char.IsUpper))
            .WithMessage("Password must contain an uppercase letter.")
            .Must(password => password.Any(char.IsDigit))
            .WithMessage("Password must contain a number.");
        RuleFor(command => command.OrganizationName).NotEmpty().Length(2, 120);
        RuleFor(command => command.WorkspaceName).NotEmpty().Length(2, 120);
    }
}

public sealed class SignUpHandler(
    ISignUpRepository repository,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer tokenIssuer,
    TimeProvider timeProvider) : IRequestHandler<SignUpCommand, AuthSessionDto>
{
    public async Task<AuthSessionDto> Handle(SignUpCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = UserAccount.NormalizeEmail(request.Email);
        if (await repository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var account = UserAccount.Create(request.Email, request.DisplayName, now);
        var passwordHash = await passwordHasher.HashAsync(request.Password, cancellationToken);
        var credential = LocalCredential.Create(
            account.Id,
            passwordHash.Value,
            passwordHash.Algorithm,
            passwordHash.ParametersVersion,
            now);

        var organization = Organization.Create(request.OrganizationName, now);
        var workspace = Workspace.Create(organization.Id, request.WorkspaceName, now);
        var organizationMembership = OrganizationMembership.Create(
            organization.Id,
            account.Id,
            OrganizationRole.Owner,
            now);
        var ownerMembership = TenantMembership.CreateForUser(
            workspace.Id,
            account.Id,
            TenantRole.Owner,
            now);

        var refreshToken = RefreshTokenCodec.GenerateToken();
        var session = RefreshSession.CreateInitial(
            account.Id,
            workspace.Id,
            RefreshTokenCodec.Hash(refreshToken),
            request.UserAgent,
            request.IpAddress,
            isPersistent: false,
            now,
            tokenIssuer.RefreshTokenLifetime);

        await repository.AddAsync(
            account,
            credential,
            organization,
            workspace,
            organizationMembership,
            ownerMembership,
            session,
            Role.SeedSystemRoles(workspace.Id, now),
            cancellationToken);

        var accessToken = tokenIssuer.IssueUserToken(account.Id, workspace.Id, session.Id, now);
        return new AuthSessionDto(
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshToken,
            session.ExpiresAt,
            session.Id,
            account.Id,
            account.DisplayName,
            account.NormalizedEmail,
            workspace.Id,
            workspace.Slug,
            workspace.Name,
            ownerMembership.Role);
    }
}
