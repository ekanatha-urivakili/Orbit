using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Settings;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;

namespace Orbit.Application.Configuration;

public sealed record CustomFieldChoiceOptionDto(Guid Id, string Label, int Order)
{
    public static CustomFieldChoiceOptionDto From(CustomFieldChoiceOption option) =>
        new(option.Id, option.Label, option.Order);
}

public sealed record CustomFieldDefinitionDto(
    Guid Id,
    Guid ProjectId,
    string Key,
    string Label,
    CustomFieldType FieldType,
    bool Required,
    int Order,
    bool Enabled,
    long Version,
    IReadOnlyList<CustomFieldChoiceOptionDto> ChoiceOptions,
    IReadOnlyList<WorkItemType> ApplicableTypes)
{
    public static CustomFieldDefinitionDto From(CustomFieldDefinition definition) =>
        new(
            definition.Id,
            definition.ProjectId,
            definition.Key,
            definition.Label,
            definition.FieldType,
            definition.Required,
            definition.Order,
            definition.Enabled,
            definition.Version,
            [.. definition.ChoiceOptions.OrderBy(option => option.Order).Select(CustomFieldChoiceOptionDto.From)],
            definition.ApplicableTypes);
}

public sealed record CreateCustomFieldCommand(
    Guid ProjectId,
    string Key,
    string Label,
    CustomFieldType FieldType,
    bool Required,
    int Order,
    IReadOnlyList<CustomFieldChoiceOptionInput> ChoiceOptions,
    IReadOnlyList<WorkItemType> ApplicableTypes) : ICommand<CustomFieldDefinitionDto>;

public sealed class CreateCustomFieldValidator : AbstractValidator<CreateCustomFieldCommand>
{
    public CreateCustomFieldValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Key).NotEmpty().MaximumLength(64).Matches("^[a-zA-Z0-9-]+$");
        RuleFor(command => command.Label).NotEmpty().Length(2, 80);
        RuleFor(command => command.FieldType).IsInEnum();
        RuleFor(command => command.Order).InclusiveBetween(0, 10_000);
        RuleFor(command => command.ChoiceOptions)
            .Must(options => options == null || options.Count <= 100)
            .WithMessage("Cannot specify more than 100 choice options.");
        RuleForEach(command => command.ChoiceOptions).ChildRules(option =>
            option.RuleFor(o => o.Label)
                .Must(l => !string.IsNullOrWhiteSpace(l) && l.Trim().Length is >= 1 and <= 80)
                .WithMessage("Option label must contain 1 to 80 characters."));
        RuleForEach(command => command.ApplicableTypes).IsInEnum();
    }
}

public sealed class CreateCustomFieldHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ICustomFieldRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<CreateCustomFieldCommand, CustomFieldDefinitionDto>
{
    public async Task<CustomFieldDefinitionDto> Handle(
        CreateCustomFieldCommand request,
        CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var normalizedKey = CustomFieldDefinition.NormalizeKey(request.Key);
        var existing = await repository.GetByKeyAsync(
            tenant.TenantId, request.ProjectId, normalizedKey, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("A field with this key already exists.");
        }

        var definition = CustomFieldDefinition.Create(
            tenant.TenantId,
            request.ProjectId,
            normalizedKey,
            request.Label,
            request.FieldType,
            request.Required,
            request.Order,
            request.ChoiceOptions,
            request.ApplicableTypes,
            timeProvider.GetUtcNow());
        await repository.AddAsync(definition, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CustomFieldDefinitionDto.From(definition);
    }
}

public sealed record ListCustomFieldsQuery(Guid ProjectId) : IQuery<IReadOnlyList<CustomFieldDefinitionDto>>;

public sealed class ListCustomFieldsHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ICustomFieldRepository repository) : IRequestHandler<ListCustomFieldsQuery, IReadOnlyList<CustomFieldDefinitionDto>>
{
    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> Handle(
        ListCustomFieldsQuery request,
        CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        return (await repository.ListAsync(tenant.TenantId, request.ProjectId, cancellationToken))
            .Select(CustomFieldDefinitionDto.From)
            .ToArray();
    }
}

public sealed record UpdateCustomFieldCommand(
    Guid ProjectId,
    Guid Id,
    string Label,
    bool Required,
    int Order,
    bool Enabled,
    IReadOnlyList<CustomFieldChoiceOptionInput> ChoiceOptions,
    IReadOnlyList<WorkItemType> ApplicableTypes,
    long ExpectedVersion) : ICommand<CustomFieldDefinitionDto>;

public sealed class UpdateCustomFieldValidator : AbstractValidator<UpdateCustomFieldCommand>
{
    public UpdateCustomFieldValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Label).NotEmpty().Length(2, 80);
        RuleFor(command => command.Order).InclusiveBetween(0, 10_000);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
        RuleFor(command => command.ChoiceOptions)
            .Must(options => options == null || options.Count <= 100)
            .WithMessage("Cannot specify more than 100 choice options.");
        RuleForEach(command => command.ChoiceOptions).ChildRules(option =>
            option.RuleFor(o => o.Label)
                .Must(l => !string.IsNullOrWhiteSpace(l) && l.Trim().Length is >= 1 and <= 80)
                .WithMessage("Option label must contain 1 to 80 characters."));
        RuleForEach(command => command.ApplicableTypes).IsInEnum();
    }
}

public sealed class UpdateCustomFieldHandler(
    ITenantContext tenant,
    IProjectRepository projects,
    ICustomFieldRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UpdateCustomFieldCommand, CustomFieldDefinitionDto>
{
    public async Task<CustomFieldDefinitionDto> Handle(
        UpdateCustomFieldCommand request,
        CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.Administer, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var definition = await repository.GetAsync(tenant.TenantId, request.ProjectId, request.Id, cancellationToken)
            ?? throw new NotFoundException("Field was not found.");
        SettingsConcurrency.EnsureVersion(
            true, definition.Version, request.ExpectedVersion, "The field changed after it was loaded.");

        definition.Update(
            request.Label,
            request.Required,
            request.Order,
            request.Enabled,
            request.ChoiceOptions,
            request.ApplicableTypes,
            timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CustomFieldDefinitionDto.From(definition);
    }
}
