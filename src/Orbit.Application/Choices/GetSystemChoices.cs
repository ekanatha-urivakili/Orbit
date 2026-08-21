using Orbit.Application.Abstractions;
using Orbit.Domain.Choices;

namespace Orbit.Application.Choices;

public sealed record ChoiceDto(
    string Id,
    string Value,
    string Label,
    string Description,
    int Order,
    string ColorToken,
    bool Enabled);

public sealed record SystemChoicesDto(
    IReadOnlyList<ChoiceDto> WorkItemTypes,
    IReadOnlyList<ChoiceDto> Priorities);

public sealed record GetSystemChoicesQuery : IQuery<SystemChoicesDto>;

public sealed class GetSystemChoicesHandler : MediatR.IRequestHandler<GetSystemChoicesQuery, SystemChoicesDto>
{
    public Task<SystemChoicesDto> Handle(GetSystemChoicesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new SystemChoicesDto(
            Map(SystemChoiceCatalog.WorkItemTypes),
            Map(SystemChoiceCatalog.Priorities)));

    private static IReadOnlyList<ChoiceDto> Map<T>(IReadOnlyList<ChoiceOption<T>> choices)
        where T : struct, Enum =>
        choices.Select(choice => new ChoiceDto(
            choice.Id,
            choice.Value.ToString(),
            choice.Label,
            choice.Description,
            choice.Order,
            choice.ColorToken,
            choice.Enabled)).ToArray();
}
