using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Identity;
using Orbit.Domain.Organizations;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Identity;

public sealed record GetBootstrapStatusQuery : IQuery<BootstrapStatusDto>;

public sealed record BootstrapStatusDto(bool InitializationRequired);

public sealed record BootstrapCommand(
    string DisplayName,
    string Email,
    string Password,
    string WorkspaceName) : ICommand<BootstrapResultDto>;

public sealed record BootstrapResultDto(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid WorkspaceId,
    string WorkspaceSlug,
    string WorkspaceName,
    Guid MembershipId);

public sealed class BootstrapValidator : AbstractValidator<BootstrapCommand>
{
    public BootstrapValidator()
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
        RuleFor(command => command.WorkspaceName).NotEmpty().Length(2, 120);
    }
}

public sealed class GetBootstrapStatusHandler(IBootstrapRepository bootstrapRepository)
    : IRequestHandler<GetBootstrapStatusQuery, BootstrapStatusDto>
{
    public async Task<BootstrapStatusDto> Handle(
        GetBootstrapStatusQuery request,
        CancellationToken cancellationToken) =>
        new(await bootstrapRepository.IsInitializationRequiredAsync(cancellationToken));
}

public sealed class BootstrapHandler(
    IBootstrapRepository bootstrapRepository,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
    : IRequestHandler<BootstrapCommand, BootstrapResultDto>
{
    public async Task<BootstrapResultDto> Handle(
        BootstrapCommand request,
        CancellationToken cancellationToken)
    {
        if (!await bootstrapRepository.IsInitializationRequiredAsync(cancellationToken))
        {
            throw new ConflictException("This ORBIT installation has already been initialized.");
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
        var siteRole = SiteRoleAssignment.CreateSuperAdministrator(account.Id, now);
        var organization = Organization.Create(request.WorkspaceName, now);
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

        var initialized = await bootstrapRepository.TryInitializeAsync(
            account,
            credential,
            siteRole,
            organization,
            workspace,
            organizationMembership,
            ownerMembership,
            cancellationToken);
        if (!initialized)
        {
            throw new ConflictException("This ORBIT installation has already been initialized.");
        }

        return new BootstrapResultDto(
            account.Id,
            account.NormalizedEmail,
            account.DisplayName,
            workspace.Id,
            workspace.Slug,
            workspace.Name,
            ownerMembership.Id);
    }
}
