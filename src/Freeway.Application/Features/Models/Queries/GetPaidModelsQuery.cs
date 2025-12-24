using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Models.Queries;

public record GetPaidModelsQuery : IRequest<Result<ModelsListDto>>;
