using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Application.Features.Credits.Commands;

/// <summary>
/// Corrects a credit grant to match what the Console shows: the amount, or the dates
/// of the cycle. Later cycles are opened from where the latest one ends, so moving a
/// grant's expiry moves the renewal day with it.
/// </summary>
public record UpdateCreditGrantCommand(Guid Id, UpdateCreditGrantRequest Changes) : IRequest<Result<CreditGrantDto>>;

public class UpdateCreditGrantCommandHandler : IRequestHandler<UpdateCreditGrantCommand, Result<CreditGrantDto>>
{
    private readonly IAppDbContext _context;
    private readonly IAnthropicCreditService _credit;
    private readonly IDateTimeService _dateTimeService;

    public UpdateCreditGrantCommandHandler(
        IAppDbContext context,
        IAnthropicCreditService credit,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _credit = credit;
        _dateTimeService = dateTimeService;
    }

    public async Task<Result<CreditGrantDto>> Handle(UpdateCreditGrantCommand request, CancellationToken cancellationToken)
    {
        var grant = await _context.CreditGrants.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);
        if (grant is null)
        {
            return Result<CreditGrantDto>.Failure("Credit grant not found", 404);
        }

        var changes = request.Changes;
        var amount = changes.AmountUsd ?? grant.AmountUsd;
        var starts = Utc(changes.StartsAt) ?? grant.StartsAt;
        var expires = Utc(changes.ExpiresAt) ?? grant.ExpiresAt;

        if (amount < 0 || amount > 100_000)
        {
            return Result<CreditGrantDto>.Failure("amount_usd must be between 0 and 100000", 400);
        }

        if (expires <= starts)
        {
            return Result<CreditGrantDto>.Failure("expires_at must be after starts_at", 400);
        }

        var overlaps = await _context.CreditGrants.AnyAsync(g =>
            g.Id != grant.Id && g.Provider == grant.Provider && g.StartsAt < expires && g.ExpiresAt > starts,
            cancellationToken);
        if (overlaps)
        {
            return Result<CreditGrantDto>.Failure("Those dates overlap another credit cycle", 409);
        }

        grant.AmountUsd = amount;
        grant.StartsAt = starts;
        grant.ExpiresAt = expires;
        if (changes.Note is not null) grant.Note = changes.Note.Trim().Length == 0 ? null : changes.Note.Trim();
        grant.Source = "manual";
        grant.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Take effect now rather than at the next scheduled count.
        await _credit.RefreshAsync(cancellationToken);

        return Result<CreditGrantDto>.Success(new CreditGrantDto
        {
            Id = grant.Id,
            AmountUsd = grant.AmountUsd,
            StartsAt = grant.StartsAt,
            ExpiresAt = grant.ExpiresAt,
            Source = grant.Source,
            Note = grant.Note
        });
    }

    private static DateTime? Utc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } v => v,
        { Kind: DateTimeKind.Local } v => v.ToUniversalTime(),
        { } v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
    };
}
