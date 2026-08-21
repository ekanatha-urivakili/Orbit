using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Configuration;
using Orbit.Domain.Projects;

namespace Orbit.Application.Projects;

public sealed record CreateProjectCommand(string Key, string Name) : ICommand<ProjectDto>;

public sealed class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator()
    {
        RuleFor(command => command.Key)
            .NotEmpty()
            .Matches("^[A-Za-z0-9]{2,10}$");
        RuleFor(command => command.Name).NotEmpty().Length(2, 120);
    }
}

public sealed class CreateProjectHandler(
    ITenantContext tenantContext,
    ITenantAuthorization authorization,
    IProjectRepository projects,
    IWorkItemStatusRepository statuses,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<CreateProjectCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        if (!authorization.CanCreateProject())
        {
            throw new AccessDeniedException("The current principal cannot create projects.");
        }

        var now = timeProvider.GetUtcNow();
        var project = Project.Create(tenantContext.TenantId, request.Key, request.Name, now);
        await projects.AddAsync(project, cancellationToken);

        // Every project owns its own workflow (§13.5 "Edit workflow"); seed the six default
        // statuses so the board and work items have somewhere to point on day one.
        await statuses.AddRangeAsync(
            WorkItemStatusDefinition.CreateSoftwareDefaults(tenantContext.TenantId, project.Id, now),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ProjectDto.From(project);
    }
}
