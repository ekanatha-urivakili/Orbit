namespace Orbit.Api.Endpoints;

/// <summary>
/// Reports the API's version policy: <c>/api/v1</c> (formalized via <c>app.MapGroup("/api/v1")</c>
/// in Program.cs) is the current, and so far only, supported version. This is the seam a future
/// <c>/api/v2</c> would use to coexist - a second <c>app.MapGroup("/api/v2")</c> mounted alongside
/// v1's, sharing handlers where behavior is unchanged and only re-implementing the endpoints that
/// actually break compatibility.
///
/// Deprecation/sunset convention (not yet exercised - v1 has no deprecated predecessor): once a
/// version is superseded, its group's responses gain a <c>Deprecation: &lt;date&gt;</c> header
/// (RFC 8594) and, once a removal date is set, a <c>Sunset: &lt;date&gt;</c> header plus a
/// <c>Link: &lt;...&gt;; rel="deprecation"</c> pointing at migration docs; <see cref="GetVersionAsync"/>
/// grows a matching entry in <see cref="ApiVersionInfo.Deprecated"/> at the same time.
/// </summary>
public static class VersionEndpoints
{
    public const string CurrentVersion = "v1";

    private static readonly string[] SupportedVersions = [CurrentVersion];

    public static IResult GetVersion() =>
        Results.Ok(new ApiVersionInfo(CurrentVersion, SupportedVersions, []));

    public sealed record ApiVersionInfo(
        string CurrentVersion,
        IReadOnlyList<string> SupportedVersions,
        IReadOnlyList<DeprecatedApiVersion> Deprecated);

    public sealed record DeprecatedApiVersion(string Version, DateOnly SunsetDate, string? MigrationGuideUrl);
}
