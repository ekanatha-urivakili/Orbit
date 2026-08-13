using Orbit.Domain.Projects;

namespace Orbit.Application.Projects;

public sealed record ProjectDto(
    Guid Id,
    string Key,
    string Name,
    long Version,
    DateTimeOffset CreatedAt)
{
    public static ProjectDto From(Project project) =>
        new(project.Id, project.Key, project.Name, project.Version, project.CreatedAt);
}
