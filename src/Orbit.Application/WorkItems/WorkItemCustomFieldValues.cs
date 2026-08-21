using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record WorkItemCustomFieldValueDto(Guid CustomFieldDefinitionId, string[] Values)
{
    public static WorkItemCustomFieldValueDto From(WorkItemCustomFieldValue value) =>
        new(value.CustomFieldDefinitionId, value.Values);
}

public sealed record CustomFieldValueInput(Guid CustomFieldDefinitionId, string[] Values);

public sealed record GetWorkItemCustomFieldValuesQuery(Guid WorkItemId) : IQuery<IReadOnlyList<WorkItemCustomFieldValueDto>>;

public sealed class GetWorkItemCustomFieldValuesValidator : AbstractValidator<GetWorkItemCustomFieldValuesQuery>
{
    public GetWorkItemCustomFieldValuesValidator() => RuleFor(query => query.WorkItemId).NotEmpty();
}

public sealed class GetWorkItemCustomFieldValuesHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IWorkItemCustomFieldValueRepository values)
    : IRequestHandler<GetWorkItemCustomFieldValuesQuery, IReadOnlyList<WorkItemCustomFieldValueDto>>
{
    public async Task<IReadOnlyList<WorkItemCustomFieldValueDto>> Handle(
        GetWorkItemCustomFieldValuesQuery request, CancellationToken cancellationToken)
    {
        _ = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var current = await values.ListByWorkItemAsync(tenantContext.TenantId, request.WorkItemId, cancellationToken);
        return [.. current.Select(WorkItemCustomFieldValueDto.From)];
    }
}

public sealed record SetWorkItemCustomFieldValuesCommand(
    Guid WorkItemId, IReadOnlyList<CustomFieldValueInput> Values)
    : ICommand<IReadOnlyList<WorkItemCustomFieldValueDto>>;

public sealed class SetWorkItemCustomFieldValuesValidator : AbstractValidator<SetWorkItemCustomFieldValuesCommand>
{
    public SetWorkItemCustomFieldValuesValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleForEach(command => command.Values).ChildRules(input =>
            input.RuleFor(i => i.CustomFieldDefinitionId).NotEmpty());
    }
}

public sealed class SetWorkItemCustomFieldValuesHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    ICustomFieldRepository definitions,
    IWorkItemCustomFieldValueRepository values,
    IWorkItemHistoryRepository history,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<SetWorkItemCustomFieldValuesCommand, IReadOnlyList<WorkItemCustomFieldValueDto>>
{
    public async Task<IReadOnlyList<WorkItemCustomFieldValueDto>> Handle(
        SetWorkItemCustomFieldValuesCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
                tenantContext.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var projectDefinitions = await definitions.ListAsync(
            tenantContext.TenantId, workItem.ProjectId, cancellationToken);
        var definitionsById = projectDefinitions.ToDictionary(definition => definition.Id);

        var now = timeProvider.GetUtcNow();
        foreach (var input in request.Values)
        {
            if (!definitionsById.TryGetValue(input.CustomFieldDefinitionId, out var definition) || !definition.Enabled)
            {
                throw new ValidationException("One or more custom fields were not found on this project.");
            }

            if (!definition.AppliesTo(workItem.Type))
            {
                if (input.Values.Length > 0)
                {
                    throw new ValidationException($"'{definition.Label}' does not apply to this work item's type.");
                }

                continue;
            }

            if (definition.Required && input.Values.Length == 0)
            {
                throw new ValidationException($"'{definition.Label}' is required.");
            }

            var existing = await values.GetAsync(
                tenantContext.TenantId, workItem.Id, definition.Id, cancellationToken);
            var oldValueLabel = existing is { Values.Length: > 0 } ? string.Join(", ", existing.Values) : null;
            var newValueLabel = input.Values.Length > 0 ? string.Join(", ", input.Values) : null;

            if (input.Values.Length == 0)
            {
                if (existing is not null)
                {
                    await values.RemoveAsync(existing, cancellationToken);
                }
            }
            else if (existing is null)
            {
                await values.AddAsync(
                    WorkItemCustomFieldValue.Create(
                        tenantContext.TenantId, workItem.Id, definition, input.Values, now),
                    cancellationToken);
            }
            else
            {
                existing.SetValues(definition, input.Values, now);
            }

            await WorkItemHistoryRecorder.RecordAsync(
                history, tenantContext.TenantId, workItem.Id, principal.MembershipId, now, cancellationToken,
                (definition.Label, oldValueLabel, newValueLabel));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var current = await values.ListByWorkItemAsync(tenantContext.TenantId, workItem.Id, cancellationToken);
        return [.. current.Select(WorkItemCustomFieldValueDto.From)];
    }
}
