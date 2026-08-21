using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Workspaces;
using Orbit.Domain.Access;
using Orbit.Domain.Organizations;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class WorkspaceProvisioningHandlerTests
{
    [Fact]
    public async Task CreateWorkspace_CreatesWorkspaceAndOwnerMembership()
    {
        var userId = Guid.NewGuid();
        var currentTenantId = Guid.NewGuid();
        var repository = new WorkspaceProvisioningRepositoryStub { IsSiteSuperAdministrator = true };
        var handler = new CreateWorkspaceHandler(
            new TenantContextStub(currentTenantId),
            new PrincipalStub(userId),
            repository,
            TimeProvider.System);

        var result = await handler.Handle(
            new CreateWorkspaceCommand("Platform Delivery"),
            CancellationToken.None);

        Assert.Equal("platform-delivery", result.Slug);
        Assert.Equal(TenantRole.Owner, result.Role);
        Assert.Equal(result.Id, repository.Workspace!.Id);
        Assert.Equal(result.Id, repository.OwnerMembership!.TenantId);
        Assert.Equal(userId, repository.OwnerMembership.UserId);
        Assert.Equal(currentTenantId, repository.CurrentTenantId);
    }

    [Fact]
    public async Task CreateWorkspace_RejectsNonSiteAdministratorBeforeCheckingSlug()
    {
        var repository = new WorkspaceProvisioningRepositoryStub();
        var handler = new CreateWorkspaceHandler(
            new TenantContextStub(Guid.NewGuid()),
            new PrincipalStub(Guid.NewGuid()),
            repository,
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateWorkspaceCommand("Platform Delivery"),
            CancellationToken.None);

        await Assert.ThrowsAsync<AccessDeniedException>(action);
        Assert.Equal(0, repository.SlugCheckCount);
        Assert.Null(repository.Workspace);
    }

    [Fact]
    public async Task CreateWorkspace_RejectsDuplicateSlug()
    {
        var repository = new WorkspaceProvisioningRepositoryStub
        {
            IsSiteSuperAdministrator = true,
            SlugExists = true
        };
        var handler = new CreateWorkspaceHandler(
            new TenantContextStub(Guid.NewGuid()),
            new PrincipalStub(Guid.NewGuid()),
            repository,
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateWorkspaceCommand("Platform Delivery"),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(action);
        Assert.Null(repository.Workspace);
    }

    [Fact]
    public async Task GetSiteCapabilities_ReflectsGlobalSiteRole()
    {
        var repository = new WorkspaceProvisioningRepositoryStub { IsSiteSuperAdministrator = true };
        var handler = new GetSiteCapabilitiesHandler(
            new PrincipalStub(Guid.NewGuid()),
            repository);

        var result = await handler.Handle(new GetSiteCapabilitiesQuery(), CancellationToken.None);

        Assert.True(result.CanCreateWorkspace);
    }

    [Fact]
    public async Task CreateWorkspaceInOrganization_AddsWorkspaceUnderCallersOrganization()
    {
        var userId = Guid.NewGuid();
        var currentTenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var repository = new WorkspaceProvisioningRepositoryStub
        {
            CallerOrganizationMembership = OrganizationMembership.Create(
                organizationId, userId, OrganizationRole.Owner, DateTimeOffset.UtcNow)
        };
        var handler = new CreateWorkspaceInOrganizationHandler(
            new TenantContextStub(currentTenantId),
            new PrincipalStub(userId),
            repository,
            TimeProvider.System);

        var result = await handler.Handle(
            new CreateWorkspaceInOrganizationCommand("Growth Team"), CancellationToken.None);

        Assert.Equal("growth-team", result.Slug);
        Assert.Equal(TenantRole.Owner, result.Role);
        Assert.Equal(organizationId, repository.Workspace!.OrganizationId);
        Assert.Equal(userId, repository.OwnerMembership!.UserId);
        Assert.Equal(currentTenantId, repository.CurrentTenantId);
    }

    [Fact]
    public async Task CreateWorkspaceInOrganization_RejectsCallerWithNoOrganizationMembership()
    {
        var repository = new WorkspaceProvisioningRepositoryStub();
        var handler = new CreateWorkspaceInOrganizationHandler(
            new TenantContextStub(Guid.NewGuid()),
            new PrincipalStub(Guid.NewGuid()),
            repository,
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateWorkspaceInOrganizationCommand("Growth Team"), CancellationToken.None);

        await Assert.ThrowsAsync<AccessDeniedException>(action);
        Assert.Null(repository.Workspace);
    }

    [Fact]
    public async Task CreateWorkspaceInOrganization_RejectsNonOwnerOrganizationMember()
    {
        var userId = Guid.NewGuid();
        var repository = new WorkspaceProvisioningRepositoryStub
        {
            CallerOrganizationMembership = OrganizationMembership.Create(
                Guid.NewGuid(), userId, OrganizationRole.Member, DateTimeOffset.UtcNow)
        };
        var handler = new CreateWorkspaceInOrganizationHandler(
            new TenantContextStub(Guid.NewGuid()),
            new PrincipalStub(userId),
            repository,
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateWorkspaceInOrganizationCommand("Growth Team"), CancellationToken.None);

        await Assert.ThrowsAsync<AccessDeniedException>(action);
        Assert.Null(repository.Workspace);
    }

    private sealed class WorkspaceProvisioningRepositoryStub : IWorkspaceProvisioningRepository
    {
        public bool IsSiteSuperAdministrator { get; init; }
        public bool SlugExists { get; init; }
        public int SlugCheckCount { get; private set; }
        public OrganizationMembership? CallerOrganizationMembership { get; init; }
        public Organization? Organization { get; private set; }
        public Workspace? Workspace { get; private set; }
        public OrganizationMembership? OrganizationMembership { get; private set; }
        public TenantMembership? OwnerMembership { get; private set; }
        public Guid CurrentTenantId { get; private set; }

        public Task<bool> IsSiteSuperAdministratorAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(IsSiteSuperAdministrator);

        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken)
        {
            SlugCheckCount++;
            return Task.FromResult(SlugExists);
        }

        public Task AddAsync(
            Organization organization,
            Workspace workspace,
            OrganizationMembership organizationMembership,
            TenantMembership ownerMembership,
            IReadOnlyList<Role> systemRoles,
            Guid currentTenantId,
            CancellationToken cancellationToken)
        {
            Organization = organization;
            Workspace = workspace;
            OrganizationMembership = organizationMembership;
            OwnerMembership = ownerMembership;
            CurrentTenantId = currentTenantId;
            return Task.CompletedTask;
        }

        public Task<OrganizationMembership?> GetOrganizationMembershipAsync(
            Guid workspaceTenantId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(CallerOrganizationMembership);

        public Task AddWorkspaceToOrganizationAsync(
            Workspace workspace,
            TenantMembership ownerMembership,
            IReadOnlyList<Role> systemRoles,
            Guid currentTenantId,
            CancellationToken cancellationToken)
        {
            Workspace = workspace;
            OwnerMembership = ownerMembership;
            CurrentTenantId = currentTenantId;
            return Task.CompletedTask;
        }
    }

    private sealed class TenantContextStub(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
    }

    private sealed class PrincipalStub(Guid userId) : ICurrentPrincipal
    {
        public Guid? UserId { get; } = userId;
        public Guid? SessionId => null;
        public Guid MembershipId => Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Owner;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => false;
    }
}
