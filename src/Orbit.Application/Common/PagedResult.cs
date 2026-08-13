namespace Orbit.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);

public static class Paging
{
    public const int DefaultTake = 200;
    public const int MaxTake = 200;
}
