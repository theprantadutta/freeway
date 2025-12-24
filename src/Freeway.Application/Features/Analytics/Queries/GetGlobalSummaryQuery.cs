using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Analytics.Queries;

public record GetGlobalSummaryQuery : IRequest<Result<GlobalSummaryDto>>;
