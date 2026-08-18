using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
using Orbit.Domain.Projects;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class CreateWorkItemHandlerTests
{
    [Fact]
    public async Task Handle_AllocatesProjectSequenceAndPersistsItem()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var projects = new ProjectRepositoryStub(project);
        var workItems = new WorkItemRepositoryStub();
        var unitOfWork = new UnitOfWorkStub();
        var handler = new CreateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            projects,
            new WorkItemTypeRepositoryStub(tenantId),
            workItems,
            unitOfWork,
            TimeProvider.System);

        var result = await handler.Handle(
            new CreateWorkItemCommand(project.Id, "Build the board", null, WorkItemType.Story, Priority.High),
            CancellationToken.None);

        Assert.Equal("ORB-1", result.Key);
        Assert.NotNull(workItems.Added);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_RejectsDisabledWorkspaceItemType()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var handler = new CreateWorkItemHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new ProjectRepositoryStub(project),
            new WorkItemTypeRepositoryStub(tenantId, WorkItemType.Story),
            new WorkItemRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateWorkItemCommand(project.Id, "Build the board", null, WorkItemType.Story, Priority.High),
            CancellationToken.None);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class CurrentPrincipalStub : ICurrentPrincipal
    {
        public Guid? UserId => null;
        public Guid? SessionId => null;
        public Guid MembershipId => Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Owner;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => true;
    }

    private sealed class ProjectRepositoryStub(Project project) : IProjectRepository
    {
        public Task AddAsync(Project value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Project?> GetAsync(
            Guid tenantId,
            Guid projectId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<Project?>(project.Id == projectId && project.TenantId == tenantId ? project : null);
        public Task<PagedResult<Project>> ListAsync(
            Guid tenantId,
            ProjectPermission permission,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Project>([project], 1));
    }

    private sealed class WorkItemRepositoryStub : IWorkItemRepository
    {
        public WorkItem? Added { get; private set; }
        public Task AddAsync(WorkItem workItem, CancellationToken cancellationToken)
        {
            Added = workItem;
            return Task.CompletedTask;
        }
        public Task<WorkItem?> GetAsync(
            Guid tenantId,
            Guid workItemId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkItem?>(null);
        public Task<PagedResult<WorkItem>> ListByProjectAsync(
            Guid tenantId,
            Guid projectId,
            ProjectPermission permission,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WorkItem>([], 0));
        public Task<IReadOnlyList<WorkItem>> ListByIdsAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> workItemIds,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItem>>([]);
    }

    private sealed class WorkItemTypeRepositoryStub : IWorkItemTypeRepository
    {
        private readonly IReadOnlyList<WorkItemTypeDefinition> definitions;

        public WorkItemTypeRepositoryStub(Guid tenantId, WorkItemType? disabled = null)
        {
            definitions = WorkItemTypeDefinition.CreateSoftwareDefaults(tenantId, DateTimeOffset.UtcNow);
            if (disabled.HasValue)
            {
                var definition = definitions.Single(itemType => itemType.Id == disabled.Value);
                definition.Update(
                    definition.Label,
                    definition.Description,
                    definition.Order,
                    definition.ColorToken,
                    false,
                    DateTimeOffset.UtcNow);
            }
        }

        public Task<WorkItemTypeDefinition?> GetAsync(
            Guid requestedTenantId,
            WorkItemType id,
            CancellationToken cancellationToken) =>
            Task.FromResult(definitions.SingleOrDefault(
                definition => definition.TenantId == requestedTenantId && definition.Id == id));

        public Task<IReadOnlyList<WorkItemTypeDefinition>> ListAsync(
            Guid requestedTenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemTypeDefinition>>(
                definitions.Where(definition => definition.TenantId == requestedTenantId).ToArray());
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
