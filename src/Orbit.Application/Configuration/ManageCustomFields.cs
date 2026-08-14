using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Settings;
using Orbit.Domain.Configuration;

namespace Orbit.Application.Configuration;

public sealed record CustomFieldDefinitionDto(
    Guid Id,
    string Key,
    string Label,
    CustomFieldType FieldType,
    bool Required,
    int Order,
    bool Enabled,
    long Version)
{
    public static CustomFieldDefinitionDto From(CustomFieldDefinition definition) =>
        new(
            definition.Id,
            definition.Key,
            definition.Label,
            definition.FieldType,
            definition.Required,
            definition.Order,
            definition.Enabled,
            definition.Version);
}

public sealed record CreateCustomFieldCommand(
    string Key,
    string Label,
    CustomFieldType FieldType,
    bool Required,
    int Order) : ICommand<CustomFieldDefinitionDto>;

public sealed class CreateCustomFieldValidator : AbstractValidator<CreateCustomFieldCommand>
{
    public CreateCustomFieldValidator()
    {
        RuleFor(command => command.Key).NotEmpty().MaximumLength(64).Matches("^[a-zA-Z0-9-]+$");
        RuleFor(command => command.Label).NotEmpty().Length(2, 80);
        RuleFor(command => command.FieldType).IsInEnum();
        RuleFor(command => command.Order).InclusiveBetween(0, 10_000);
    }
}

public sealed class CreateCustomFieldHandler(
    ITenantContext tenant,
    ITenantAuthorization authorization,
    ICustomFieldRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<CreateCustomFieldCommand, CustomFieldDefinitionDto>
{
    public async Task<CustomFieldDefinitionDto> Handle(
        CreateCustomFieldCommand request,
        CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("Workspace administration permission is required.");
        }

        var normalizedKey = CustomFieldDefinition.NormalizeKey(request.Key);
        var existing = await repository.GetByKeyAsync(tenant.TenantId, normalizedKey, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("A field with this key already exists.");
        }

        var definition = CustomFieldDefinition.Create(
            tenant.TenantId,
            normalizedKey,
            request.Label,
            request.FieldType,
            request.Required,
            request.Order,
            timeProvider.GetUtcNow());
        await repository.AddAsync(definition, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CustomFieldDefinitionDto.From(definition);
    }
}

public sealed record ListCustomFieldsQuery : IQuery<IReadOnlyList<CustomFieldDefinitionDto>>;

public sealed class ListCustomFieldsHandler(
    ITenantContext tenant,
    ICustomFieldRepository repository) : IRequestHandler<ListCustomFieldsQuery, IReadOnlyList<CustomFieldDefinitionDto>>
{
    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> Handle(
        ListCustomFieldsQuery request,
        CancellationToken cancellationToken) =>
        (await repository.ListAsync(tenant.TenantId, cancellationToken))
            .Select(CustomFieldDefinitionDto.From)
            .ToArray();
}

public sealed record UpdateCustomFieldCommand(
    Guid Id,
    string Label,
    bool Required,
    int Order,
    bool Enabled,
    long ExpectedVersion) : ICommand<CustomFieldDefinitionDto>;

public sealed class UpdateCustomFieldValidator : AbstractValidator<UpdateCustomFieldCommand>
{
    public UpdateCustomFieldValidator()
    {
        RuleFor(command => command.Label).NotEmpty().Length(2, 80);
        RuleFor(command => command.Order).InclusiveBetween(0, 10_000);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class UpdateCustomFieldHandler(
    ITenantContext tenant,
    ITenantAuthorization authorization,
    ICustomFieldRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UpdateCustomFieldCommand, CustomFieldDefinitionDto>
{
    public async Task<CustomFieldDefinitionDto> Handle(
        UpdateCustomFieldCommand request,
        CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("Workspace administration permission is required.");
        }

        var definition = await repository.GetAsync(tenant.TenantId, request.Id, cancellationToken)
            ?? throw new NotFoundException("Field was not found.");
        SettingsConcurrency.EnsureVersion(
            true, definition.Version, request.ExpectedVersion, "The field changed after it was loaded.");

        definition.Update(request.Label, request.Required, request.Order, request.Enabled, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CustomFieldDefinitionDto.From(definition);
    }
}
