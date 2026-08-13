using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Directory;

namespace Orbit.Application.Directory;

public sealed record DirectoryGroupDto(Guid Id, string Name, Guid CreatedByMembershipId, DateTimeOffset CreatedAt)
{
    public static DirectoryGroupDto From(DirectoryGroup group) =>
        new(group.Id, group.Name, group.CreatedByMembershipId, group.CreatedAt);
}

public sealed record GroupMembershipDto(Guid Id, Guid GroupId, Guid MembershipId, DateTimeOffset CreatedAt)
{
    public static GroupMembershipDto From(GroupMembership membership) =>
        new(membership.Id, membership.GroupId, membership.MembershipId, membership.CreatedAt);
}

public sealed record CreateGroupCommand(string Name) : ICommand<DirectoryGroupDto>;

public sealed class CreateGroupValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupValidator() => RuleFor(command => command.Name).NotEmpty().Length(2, 120);
}

public sealed class CreateGroupHandler(
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    ITenantAuthorization authorization,
    IDirectoryGroupRepository groups,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<CreateGroupCommand, DirectoryGroupDto>
{
    public async Task<DirectoryGroupDto> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("The current principal cannot manage directory groups.");
        }

        var group = DirectoryGroup.Create(
            tenantContext.TenantId, request.Name, principal.MembershipId, timeProvider.GetUtcNow());
        await groups.AddAsync(group, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DirectoryGroupDto.From(group);
    }
}

public sealed record RenameGroupCommand(Guid GroupId, string Name) : ICommand<DirectoryGroupDto>;

public sealed class RenameGroupValidator : AbstractValidator<RenameGroupCommand>
{
    public RenameGroupValidator() => RuleFor(command => command.Name).NotEmpty().Length(2, 120);
}

public sealed class RenameGroupHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    IDirectoryGroupRepository groups,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<RenameGroupCommand, DirectoryGroupDto>
{
    public async Task<DirectoryGroupDto> Handle(RenameGroupCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("The current principal cannot manage directory groups.");
        }

        var group = await groups.GetAsync(tenantContext.TenantId, request.GroupId, cancellationToken)
            ?? throw new NotFoundException("Group was not found.");
        group.Rename(request.Name, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DirectoryGroupDto.From(group);
    }
}

public sealed record ListGroupsQuery : IQuery<IReadOnlyList<DirectoryGroupDto>>;

public sealed class ListGroupsHandler(ITenantContext tenantContext, IDirectoryGroupRepository groups)
    : IRequestHandler<ListGroupsQuery, IReadOnlyList<DirectoryGroupDto>>
{
    public async Task<IReadOnlyList<DirectoryGroupDto>> Handle(
        ListGroupsQuery request, CancellationToken cancellationToken) =>
        (await groups.ListAsync(tenantContext.TenantId, cancellationToken)).Select(DirectoryGroupDto.From).ToArray();
}

public sealed record ListGroupMembersQuery(Guid GroupId) : IQuery<IReadOnlyList<GroupMembershipDto>>;

public sealed class ListGroupMembersHandler(ITenantContext tenantContext, IGroupMembershipRepository groupMemberships)
    : IRequestHandler<ListGroupMembersQuery, IReadOnlyList<GroupMembershipDto>>
{
    public async Task<IReadOnlyList<GroupMembershipDto>> Handle(
        ListGroupMembersQuery request,
        CancellationToken cancellationToken) =>
        (await groupMemberships.ListByGroupAsync(tenantContext.TenantId, request.GroupId, cancellationToken))
            .Select(GroupMembershipDto.From)
            .ToArray();
}

public sealed record AddGroupMemberCommand(Guid GroupId, Guid MembershipId) : ICommand<GroupMembershipDto>;

public sealed class AddGroupMemberValidator : AbstractValidator<AddGroupMemberCommand>
{
    public AddGroupMemberValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.MembershipId).NotEmpty();
    }
}

public sealed class AddGroupMemberHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    IDirectoryGroupRepository groups,
    IGroupMembershipRepository groupMemberships,
    ITenantMembershipRepository memberships,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<AddGroupMemberCommand, GroupMembershipDto>
{
    public async Task<GroupMembershipDto> Handle(AddGroupMemberCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("The current principal cannot manage directory groups.");
        }

        var tenantId = tenantContext.TenantId;
        _ = await groups.GetAsync(tenantId, request.GroupId, cancellationToken)
            ?? throw new NotFoundException("Group was not found.");
        _ = await memberships.GetActiveAsync(tenantId, request.MembershipId, cancellationToken)
            ?? throw new NotFoundException("Workspace membership was not found.");

        var existing = await groupMemberships.GetAsync(tenantId, request.GroupId, request.MembershipId, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("This member already belongs to the group.");
        }

        var membership = GroupMembership.Create(tenantId, request.GroupId, request.MembershipId, timeProvider.GetUtcNow());
        await groupMemberships.AddAsync(membership, cancellationToken);
        var workspace = await settings.GetWorkspaceAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");
        workspace.IncrementAuthorizationEpoch();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return GroupMembershipDto.From(membership);
    }
}

public sealed record RemoveGroupMemberCommand(Guid GroupId, Guid MembershipId) : ICommand<Unit>;

public sealed class RemoveGroupMemberHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    IGroupMembershipRepository groupMemberships,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoveGroupMemberCommand, Unit>
{
    public async Task<Unit> Handle(RemoveGroupMemberCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageTeams())
        {
            throw new AccessDeniedException("The current principal cannot manage directory groups.");
        }

        var tenantId = tenantContext.TenantId;
        var membership = await groupMemberships.GetAsync(
            tenantId, request.GroupId, request.MembershipId, cancellationToken)
            ?? throw new NotFoundException("Group membership was not found.");
        await groupMemberships.RemoveAsync(membership, cancellationToken);
        var workspace = await settings.GetWorkspaceAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Workspace was not found.");
        workspace.IncrementAuthorizationEpoch();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
