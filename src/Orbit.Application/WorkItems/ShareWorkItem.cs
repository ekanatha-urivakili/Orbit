using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Messaging;

namespace Orbit.Application.WorkItems;

public sealed record ShareWorkItemCommand(
    Guid WorkItemId, Guid[] MembershipIds, Guid[] TeamIds, string? Message) : ICommand<Unit>;

public sealed class ShareWorkItemValidator : AbstractValidator<ShareWorkItemCommand>
{
    public ShareWorkItemValidator()
    {
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.Message).MaximumLength(2_000);
        RuleFor(command => command)
            .Must(command => command.MembershipIds.Length > 0 || command.TeamIds.Length > 0)
            .WithMessage("At least one recipient or team is required.");
    }
}

public sealed class ShareWorkItemHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IWorkItemRepository workItems,
    ITenantMembershipRepository tenantMemberships,
    ITeamMembershipRepository teamMemberships,
    ISettingsRepository settings,
    IOutboxRepository outbox,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<ShareWorkItemCommand, Unit>
{
    public async Task<Unit> Handle(ShareWorkItemCommand request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var membershipIds = new HashSet<Guid>(request.MembershipIds);
        foreach (var teamId in request.TeamIds)
        {
            var members = await teamMemberships.ListByTeamAsync(tenantContext.TenantId, teamId, cancellationToken);
            foreach (var member in members)
            {
                membershipIds.Add(member.MembershipId);
            }
        }

        var memberships = await tenantMemberships.ListByIdsAsync(tenantContext.TenantId, membershipIds, cancellationToken);
        var recipientUserIds = memberships
            .Where(membership => membership.IsActive && membership.UserId.HasValue)
            .Select(membership => membership.UserId!.Value)
            .Distinct()
            .Where(userId => userId != principal.UserId)
            .ToArray();
        if (recipientUserIds.Length == 0)
        {
            return Unit.Value;
        }

        var accounts = await settings.GetUserAccountsAsync(recipientUserIds, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var sharerAccount = principal.UserId.HasValue
            ? await settings.GetUserAccountAsync(principal.UserId.Value, cancellationToken)
            : null;
        var sharerName = sharerAccount?.DisplayName ?? "A teammate";
        var link = $"/browse/{workItem.Key}";

        foreach (var account in accounts)
        {
            var messageHtml = string.IsNullOrWhiteSpace(request.Message)
                ? string.Empty
                : $"<p>{System.Net.WebUtility.HtmlEncode(request.Message)}</p>";

            var email = OutboxEmailMessage.Create(
                account.NormalizedEmail,
                $"{sharerName} shared {workItem.Key} with you",
                $"""
                <p>Hi {System.Net.WebUtility.HtmlEncode(account.DisplayName)},</p>
                <p>{System.Net.WebUtility.HtmlEncode(sharerName)} shared
                <strong>{System.Net.WebUtility.HtmlEncode(workItem.Key)}: {System.Net.WebUtility.HtmlEncode(workItem.Summary)}</strong>
                with you.</p>
                {messageHtml}
                <p><a href="{link}">{System.Net.WebUtility.HtmlEncode(link)}</a></p>
                """,
                now);
            await outbox.AddAsync(email, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
