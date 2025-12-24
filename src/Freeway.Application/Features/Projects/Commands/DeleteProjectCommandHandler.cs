using Freeway.Application.Common;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Application.Features.Projects.Commands;

public class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, Result<bool>>
{
    private readonly IAppDbContext _context;
    private readonly IProjectCacheService _projectCacheService;

    public DeleteProjectCommandHandler(
        IAppDbContext context,
        IProjectCacheService projectCacheService)
    {
        _context = context;
        _projectCacheService = projectCacheService;
    }

    public async Task<Result<bool>> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (project == null)
        {
            return Result<bool>.NotFound("Project not found");
        }

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        _projectCacheService.InvalidateCache();

        return Result<bool>.NoContent();
    }
}
