using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;
using Orbit.Domain.Settings;

namespace Orbit.Application.Settings;

/// <summary>
/// The board "View settings" panel (§13.5 next-increment): a per-user, per-project overlay for
/// field visibility, column sizing, and hide-done-after, following the same zero-version-sentinel
/// GET / If-Match PATCH shape as <see cref="ProjectSetting"/> (see
/// <see cref="GetProjectSettingHandler"/>), but keyed by the caller's own user id rather than
/// requiring project-admin permission - it's every viewer's personal preference, not shared config.
/// </summary>
public sealed record BoardViewPreferenceDto(
    Guid ProjectId,
    HideDoneItemsAfter HideDoneItemsAfter,
    BoardColumnSizeMode ColumnSizeMode,
    IReadOnlyList<string> HiddenFields,
    long Version)
{
    public static BoardViewPreferenceDto From(BoardViewPreference preference) =>
        new(preference.ProjectId, preference.HideDoneItemsAfter, preference.ColumnSizeMode, preference.HiddenFields, preference.Version);
}

public sealed record GetBoardViewPreferenceQuery(Guid ProjectId) : IQuery<BoardViewPreferenceDto>;

public sealed class GetBoardViewPreferenceHandler(
    ITenantContext tenant,
    ICurrentPrincipal principal,
    IProjectRepository projects,
    ISettingsRepository settings) : IRequestHandler<GetBoardViewPreferenceQuery, BoardViewPreferenceDto>
{
    public async Task<BoardViewPreferenceDto> Handle(
        GetBoardViewPreferenceQuery request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        var preference = await settings.GetBoardViewPreferenceAsync(
            tenant.TenantId, PrincipalGuards.RequireUser(principal), request.ProjectId, cancellationToken);
        return preference is null
            ? new BoardViewPreferenceDto(request.ProjectId, HideDoneItemsAfter.Never, BoardColumnSizeMode.Flexible, [], 0)
            : BoardViewPreferenceDto.From(preference);
    }
}

public sealed record UpdateBoardViewPreferenceCommand(
    Guid ProjectId,
    HideDoneItemsAfter HideDoneItemsAfter,
    BoardColumnSizeMode ColumnSizeMode,
    IReadOnlyList<string> HiddenFields,
    long ExpectedVersion) : ICommand<BoardViewPreferenceDto>;

public sealed class UpdateBoardViewPreferenceValidator : AbstractValidator<UpdateBoardViewPreferenceCommand>
{
    public UpdateBoardViewPreferenceValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.HideDoneItemsAfter).IsInEnum();
        RuleFor(command => command.ColumnSizeMode).IsInEnum();
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateBoardViewPreferenceHandler(
    ITenantContext tenant,
    ICurrentPrincipal principal,
    IProjectRepository projects,
    ISettingsRepository settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<UpdateBoardViewPreferenceCommand, BoardViewPreferenceDto>
{
    public async Task<BoardViewPreferenceDto> Handle(
        UpdateBoardViewPreferenceCommand request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(tenant.TenantId, request.ProjectId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        var preference = await settings.GetBoardViewPreferenceAsync(
            tenant.TenantId, PrincipalGuards.RequireUser(principal), request.ProjectId, cancellationToken);
        SettingsConcurrency.EnsureVersion(
            preference is not null,
            preference?.Version ?? 0,
            request.ExpectedVersion,
            "Your view settings changed after they were loaded.");

        var now = timeProvider.GetUtcNow();
        if (preference is null)
        {
            preference = BoardViewPreference.Create(
                tenant.TenantId, PrincipalGuards.RequireUser(principal), request.ProjectId, now);
            await settings.AddBoardViewPreferenceAsync(preference, cancellationToken);
        }

        preference.Update(request.HideDoneItemsAfter, request.ColumnSizeMode, request.HiddenFields, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return BoardViewPreferenceDto.From(preference);
    }
}
