using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
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

        var project = Project.Create(tenantContext.TenantId, request.Key, request.Name, timeProvider.GetUtcNow());
        await projects.AddAsync(project, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ProjectDto.From(project);
    }
}
