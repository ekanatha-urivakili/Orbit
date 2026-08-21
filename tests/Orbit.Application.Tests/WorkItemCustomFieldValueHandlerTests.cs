using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Common;
using Orbit.Domain.Configuration;
using Orbit.Domain.WorkItems;
using FluentValidation;

namespace Orbit.Application.Tests;

public sealed class WorkItemCustomFieldValueHandlerTests
{
    [Fact]
    public async Task Set_CreatesNewValue_ForFieldNotYetSet()
    {
        var tenantId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var definition = CustomFieldDefinition.Create(
            tenantId, workItem.ProjectId, "severity", "Severity", CustomFieldType.Text, false, 0, [], [], DateTimeOffset.UtcNow);
        var values = new WorkItemCustomFieldValueRepositoryStub();
        var handler = new SetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new CustomFieldRepositoryStub(definition),
            values,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new SetWorkItemCustomFieldValuesCommand(
                workItem.Id, [new CustomFieldValueInput(definition.Id, ["Critical"])]),
            CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(definition.Id, dto.CustomFieldDefinitionId);
        Assert.Equal(["Critical"], dto.Values);
        Assert.Single(values.Added);
    }

    [Fact]
    public async Task Set_RecordsHistoryEntry_UsingFieldLabelAndJoinedValues()
    {
        var tenantId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var definition = CustomFieldDefinition.Create(
            tenantId, workItem.ProjectId, "severity", "Severity", CustomFieldType.Text, false, 0, [], [],
            DateTimeOffset.UtcNow);
        var values = new WorkItemCustomFieldValueRepositoryStub();
        var history = new WorkItemHistoryRepositoryStub();
        var handler = new SetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new CustomFieldRepositoryStub(definition),
            values,
            history,
            new UnitOfWorkStub(),
            TimeProvider.System);

        await handler.Handle(
            new SetWorkItemCustomFieldValuesCommand(
                workItem.Id, [new CustomFieldValueInput(definition.Id, ["Critical"])]),
            CancellationToken.None);

