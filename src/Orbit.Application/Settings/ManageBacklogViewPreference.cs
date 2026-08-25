using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Settings;

namespace Orbit.Application.Settings;

/// <summary>
/// The backlog "View settings" panel (§13.5.6 next-increment): a per-user, per-project overlay
/// for empty-sprint visibility, row density, and field visibility, mirroring
/// <see cref="ManageBoardViewPreference"/>'s zero-version-sentinel GET / If-Match PATCH shape.
/// </summary>
public sealed record BacklogViewPreferenceDto(
    Guid ProjectId,
    bool ShowEmptySprints,
    BacklogRowDensity Density,
    IReadOnlyList<string> HiddenFields,
    long Version)
{
    public static BacklogViewPreferenceDto From(BacklogViewPreference preference) =>
        new(preference.ProjectId, preference.ShowEmptySprints, preference.Density, preference.HiddenFields, preference.Version);
}

public sealed record GetBacklogViewPreferenceQuery(Guid ProjectId) : IQuery<BacklogViewPreferenceDto>;

public sealed class GetBacklogViewPreferenceHandler(
    ITenantContext tenant,
    ICurrentPrincipal principal,
    IProjectRepository projects,
    ISettingsRepository settings) : IRequestHandler<GetBacklogViewPreferenceQuery, BacklogViewPreferenceDto>
{
    public async Task<BacklogViewPreferenceDto> Handle(
        GetBacklogViewPreferenceQuery request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        var preference = await settings.GetBacklogViewPreferenceAsync(
            tenant.TenantId, PrincipalGuards.RequireUser(principal), request.ProjectId, cancellationToken);
        return preference is null
            ? new BacklogViewPreferenceDto(request.ProjectId, true, BacklogRowDensity.Default, [], 0)
            : BacklogViewPreferenceDto.From(preference);
    }
}

public sealed record UpdateBacklogViewPreferenceCommand(
    Guid ProjectId,
    bool ShowEmptySprints,
    BacklogRowDensity Density,
    IReadOnlyList<string> HiddenFields,
    long ExpectedVersion) : ICommand<BacklogViewPreferenceDto>;

public sealed class UpdateBacklogViewPreferenceValidator : AbstractValidator<UpdateBacklogViewPreferenceCommand>
{
    public UpdateBacklogViewPreferenceValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Density).IsInEnum();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateBacklogViewPreferenceHandler(
    ITenantContext tenant,
    ICurrentPrincipal principal,
    IProjectRepository projects,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UpdateBacklogViewPreferenceCommand, BacklogViewPreferenceDto>
{
    public async Task<BacklogViewPreferenceDto> Handle(
        UpdateBacklogViewPreferenceCommand request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        var preference = await settings.GetBacklogViewPreferenceAsync(
            tenant.TenantId, PrincipalGuards.RequireUser(principal), request.ProjectId, cancellationToken);
        SettingsConcurrency.EnsureVersion(
            preference is not null,
            preference?.Version ?? 0,
            request.ExpectedVersion,
            "Your view settings changed after they were loaded.");

        var now = timeProvider.GetUtcNow();
        if (preference is null)
        {
            preference = BacklogViewPreference.Create(
                tenant.TenantId, PrincipalGuards.RequireUser(principal), request.ProjectId, now);
            await settings.AddBacklogViewPreferenceAsync(preference, cancellationToken);
        }

        preference.Update(request.ShowEmptySprints, request.Density, request.HiddenFields, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return BacklogViewPreferenceDto.From(preference);
    }
}
