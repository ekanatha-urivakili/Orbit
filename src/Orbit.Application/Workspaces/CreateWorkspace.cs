using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Workspaces;

public sealed record SiteCapabilitiesDto(bool CanCreateWorkspace);

public sealed record GetSiteCapabilitiesQuery : IQuery<SiteCapabilitiesDto>;

public sealed class GetSiteCapabilitiesHandler(
    ICurrentPrincipal principal,
    IWorkspaceProvisioningRepository repository)
    : IRequestHandler<GetSiteCapabilitiesQuery, SiteCapabilitiesDto>
{
    public async Task<SiteCapabilitiesDto> Handle(
        GetSiteCapabilitiesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        return new SiteCapabilitiesDto(
            await repository.IsSiteSuperAdministratorAsync(userId, cancellationToken));
    }
}

public sealed record CreateWorkspaceCommand(string Name) : ICommand<CreatedWorkspaceDto>;

public sealed record CreatedWorkspaceDto(
    Guid Id,
    string Slug,
    string Name,
    Guid MembershipId,
    TenantRole Role);

public sealed class CreateWorkspaceValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceValidator() =>
        RuleFor(command => command.Name).NotEmpty().Length(2, 120);
}

public sealed class CreateWorkspaceHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkspaceProvisioningRepository repository,
    TimeProvider timeProvider)
    : IRequestHandler<CreateWorkspaceCommand, CreatedWorkspaceDto>
{
    public async Task<CreatedWorkspaceDto> Handle(
        CreateWorkspaceCommand request,
        CancellationToken cancellationToken)
    {
        var userId = PrincipalGuards.RequireUser(principal);
        if (!await repository.IsSiteSuperAdministratorAsync(userId, cancellationToken))
        {
            throw new AccessDeniedException("Only a site super administrator can create workspaces.");
        }

        var now = timeProvider.GetUtcNow();
        var workspace = Workspace.Create(request.Name, now);
        if (await repository.SlugExistsAsync(workspace.Slug, cancellationToken))
        {
            throw new ConflictException("A workspace with this URL slug already exists.");
        }

        var ownerMembership = TenantMembership.CreateForUser(
            workspace.Id,
            userId,
            TenantRole.Owner,
            now);
        await repository.AddAsync(
            workspace,
            ownerMembership,
            tenantContext.TenantId,
            cancellationToken);

        return new CreatedWorkspaceDto(
            workspace.Id,
            workspace.Slug,
            workspace.Name,
            ownerMembership.Id,
            ownerMembership.Role);
    }
}
