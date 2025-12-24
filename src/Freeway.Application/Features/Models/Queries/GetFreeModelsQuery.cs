using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Models.Queries;

public record GetFreeModelsQuery : IRequest<Result<ModelsListDto>>;
