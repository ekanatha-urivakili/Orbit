using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Settings;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;

namespace Orbit.Application.Configuration;

public sealed record WorkItemTypeDefinitionDto(
    WorkItemType Id,
    string Label,
    string Description,
    int Order,
    string ColorToken,
    bool Enabled,
    bool CanAdminister,
    long Version)
{
    public static WorkItemTypeDefinitionDto From(
        WorkItemTypeDefinition definition,
        bool canAdminister) =>
        new(
            definition.Id,
            definition.Label,
            definition.Description,
            definition.Order,
            definition.ColorToken,
            definition.Enabled,
            canAdminister,
            definition.Version);
}

public sealed record ListWorkItemTypesQuery : IQuery<IReadOnlyList<WorkItemTypeDefinitionDto>>;

public sealed class ListWorkItemTypesHandler(
    ITenantContext tenant,
    ITenantAuthorization authorization,
    IWorkItemTypeRepository repository)
    : IRequestHandler<ListWorkItemTypesQuery, IReadOnlyList<WorkItemTypeDefinitionDto>>
{
    public async Task<IReadOnlyList<WorkItemTypeDefinitionDto>> Handle(
        ListWorkItemTypesQuery request,
        CancellationToken cancellationToken)
    {
        var canAdminister = authorization.CanManageTeams();
        return (await repository.ListAsync(tenant.TenantId, cancellationToken))
            .Select(definition => WorkItemTypeDefinitionDto.From(definition, canAdminister))
            .ToArray();
    }
}

public sealed record UpdateWorkItemTypeCommand(
    WorkItemType Id,
    string Label,
    string Description,
    int Order,
    string ColorToken,
    bool Enabled,
    long ExpectedVersion) : ICommand<WorkItemTypeDefinitionDto>;

public sealed class UpdateWorkItemTypeValidator : AbstractValidator<UpdateWorkItemTypeCommand>
{
    public UpdateWorkItemTypeValidator()
    {
        RuleFor(command => command.Id).IsInEnum();
        RuleFor(command => command.Label).NotEmpty().Length(2, 80);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.Order).InclusiveBetween(0, 10_000);
        RuleFor(command => command.ColorToken).NotEmpty().MaximumLength(32).Matches("^[a-zA-Z0-9-]+$");
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class UpdateWorkItemTypeHandler(
    ITenantContext tenant,
    ITenantAuthorization authorization,
    IWorkItemTypeRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateWorkItemTypeCommand, WorkItemTypeDefinitionDto>
{
    public async Task<WorkItemTypeDefinitionDto> Handle(
        UpdateWorkItemTypeCommand request,
        CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("Workspace administration permission is required.");
        }

        var definition = await repository.GetAsync(tenant.TenantId, request.Id, cancellationToken)
            ?? throw new NotFoundException("Work item type was not found.");
        SettingsConcurrency.EnsureVersion(
            true, definition.Version, request.ExpectedVersion, "The work item type changed after it was loaded.");

        definition.Update(
            request.Label,
            request.Description,
            request.Order,
            request.ColorToken,
            request.Enabled,
            timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemTypeDefinitionDto.From(definition, true);
    }
}