        var entry = Assert.Single(history.Added);
        Assert.Equal("Severity", entry.FieldName);
        Assert.Null(entry.OldValue);
        Assert.Equal("Critical", entry.NewValue);
    }

    [Fact]
    public async Task Set_UpdatesExistingValue_InPlace()
    {
        var tenantId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var definition = CustomFieldDefinition.Create(
            tenantId, workItem.ProjectId, "severity", "Severity", CustomFieldType.Text, false, 0, [], [], DateTimeOffset.UtcNow);
        var existing = WorkItemCustomFieldValue.Create(
            tenantId, workItem.Id, definition, ["Low"], DateTimeOffset.UtcNow);
        var values = new WorkItemCustomFieldValueRepositoryStub(existing);
        var handler = new SetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new CustomFieldRepositoryStub(definition),
            values,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new SetWorkItemCustomFieldValuesCommand(
                workItem.Id, [new CustomFieldValueInput(definition.Id, ["High"])]),
            CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(["High"], dto.Values);
        Assert.Empty(values.Added);
    }

    [Fact]
    public async Task Set_RemovesRow_WhenValuesClearedToEmpty()
    {
        var tenantId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var definition = CustomFieldDefinition.Create(
            tenantId, workItem.ProjectId, "severity", "Severity", CustomFieldType.Text, false, 0, [], [], DateTimeOffset.UtcNow);
        var existing = WorkItemCustomFieldValue.Create(
            tenantId, workItem.Id, definition, ["Low"], DateTimeOffset.UtcNow);
        var values = new WorkItemCustomFieldValueRepositoryStub(existing);
        var handler = new SetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new CustomFieldRepositoryStub(definition),
            values,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new SetWorkItemCustomFieldValuesCommand(workItem.Id, [new CustomFieldValueInput(definition.Id, [])]),
            CancellationToken.None);

        Assert.Empty(result);
        Assert.Contains(existing, values.Removed);
    }

    [Fact]
    public async Task Set_RejectsEmptyValue_ForRequiredField()
    {
        var tenantId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var definition = CustomFieldDefinition.Create(
            tenantId, workItem.ProjectId, "severity", "Severity", CustomFieldType.Text, true, 0, [], [], DateTimeOffset.UtcNow);
        var values = new WorkItemCustomFieldValueRepositoryStub();
        var handler = new SetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new CustomFieldRepositoryStub(definition),
            values,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new SetWorkItemCustomFieldValuesCommand(workItem.Id, [new CustomFieldValueInput(definition.Id, [])]),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ValidationException>(action);
        Assert.Contains("Severity", exception.Message);
        Assert.Empty(values.Added);
    }

    [Fact]
    public async Task Set_AllowsRequiredField_WhenNotMentionedInRequest()
    {
        var tenantId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var definition = CustomFieldDefinition.Create(
            tenantId, workItem.ProjectId, "severity", "Severity", CustomFieldType.Text, true, 0, [], [], DateTimeOffset.UtcNow);
        var values = new WorkItemCustomFieldValueRepositoryStub();
        var handler = new SetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new CustomFieldRepositoryStub(definition),
            values,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new SetWorkItemCustomFieldValuesCommand(workItem.Id, []),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Set_RejectsValue_ForFieldNotApplicableToWorkItemType()
    {
        var tenantId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId); // WorkItemType.Story
        var definition = CustomFieldDefinition.Create(
            tenantId, workItem.ProjectId, "repro-steps", "Repro steps", CustomFieldType.Text, false, 0, [],
            [WorkItemType.Bug], DateTimeOffset.UtcNow);
        var values = new WorkItemCustomFieldValueRepositoryStub();
        var handler = new SetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new CustomFieldRepositoryStub(definition),
            values,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new SetWorkItemCustomFieldValuesCommand(
                workItem.Id, [new CustomFieldValueInput(definition.Id, ["Repro"])]),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ValidationException>(action);
        Assert.Contains("Repro steps", exception.Message);
        Assert.Empty(values.Added);
    }

    [Fact]
    public async Task Set_AllowsClearingInapplicableField_AsANoOp()
    {
        var tenantId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId); // WorkItemType.Story
        var definition = CustomFieldDefinition.Create(
            tenantId, workItem.ProjectId, "repro-steps", "Repro steps", CustomFieldType.Text, true, 0, [],
            [WorkItemType.Bug], DateTimeOffset.UtcNow);
        var values = new WorkItemCustomFieldValueRepositoryStub();
        var handler = new SetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new CustomFieldRepositoryStub(definition),
            values,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new SetWorkItemCustomFieldValuesCommand(workItem.Id, [new CustomFieldValueInput(definition.Id, [])]),
            CancellationToken.None);

        Assert.Empty(result);
        Assert.Empty(values.Added);
        Assert.Empty(values.Removed);
    }

    [Fact]
    public async Task Set_AllowsValue_ForFieldWithNoApplicableTypesRestriction()
    {
        var tenantId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId); // WorkItemType.Story
        var definition = CustomFieldDefinition.Create(
            tenantId, workItem.ProjectId, "severity", "Severity", CustomFieldType.Text, false, 0, [], [],
            DateTimeOffset.UtcNow);
        var values = new WorkItemCustomFieldValueRepositoryStub();
        var handler = new SetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new CustomFieldRepositoryStub(definition),
            values,
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new SetWorkItemCustomFieldValuesCommand(
                workItem.Id, [new CustomFieldValueInput(definition.Id, ["High"])]),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Single(values.Added);
    }

    [Fact]
    public async Task Set_RejectsUnknownField()
    {
        var tenantId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var handler = new SetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new CustomFieldRepositoryStub(),
            new WorkItemCustomFieldValueRepositoryStub(),
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new SetWorkItemCustomFieldValuesCommand(
                workItem.Id, [new CustomFieldValueInput(Guid.NewGuid(), ["x"])]),
            CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task Set_RejectsDisabledField()
    {
        var tenantId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var definition = CustomFieldDefinition.Create(
            tenantId, workItem.ProjectId, "severity", "Severity", CustomFieldType.Text, false, 0, [], [], DateTimeOffset.UtcNow);
        definition.Update("Severity", false, 0, false, [], [], DateTimeOffset.UtcNow);
        var handler = new SetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(tenantId),
            new CurrentPrincipalStub(),
            new WorkItemRepositoryStub(workItem),
            new CustomFieldRepositoryStub(definition),
            new WorkItemCustomFieldValueRepositoryStub(),
            new WorkItemHistoryRepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new SetWorkItemCustomFieldValuesCommand(
                workItem.Id, [new CustomFieldValueInput(definition.Id, ["x"])]),
            CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(action);
    }

    [Fact]
    public async Task Get_ReturnsCurrentValues()
    {
        var tenantId = Guid.NewGuid();
        var workItem = CreateWorkItem(tenantId);
        var definition = CustomFieldDefinition.Create(
            tenantId, workItem.ProjectId, "severity", "Severity", CustomFieldType.Text, false, 0, [], [], DateTimeOffset.UtcNow);
        var existing = WorkItemCustomFieldValue.Create(
            tenantId, workItem.Id, definition, ["Low"], DateTimeOffset.UtcNow);
        var handler = new GetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(tenantId),
            new WorkItemRepositoryStub(workItem),
            new WorkItemCustomFieldValueRepositoryStub(existing));

        var result = await handler.Handle(new GetWorkItemCustomFieldValuesQuery(workItem.Id), CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(["Low"], dto.Values);
    }

    [Fact]
    public async Task Get_HidesExistence_WhenWorkItemNotVisible()
    {
        var handler = new GetWorkItemCustomFieldValuesHandler(
            new TenantContextStub(Guid.NewGuid()),
            new WorkItemRepositoryStub(null),
            new WorkItemCustomFieldValueRepositoryStub());

        var action = () => handler.Handle(new GetWorkItemCustomFieldValuesQuery(Guid.NewGuid()), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    private static WorkItem CreateWorkItem(Guid tenantId) =>
        WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Build the board", null, WorkItemType.Story, Priority.High,
            Guid.NewGuid(), DateTimeOffset.UtcNow);

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class WorkItemRepositoryStub(WorkItem? workItem) : IWorkItemRepository
    {
        public Task AddAsync(WorkItem value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<WorkItem?> GetAsync(
            Guid tenantId,
            Guid workItemId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult(workItem is not null && workItem.Id == workItemId && workItem.TenantId == tenantId
                ? workItem
                : null);
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
        public Task<bool> HasChildrenAsync(Guid tenantId, Guid parentWorkItemId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task RemoveAsync(WorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CustomFieldRepositoryStub(params CustomFieldDefinition[] definitions) : ICustomFieldRepository
    {
        public Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<CustomFieldDefinition?> GetAsync(
            Guid tenantId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(definitions.SingleOrDefault(d => d.TenantId == tenantId && d.Id == id));
        public Task<CustomFieldDefinition?> GetByKeyAsync(
            Guid tenantId, Guid projectId, string key, CancellationToken cancellationToken) =>
            Task.FromResult(definitions.SingleOrDefault(d => d.TenantId == tenantId && d.Key == key));
        public Task<IReadOnlyList<CustomFieldDefinition>> ListAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CustomFieldDefinition>>(
                [.. definitions.Where(d => d.TenantId == tenantId && d.ProjectId == projectId)]);
    }

    private sealed class WorkItemCustomFieldValueRepositoryStub(params WorkItemCustomFieldValue[] values)
        : IWorkItemCustomFieldValueRepository
    {
        private readonly List<WorkItemCustomFieldValue> current = [.. values];

        public List<WorkItemCustomFieldValue> Added { get; } = [];
        public List<WorkItemCustomFieldValue> Removed { get; } = [];

        public Task<WorkItemCustomFieldValue?> GetAsync(
            Guid tenantId, Guid workItemId, Guid customFieldDefinitionId, CancellationToken cancellationToken) =>
            Task.FromResult(current.SingleOrDefault(
                value => value.TenantId == tenantId
                    && value.WorkItemId == workItemId
                    && value.CustomFieldDefinitionId == customFieldDefinitionId));

        public Task<IReadOnlyList<WorkItemCustomFieldValue>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemCustomFieldValue>>(
                [.. current.Where(value => value.TenantId == tenantId && value.WorkItemId == workItemId)]);

        public Task AddAsync(WorkItemCustomFieldValue value, CancellationToken cancellationToken)
        {
            current.Add(value);
            Added.Add(value);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(WorkItemCustomFieldValue value, CancellationToken cancellationToken)
        {
            current.Remove(value);
            Removed.Add(value);
            return Task.CompletedTask;
        }
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class CurrentPrincipalStub : ICurrentPrincipal
    {
        public Guid? UserId => null;
        public Guid? SessionId => null;
        public Guid MembershipId { get; } = Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => TenantRole.Member;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => true;
    }

    private sealed class WorkItemHistoryRepositoryStub : IWorkItemHistoryRepository
    {
        public List<WorkItemHistoryEntry> Added { get; } = [];

        public Task AddAsync(WorkItemHistoryEntry entry, CancellationToken cancellationToken)
        {
            Added.Add(entry);
            return Task.CompletedTask;
        }

        public Task<PagedResult<WorkItemHistoryEntry>> ListByWorkItemAsync(
            Guid tenantId, Guid workItemId, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WorkItemHistoryEntry>([], 0));

        public Task<IReadOnlyList<WorkItemHistoryEntry>> ListByWorkItemsAndFieldAsync(
            Guid tenantId, IReadOnlyCollection<Guid> workItemIds, string fieldName, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemHistoryEntry>>(
                [.. Added.Where(e => workItemIds.Contains(e.WorkItemId) && e.FieldName == fieldName)]);
    }
}
