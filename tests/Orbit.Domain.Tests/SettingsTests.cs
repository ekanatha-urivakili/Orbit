using Orbit.Domain.Choices;
using Orbit.Domain.Common;
using Orbit.Domain.Identity;
using Orbit.Domain.Settings;

namespace Orbit.Domain.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void UserAccount_UpdateProfile_NormalizesAndVersionsTheChange()
    {
        var createdAt = DateTimeOffset.Parse("2026-08-11T12:00:00Z");
        var account = UserAccount.Create("owner@example.test", "Owner", createdAt);

        account.UpdateProfile("  Orbit Owner  ", " https://example.test/avatar.png ", createdAt.AddMinutes(1));

        Assert.Equal("Orbit Owner", account.DisplayName);
        Assert.Equal("https://example.test/avatar.png", account.AvatarUrl);
        Assert.Equal(2, account.Version);
        Assert.Equal(createdAt.AddMinutes(1), account.UpdatedAt);
    }

    [Fact]
    public void NotificationPreference_RequiresACompleteQuietHoursWindow()
    {
        var preference = NotificationPreference.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var action = () => preference.Update(
            true,
            true,
            DigestCadence.Daily,
            new TimeOnly(18, 0),
            null,
            false,
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void ProjectSetting_RejectsSubtaskAsTheDefaultType()
    {
        var setting = ProjectSetting.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        var action = () => setting.Update(
            WorkItemType.Subtask,
            Priority.Medium,
            true,
            true,
            null,
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }
}
