using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.Access;

public sealed record RoleDto(Guid Id, string Name, bool IsSystem, IReadOnlyList<ProjectPermission> Permissions, DateTimeOffset CreatedAt)
{
    public static RoleDto From(Role role) =>
        new(role.Id, role.Name, role.IsSystem, role.Permissions.Select(p => p.Permission).ToArray(), role.CreatedAt);
}

public sealed record ListRolesQuery : IQuery<IReadOnlyList<RoleDto>>;

public sealed class ListRolesHandler(ITenantContext tenantContext, IRoleRepository roles)
    : IRequestHandler<ListRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<IReadOnlyList<RoleDto>> Handle(ListRolesQuery request, CancellationToken cancellationToken) =>
        (await roles.ListByTenantAsync(tenantContext.TenantId, cancellationToken)).Select(RoleDto.From).ToArray();
}

public sealed record CreateRoleCommand(string Name, IReadOnlyList<ProjectPermission> Permissions) : ICommand<RoleDto>;

public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(command => command.Name).NotEmpty().Length(1, 100);
        RuleForEach(command => command.Permissions).IsInEnum();
    }
}

public sealed class CreateRoleHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    IRoleRepository roles,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<CreateRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageRoles())
        {
            throw new AccessDeniedException("The current principal cannot manage roles.");
        }

        var existing = await roles.GetByNameAsync(tenantContext.TenantId, request.Name, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("A role with this name already exists.");
        }

        var role = Role.Create(tenantContext.TenantId, request.Name, isSystem: false, request.Permissions, timeProvider.GetUtcNow());
        await roles.AddAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return RoleDto.From(role);
    }
}

public sealed record RenameRoleCommand(Guid RoleId, string Name) : ICommand<RoleDto>;

public sealed class RenameRoleValidator : AbstractValidator<RenameRoleCommand>
{
    public RenameRoleValidator() => RuleFor(command => command.Name).NotEmpty().Length(1, 100);
}

public sealed class RenameRoleHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    IRoleRepository roles,
    IUnitOfWork unitOfWork) : IRequestHandler<RenameRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(RenameRoleCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageRoles())
        {
            throw new AccessDeniedException("The current principal cannot manage roles.");
        }

        var role = await roles.GetAsync(tenantContext.TenantId, request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Role was not found.");
        var existing = await roles.GetByNameAsync(tenantContext.TenantId, request.Name, cancellationToken);
        if (existing is not null && existing.Id != role.Id)
        {
            throw new ConflictException("A role with this name already exists.");
        }

        role.Rename(request.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return RoleDto.From(role);
    }
}

public sealed record UpdateRolePermissionsCommand(Guid RoleId, IReadOnlyList<ProjectPermission> Permissions) : ICommand<RoleDto>;

public sealed class UpdateRolePermissionsValidator : AbstractValidator<UpdateRolePermissionsCommand>
{
    public UpdateRolePermissionsValidator() => RuleForEach(command => command.Permissions).IsInEnum();
}

public sealed class UpdateRolePermissionsHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    IRoleRepository roles,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateRolePermissionsCommand, RoleDto>
{
    public async Task<RoleDto> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageRoles())
        {
            throw new AccessDeniedException("The current principal cannot manage roles.");
        }

        var role = await roles.GetAsync(tenantContext.TenantId, request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Role was not found.");

        foreach (var permission in Enum.GetValues<ProjectPermission>())
        {
            if (request.Permissions.Contains(permission))
            {
                role.GrantPermission(permission);
            }
            else
            {
                role.RevokePermission(permission);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return RoleDto.From(role);
    }
}

public sealed record DeleteRoleCommand(Guid RoleId) : ICommand<Unit>;

public sealed class DeleteRoleHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    IRoleRepository roles,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteRoleCommand, Unit>
{
    public async Task<Unit> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanManageRoles())
        {
            throw new AccessDeniedException("The current principal cannot manage roles.");
        }

        var role = await roles.GetAsync(tenantContext.TenantId, request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Role was not found.");
        if (role.IsSystem)
        {
            throw new ConflictException("A system role cannot be deleted.");
        }

        if (await roles.HasAssignmentsAsync(tenantContext.TenantId, request.RoleId, cancellationToken))
        {
            throw new ConflictException("A role with active assignments cannot be deleted.");
        }

        await roles.RemoveAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
