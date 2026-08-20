using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.Settings;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Identity;
using Orbit.Domain.Settings;
using Orbit.Domain.Workspaces;

namespace Orbit.Application.Tests;

public sealed class WorkspaceLogoHandlerTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    [Fact]
    public async Task Presign_RejectsNonAdministrator()
    {
        var handler = new PresignWorkspaceLogoUploadHandler(
            new TenantContextStub(TenantId), new PrincipalStub(TenantRole.Member), new ObjectStorageStub());

        var action = () => handler.Handle(
            new PresignWorkspaceLogoUploadCommand("logo.png", "image/png", 1024), CancellationToken.None);

        await Assert.ThrowsAsync<AccessDeniedException>(action);
    }

    [Fact]
    public async Task Presign_ReturnsObjectKeyScopedToTenant()
    {
        var handler = new PresignWorkspaceLogoUploadHandler(
            new TenantContextStub(TenantId), new PrincipalStub(TenantRole.Owner), new ObjectStorageStub());

        var result = await handler.Handle(
            new PresignWorkspaceLogoUploadCommand("logo.png", "image/png", 1024), CancellationToken.None);

        Assert.StartsWith($"{TenantId:N}/branding/", result.ObjectKey, StringComparison.Ordinal);
        Assert.EndsWith("logo.png", result.ObjectKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirm_RejectsObjectKeyFromAnotherTenant()
    {
        var repository = new SettingsRepositoryStub();
        var handler = new ConfirmWorkspaceLogoUploadHandler(
            new TenantContextStub(TenantId),
            new PrincipalStub(TenantRole.Owner),
            repository,
            new ObjectStorageStub(),
            new UnitOfWorkStub(),
            TimeProvider.System);

        var action = () => handler.Handle(
            new ConfirmWorkspaceLogoUploadCommand($"{Guid.NewGuid():N}/branding/x-logo.png", 0),
            CancellationToken.None);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(action);
    }

    [Fact]
    public async Task Confirm_SetsLogoAndDeletesPreviousObject()
    {
        var now = DateTimeOffset.UtcNow;
        var workspace = Workspace.Create(Guid.CreateVersion7(), "Workspace", now);
        var setting = WorkspaceSetting.Create(TenantId, now);
        setting.SetLogo($"{TenantId:N}/branding/old-logo.png", now);
        var repository = new SettingsRepositoryStub { Workspace = workspace, Setting = setting };
        var storage = new ObjectStorageStub();
        var handler = new ConfirmWorkspaceLogoUploadHandler(
            new TenantContextStub(TenantId),
            new PrincipalStub(TenantRole.Owner),
            repository,
            storage,
            new UnitOfWorkStub(),
            TimeProvider.System);

        var newObjectKey = $"{TenantId:N}/branding/new-logo.png";
        var result = await handler.Handle(
            new ConfirmWorkspaceLogoUploadCommand(newObjectKey, setting.Version), CancellationToken.None);

        Assert.NotNull(result.LogoUrl);
        Assert.Equal(newObjectKey, setting.LogoObjectKey);
        Assert.Equal($"{TenantId:N}/branding/old-logo.png", storage.DeletedObjectKey);
    }

    private sealed class TenantContextStub(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
    }

    private sealed class PrincipalStub(TenantRole role) : ICurrentPrincipal
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? SessionId => null;
        public Guid MembershipId => Guid.NewGuid();
        public PrincipalType PrincipalType => PrincipalType.User;
        public TenantRole TenantRole => role;
        public MembershipTier MembershipTier => MembershipTier.Standard;
        public bool IsDevelopmentBypass => false;
    }

    private sealed class ObjectStorageStub : IObjectStorageService
    {
        public string? DeletedObjectKey { get; private set; }

        public PresignedUpload CreatePresignedUpload(string objectKey, string contentType, TimeSpan expiresIn) =>
            new($"https://storage.example.test/{objectKey}", objectKey, DateTimeOffset.UtcNow.Add(expiresIn));

        public string CreatePresignedDownloadUrl(string objectKey, TimeSpan expiresIn) =>
            $"https://storage.example.test/{objectKey}?download=1";

        public string CreatePresignedDisplayUrl(string objectKey, TimeSpan expiresIn) =>
            $"https://storage.example.test/{objectKey}";

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
        {
            DeletedObjectKey = objectKey;
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream());

        public Task MoveToQuarantineAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class SettingsRepositoryStub : ISettingsRepository
    {
        public Workspace? Workspace { get; set; }
        public WorkspaceSetting? Setting { get; set; }

        public Task<UserAccount?> GetUserAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

        public Task<IReadOnlyList<UserAccount>> GetUserAccountsAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UserAccount>>([]);

        public Task<UserPreference?> GetUserPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserPreference?>(null);

        public Task<NotificationPreference?> GetNotificationPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<NotificationPreference?>(null);

        public Task<IReadOnlyList<NotificationPreference>> GetNotificationPreferencesAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NotificationPreference>>([]);

        public Task<Workspace?> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(Workspace);

        public Task<WorkspaceSetting?> GetWorkspaceSettingAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(Setting);

        public Task<WorkspaceTypographySetting?> GetWorkspaceTypographySettingAsync(
            Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceTypographySetting?>(null);

        public Task<ProjectSetting?> GetProjectSettingAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectSetting?>(null);

        public Task AddUserPreferenceAsync(UserPreference preference, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddNotificationPreferenceAsync(NotificationPreference preference, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddWorkspaceSettingAsync(WorkspaceSetting setting, CancellationToken cancellationToken)
        {
            Setting = setting;
            return Task.CompletedTask;
        }

        public Task AddWorkspaceTypographySettingAsync(
            WorkspaceTypographySetting setting, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddProjectSettingAsync(ProjectSetting setting, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
