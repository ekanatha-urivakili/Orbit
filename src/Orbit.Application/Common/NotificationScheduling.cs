using Orbit.Domain.Settings;

namespace Orbit.Application.Common;

/// <summary>
/// Computes an outbox email's <c>NotBefore</c> dispatch floor from the recipient's
/// <see cref="NotificationPreference"/>: a "None" digest cadence sends as soon as claimed (subject
/// only to quiet hours), while "Daily"/"Weekly" batch delivery to a fixed local-time window instead
/// of firing one email per event. This governs *when* a queued email may go out, not aggregation of
/// several events into one email body - each trigger still enqueues its own row.
/// </summary>
public static class NotificationScheduling
{
    private const int DigestHourLocal = 8;

    public static DateTimeOffset? ComputeNotBefore(
        NotificationPreference? preference,
        string? timeZoneId,
        DateTimeOffset now)
    {
        if (preference is null)
        {
            return null;
        }

        var timeZone = ResolveTimeZone(timeZoneId);
        DateTimeOffset? target = preference.DigestCadence switch
        {
            DigestCadence.Daily => NextDailyDigest(now, timeZone),
            DigestCadence.Weekly => NextWeeklyDigest(now, timeZone),
            _ => null
        };

        if (preference.QuietHoursStart is { } start && preference.QuietHoursEnd is { } end)
        {
            var adjusted = ApplyQuietHours(target ?? now, start, end, timeZone);
            if (adjusted != (target ?? now))
            {
                target = adjusted;
            }
        }

        return target;
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static DateTimeOffset NextDailyDigest(DateTimeOffset now, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(now, timeZone).DateTime;
        var digestLocal = local.Date.AddHours(DigestHourLocal);
        if (digestLocal <= local)
        {
            digestLocal = digestLocal.AddDays(1);
        }

        return ToOffset(digestLocal, timeZone);
    }

    private static DateTimeOffset NextWeeklyDigest(DateTimeOffset now, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(now, timeZone).DateTime;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)local.DayOfWeek + 7) % 7;
        var digestLocal = local.Date.AddDays(daysUntilMonday).AddHours(DigestHourLocal);
        if (digestLocal <= local)
        {
            digestLocal = digestLocal.AddDays(7);
        }

        return ToOffset(digestLocal, timeZone);
    }

    private static DateTimeOffset ApplyQuietHours(
        DateTimeOffset candidate,
        TimeOnly start,
        TimeOnly end,
        TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(candidate, timeZone).DateTime;
        var localTime = TimeOnly.FromDateTime(local);
        var wraps = start > end;
        var inWindow = wraps
            ? localTime >= start || localTime < end
            : localTime >= start && localTime < end;

        if (!inWindow)
        {
            return candidate;
        }

        var endDate = local.Date;
        if (wraps && localTime >= start)
        {
            endDate = endDate.AddDays(1);
        }

        return ToOffset(endDate.Add(end.ToTimeSpan()), timeZone);
    }

    private static DateTimeOffset ToOffset(DateTime local, TimeZoneInfo timeZone) =>
        new(local, timeZone.GetUtcOffset(local));
}
