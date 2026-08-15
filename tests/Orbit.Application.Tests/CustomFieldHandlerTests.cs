using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Configuration;
using Orbit.Domain.Access;
using Orbit.Domain.Configuration;

namespace Orbit.Application.Tests;

public sealed class CustomFieldHandlerTests
{
    [Fact]
    public async Task Create_PersistsNewFieldForTenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new RepositoryStub();
        var unitOfWork = new UnitOfWorkStub();
        var handler = new CreateCustomFieldHandler(
            new TenantContextStub(tenantId), new AuthorizationStub(true), repository, unitOfWork, TimeProvider.System);

        var result = await handler.Handle(
            new CreateCustomFieldCommand("Severity", "Severity", CustomFieldType.Text, false, 10),
            CancellationToken.None);

        Assert.Equal("severity", result.Key);
        Assert.Single(repository.Definitions);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_RejectsDuplicateKey()
    {
        var tenantId = Guid.NewGuid();
        var repository = new RepositoryStub();
        repository.Definitions.Add(
            CustomFieldDefinition.Create(tenantId, "severity", "Severity", CustomFieldType.Text, false, 10, DateTimeOffset.UtcNow));
        var handler = new CreateCustomFieldHandler(
            new TenantContextStub(tenantId), new AuthorizationStub(true), repository, new UnitOfWorkStub(), TimeProvider.System);

        var action = () => handler.Handle(
            new CreateCustomFieldCommand("Severity", "Severity level", CustomFieldType.Text, false, 20),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(action);
    }

    [Fact]
    public async Task Create_RejectsWorkspaceMember()
    {
        var handler = new CreateCustomFieldHandler(
            new TenantContextStub(Guid.NewGuid()),
            new AuthorizationStub(false),
            new RepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateCustomFieldCommand("severity", "Severity", CustomFieldType.Text, false, 0),
            CancellationToken.None);

        await Assert.ThrowsAsync<AccessDeniedException>(action);
    }

    [Fact]
    public async Task List_ReturnsTenantFieldsInOrder()
    {
        var tenantId = Guid.NewGuid();
        var repository = new RepositoryStub();
        repository.Definitions.Add(
            CustomFieldDefinition.Create(tenantId, "b-field", "B Field", CustomFieldType.Text, false, 20, DateTimeOffset.UtcNow));
        repository.Definitions.Add(
            CustomFieldDefinition.Create(tenantId, "a-field", "A Field", CustomFieldType.Text, false, 10, DateTimeOffset.UtcNow));
        var handler = new ListCustomFieldsHandler(new TenantContextStub(tenantId), repository);

        var result = await handler.Handle(new ListCustomFieldsQuery(), CancellationToken.None);

        Assert.Equal(["a-field", "b-field"], result.Select(field => field.Key));
    }

    [Fact]
    public async Task Update_PersistsVersionedChangeWithoutTouchingKey()
    {
        var tenantId = Guid.NewGuid();
        var definition = CustomFieldDefinition.Create(
            tenantId, "severity", "Severity", CustomFieldType.Text, false, 10, DateTimeOffset.UtcNow);
        var repository = new RepositoryStub();
        repository.Definitions.Add(definition);
        var unitOfWork = new UnitOfWorkStub();
        var handler = new UpdateCustomFieldHandler(
            new TenantContextStub(tenantId), new AuthorizationStub(true), repository, unitOfWork, TimeProvider.System);

        var result = await handler.Handle(
            new UpdateCustomFieldCommand(definition.Id, "Severity level", true, 15, false, definition.Version),
            CancellationToken.None);

        Assert.Equal("severity", result.Key);
        Assert.Equal("Severity level", result.Label);
        Assert.False(result.Enabled);
        Assert.Equal(2, result.Version);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_RejectsStaleVersion()
    {
        var tenantId = Guid.NewGuid();
        var definition = CustomFieldDefinition.Create(
            tenantId, "severity", "Severity", CustomFieldType.Text, false, 10, DateTimeOffset.UtcNow);
        var repository = new RepositoryStub();
        repository.Definitions.Add(definition);
        var handler = new UpdateCustomFieldHandler(
            new TenantContextStub(tenantId), new AuthorizationStub(true), repository, new UnitOfWorkStub(), TimeProvider.System);

        var action = () => handler.Handle(
            new UpdateCustomFieldCommand(definition.Id, "Severity level", true, 15, true, definition.Version + 1),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(action);
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class AuthorizationStub(bool allowed) : ITenantAuthorization
    {
        public bool CanCreateProject() => allowed;
        public bool CanCreateMembership(TenantRole role) => allowed;
        public bool CanManageTeams() => allowed;
    }

    private sealed class RepositoryStub : ICustomFieldRepository
    {
        public List<CustomFieldDefinition> Definitions { get; } = [];

        public Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken)
        {
            Definitions.Add(definition);
            return Task.CompletedTask;
        }

        public Task<CustomFieldDefinition?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Definitions.SingleOrDefault(
                definition => definition.TenantId == tenantId && definition.Id == id));

        public Task<CustomFieldDefinition?> GetByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken) =>
            Task.FromResult(Definitions.SingleOrDefault(
                definition => definition.TenantId == tenantId && definition.Key == key));

        public Task<IReadOnlyList<CustomFieldDefinition>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CustomFieldDefinition>>(
                Definitions
                    .Where(definition => definition.TenantId == tenantId)
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
