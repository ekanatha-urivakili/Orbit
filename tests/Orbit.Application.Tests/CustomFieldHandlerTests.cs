using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Configuration;
using Orbit.Domain.Access;
using Orbit.Domain.Configuration;
using Orbit.Domain.Projects;

namespace Orbit.Application.Tests;

public sealed class CustomFieldHandlerTests
{
    [Fact]
    public async Task Create_PersistsNewFieldForProject()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var repository = new RepositoryStub();
        var unitOfWork = new UnitOfWorkStub();
        var handler = new CreateCustomFieldHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            repository,
            unitOfWork,
            TimeProvider.System);

        var result = await handler.Handle(
            new CreateCustomFieldCommand(project.Id, "Severity", "Severity", CustomFieldType.Text, false, 10, []),
            CancellationToken.None);

        Assert.Equal("severity", result.Key);
        Assert.Equal(project.Id, result.ProjectId);
        Assert.Single(repository.Definitions);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_RejectsDuplicateKeyWithinProject()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var repository = new RepositoryStub();
        repository.Definitions.Add(
            CustomFieldDefinition.Create(
                tenantId, project.Id, "severity", "Severity", CustomFieldType.Text, false, 10, [], DateTimeOffset.UtcNow));
        var handler = new CreateCustomFieldHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            repository,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateCustomFieldCommand(project.Id, "Severity", "Severity level", CustomFieldType.Text, false, 20, []),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(action);
    }

    [Fact]
    public async Task Create_HidesExistence_WhenPrincipalLacksAdministerPermission()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var handler = new CreateCustomFieldHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.View]),
            new RepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateCustomFieldCommand(project.Id, "severity", "Severity", CustomFieldType.Text, false, 0, []),
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task Create_HidesExistence_ForCrossTenantProject()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantProject = Project.Create(Guid.NewGuid(), "ORB", "Orbit", DateTimeOffset.UtcNow);
        var handler = new CreateCustomFieldHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(otherTenantProject, [ProjectPermission.Administer]),
            new RepositoryStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new CreateCustomFieldCommand(
                otherTenantProject.Id, "severity", "Severity", CustomFieldType.Text, false, 0, []),
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task List_ReturnsProjectFieldsInOrder()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var repository = new RepositoryStub();
        repository.Definitions.Add(
            CustomFieldDefinition.Create(
                tenantId, project.Id, "b-field", "B Field", CustomFieldType.Text, false, 20, [], DateTimeOffset.UtcNow));
        repository.Definitions.Add(
            CustomFieldDefinition.Create(
                tenantId, project.Id, "a-field", "A Field", CustomFieldType.Text, false, 10, [], DateTimeOffset.UtcNow));
        var handler = new ListCustomFieldsHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.View]),
            repository);

        var result = await handler.Handle(new ListCustomFieldsQuery(project.Id), CancellationToken.None);

        Assert.Equal(["a-field", "b-field"], result.Select(field => field.Key));
    }

    [Fact]
    public async Task Update_PersistsVersionedChangeWithoutTouchingKey()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var definition = CustomFieldDefinition.Create(
            tenantId, project.Id, "severity", "Severity", CustomFieldType.Text, false, 10, [], DateTimeOffset.UtcNow);
        var repository = new RepositoryStub();
        repository.Definitions.Add(definition);
        var unitOfWork = new UnitOfWorkStub();
        var handler = new UpdateCustomFieldHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            repository,
            unitOfWork,
            TimeProvider.System);

        var result = await handler.Handle(
            new UpdateCustomFieldCommand(project.Id, definition.Id, "Severity level", true, 15, false, [], definition.Version),
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
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var definition = CustomFieldDefinition.Create(
            tenantId, project.Id, "severity", "Severity", CustomFieldType.Text, false, 10, [], DateTimeOffset.UtcNow);
        var repository = new RepositoryStub();
        repository.Definitions.Add(definition);
        var handler = new UpdateCustomFieldHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            repository,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new UpdateCustomFieldCommand(
                project.Id, definition.Id, "Severity level", true, 15, true, [], definition.Version + 1),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(action);
    }

    [Fact]
    public async Task Update_HidesExistence_WhenPrincipalLacksAdministerPermission()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var definition = CustomFieldDefinition.Create(
            tenantId, project.Id, "severity", "Severity", CustomFieldType.Text, false, 10, [], DateTimeOffset.UtcNow);
        var repository = new RepositoryStub();
        repository.Definitions.Add(definition);
        var handler = new UpdateCustomFieldHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.View]),
            repository,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new UpdateCustomFieldCommand(project.Id, definition.Id, "Severity level", true, 15, true, [], definition.Version),
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task Update_AddsRemovesAndReordersChoiceOptions()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "ORB", "Orbit", DateTimeOffset.UtcNow);
        var definition = CustomFieldDefinition.Create(
            tenantId, project.Id, "severity", "Severity", CustomFieldType.SingleChoice, false, 10,
            [new CustomFieldChoiceOptionInput(null, "Low"), new CustomFieldChoiceOptionInput(null, "High")],
            DateTimeOffset.UtcNow);
        var lowId = definition.ChoiceOptions.Single(o => o.Label == "Low").Id;
        var repository = new RepositoryStub();
        repository.Definitions.Add(definition);
        var handler = new UpdateCustomFieldHandler(
            new TenantContextStub(tenantId),
            new ProjectRepositoryStub(project, [ProjectPermission.Administer]),
            repository,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var result = await handler.Handle(
            new UpdateCustomFieldCommand(
                project.Id,
                definition.Id,
                "Severity",
                false,
                10,
                true,
                [new CustomFieldChoiceOptionInput(null, "Critical"), new CustomFieldChoiceOptionInput(lowId, "Low")],
                definition.Version),
            CancellationToken.None);

        Assert.Equal(2, result.ChoiceOptions.Count);
        Assert.Equal("Critical", result.ChoiceOptions.Single(o => o.Order == 0).Label);
        Assert.Equal(lowId, result.ChoiceOptions.Single(o => o.Order == 1).Id);
    }

    [Fact]
    public void CreateValidator_RejectsMoreThan100ChoiceOptions()
    {
        var validator = new CreateCustomFieldValidator();
        var options = Enumerable.Range(1, 101)
            .Select(i => new CustomFieldChoiceOptionInput(null, $"Option {i}"))
            .ToArray();

        var result = validator.Validate(new CreateCustomFieldCommand(
            Guid.NewGuid(), "severity", "Severity", CustomFieldType.SingleChoice, false, 0, options));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ChoiceOptions");
    }

    [Fact]
    public void CreateValidator_RejectsWhitespaceOnlyChoiceOptionLabel()
    {
        var validator = new CreateCustomFieldValidator();
        var result = validator.Validate(new CreateCustomFieldCommand(
            Guid.NewGuid(), "severity", "Severity", CustomFieldType.SingleChoice, false, 0,
            [new CustomFieldChoiceOptionInput(null, "   ")]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.StartsWith("ChoiceOptions"));
    }

    [Fact]
    public void UpdateValidator_RejectsMoreThan100ChoiceOptions()
    {
        var validator = new UpdateCustomFieldValidator();
        var options = Enumerable.Range(1, 101)
            .Select(i => new CustomFieldChoiceOptionInput(null, $"Option {i}"))
            .ToArray();

        var result = validator.Validate(new UpdateCustomFieldCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Severity", false, 0, true, options, 1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ChoiceOptions");
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class ProjectRepositoryStub(Project project, ProjectPermission[] allowedPermissions) : IProjectRepository
    {
        public Task AddAsync(Project value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Project?> GetAsync(
            Guid tenantId,
            Guid projectId,
            ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                project.Id == projectId && project.TenantId == tenantId && allowedPermissions.Contains(permission)
                    ? project
                    : null);

        public Task<PagedResult<Project>> ListAsync(
            Guid tenantId,
            ProjectPermission permission,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Project>([project], 1));
    }

    private sealed class RepositoryStub : ICustomFieldRepository
    {
        public List<CustomFieldDefinition> Definitions { get; } = [];

        public Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken)
        {
            Definitions.Add(definition);
            return Task.CompletedTask;
        }

        public Task<CustomFieldDefinition?> GetAsync(
            Guid tenantId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Definitions.SingleOrDefault(
                definition => definition.TenantId == tenantId && definition.ProjectId == projectId && definition.Id == id));

        public Task<CustomFieldDefinition?> GetByKeyAsync(
            Guid tenantId, Guid projectId, string key, CancellationToken cancellationToken) =>
            Task.FromResult(Definitions.SingleOrDefault(
                definition => definition.TenantId == tenantId && definition.ProjectId == projectId && definition.Key == key));

        public Task<IReadOnlyList<CustomFieldDefinition>> ListAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CustomFieldDefinition>>(
                Definitions
                    .Where(definition => definition.TenantId == tenantId && definition.ProjectId == projectId)
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
