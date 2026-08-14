using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Configuration;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;

namespace Orbit.Application.Tests;

public sealed class WorkItemTypeHandlerTests
{
    [Fact]
    public async Task List_ReturnsTenantOrderingAndAdministrationCapability()
    {
        var tenantId = Guid.NewGuid();
        var repository = new RepositoryStub(tenantId);
        var handler = new ListWorkItemTypesHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            repository);

        var result = await handler.Handle(new ListWorkItemTypesQuery(), CancellationToken.None);

        Assert.Equal(WorkItemType.Initiative, result[0].Id);
        Assert.All(result, definition => Assert.True(definition.CanAdminister));
    }

    [Fact]
    public async Task Update_PersistsVersionedRename()
    {
        var tenantId = Guid.NewGuid();
        var repository = new RepositoryStub(tenantId);
        var unitOfWork = new UnitOfWorkStub();
        var handler = new UpdateWorkItemTypeHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(true),
            repository,
            unitOfWork,
            TimeProvider.System);
        var definition = await repository.GetAsync(tenantId, WorkItemType.Bug, CancellationToken.None);

        var result = await handler.Handle(
            new UpdateWorkItemTypeCommand(
                WorkItemType.Bug,
                "Defect",
                "Unexpected behaviour.",
                55,
                "red",
                true,
                definition!.Version),
            CancellationToken.None);

        Assert.Equal("Defect", result.Label);
        Assert.Equal(2, result.Version);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_RejectsWorkspaceMember()
    {
        var tenantId = Guid.NewGuid();
        var handler = new UpdateWorkItemTypeHandler(
            new TenantContextStub(tenantId),
            new AuthorizationStub(false),
            new RepositoryStub(tenantId),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new UpdateWorkItemTypeCommand(WorkItemType.Task, "Task", string.Empty, 30, "blue", true, 1),
            CancellationToken.None);

        await Assert.ThrowsAsync<AccessDeniedException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class AuthorizationStub(bool allowed) : ITenantAuthorization
    {
        public bool CanCreateProject() => allowed;
        public bool CanCreateMembership(TenantRole role) => allowed;
        public bool CanManageTeams() => allowed;
    }

    private sealed class RepositoryStub(Guid tenantId) : IWorkItemTypeRepository
    {
        private readonly IReadOnlyList<WorkItemTypeDefinition> definitions =
            WorkItemTypeDefinition.CreateSoftwareDefaults(tenantId, DateTimeOffset.UtcNow);

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
                definitions
                    .Where(definition => definition.TenantId == requestedTenantId)
                    .OrderBy(definition => definition.Order)
                    .ToArray());
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
