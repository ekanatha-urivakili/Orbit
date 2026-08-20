using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.WorkItems;

public sealed record SetWorkItemCoverCommand(Guid WorkItemId, Guid? AttachmentId, long ExpectedVersion)
    : ICommand<WorkItemDto>;

public sealed class SetWorkItemCoverValidator : AbstractValidator<SetWorkItemCoverCommand>
{
    public SetWorkItemCoverValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class SetWorkItemCoverHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems,
    IAttachmentRepository attachments,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<SetWorkItemCoverCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(SetWorkItemCoverCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.TransitionWorkItem, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        if (workItem.Version != request.ExpectedVersion)
        {
            throw new ConcurrencyException("The work item changed after it was loaded.");
        }

        if (request.AttachmentId is { } attachmentId)
        {
            var attachment = await attachments.GetAsync(
                tenantContext.TenantId, request.WorkItemId, attachmentId, cancellationToken)
                ?? throw new NotFoundException("Attachment was not found.");
            if (!attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("Only image attachments can be used as a cover.");
            }

            if (attachment.ScanStatus != AttachmentScanStatus.Clean)
            {
                throw new ValidationException("Only attachments that have passed malware scanning can be used as a cover.");
            }
        }

        workItem.SetCover(request.AttachmentId, timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkItemDto.From(workItem);
    }
}
