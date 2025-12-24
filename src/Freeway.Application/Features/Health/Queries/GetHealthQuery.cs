using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Health.Queries;

public record GetHealthQuery : IRequest<Result<HealthResponseDto>>;
