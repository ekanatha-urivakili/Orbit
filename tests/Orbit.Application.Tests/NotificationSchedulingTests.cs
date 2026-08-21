using Orbit.Application.Common;
using Orbit.Domain.Settings;

namespace Orbit.Application.Tests;

public sealed class NotificationSchedulingTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero); // Friday noon UTC

    [Fact]
    public void ComputeNotBefore_NullPreference_ReturnsNull()
    {
        var result = NotificationScheduling.ComputeNotBefore(null, "UTC", Now);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeNotBefore_NoDigestNoQuietHours_ReturnsNull()
    {
        var preference = CreatePreference(DigestCadence.None, null, null);

        var result = NotificationScheduling.ComputeNotBefore(preference, "UTC", Now);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeNotBefore_DailyDigest_SchedulesNextEightAmLocal()
    {
        var preference = CreatePreference(DigestCadence.Daily, null, null);

        var result = NotificationScheduling.ComputeNotBefore(preference, "UTC", Now);

        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2026, 8, 22, 8, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void ComputeNotBefore_DailyDigest_AfterWindowSameDay_UsesTomorrow()
    {
        var beforeDigest = new DateTimeOffset(2026, 8, 21, 6, 0, 0, TimeSpan.Zero);
        var preference = CreatePreference(DigestCadence.Daily, null, null);

        var result = NotificationScheduling.ComputeNotBefore(preference, "UTC", beforeDigest);

        Assert.Equal(new DateTimeOffset(2026, 8, 21, 8, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void ComputeNotBefore_WeeklyDigest_SchedulesNextMonday()
    {
        var preference = CreatePreference(DigestCadence.Weekly, null, null);

        var result = NotificationScheduling.ComputeNotBefore(preference, "UTC", Now);

        Assert.Equal(new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void ComputeNotBefore_InsideQuietHours_DelaysUntilQuietHoursEnd()
    {
        var preference = CreatePreference(
            DigestCadence.None, new TimeOnly(9, 0), new TimeOnly(17, 0));

        var result = NotificationScheduling.ComputeNotBefore(preference, "UTC", Now);

        Assert.Equal(new DateTimeOffset(2026, 8, 21, 17, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void ComputeNotBefore_OutsideQuietHours_ReturnsNull()
    {
        var preference = CreatePreference(
            DigestCadence.None, new TimeOnly(22, 0), new TimeOnly(6, 0));

        var result = NotificationScheduling.ComputeNotBefore(preference, "UTC", Now);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeNotBefore_QuietHoursWrapMidnight_DelaysToNextDayEnd()
    {
        // 23:00 UTC falls inside a 22:00-06:00 wrapping window; end is the following morning.
        var lateNight = new DateTimeOffset(2026, 8, 21, 23, 0, 0, TimeSpan.Zero);
        var preference = CreatePreference(
            DigestCadence.None, new TimeOnly(22, 0), new TimeOnly(6, 0));

        var result = NotificationScheduling.ComputeNotBefore(preference, "UTC", lateNight);

        Assert.Equal(new DateTimeOffset(2026, 8, 22, 6, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void ComputeNotBefore_UnknownTimeZone_FallsBackToUtc()
    {
        var preference = CreatePreference(DigestCadence.Daily, null, null);

        var result = NotificationScheduling.ComputeNotBefore(preference, "Not/A-Zone", Now);

        Assert.Equal(new DateTimeOffset(2026, 8, 22, 8, 0, 0, TimeSpan.Zero), result);
    }

    private static NotificationPreference CreatePreference(
        DigestCadence cadence, TimeOnly? quietStart, TimeOnly? quietEnd)
    {
        var preference = NotificationPreference.Create(UserId, Now);
        preference.Update(
            inAppEnabled: true,
            emailEnabled: true,
            digestCadence: cadence,
            quietHoursStart: quietStart,
            quietHoursEnd: quietEnd,
            selfNotify: false,
            now: Now);
        return preference;
    }
}
