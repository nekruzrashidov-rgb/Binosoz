using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Sales;

public record MoneyTotal(Currency Currency, decimal Amount);

public record CrmSummaryResponse(
    int NewLeads,
    int LeadsInProgress,
    int ActiveBookings,
    int BookingsExpiringIn24Hours,
    IReadOnlyDictionary<DealStage, int> DealsByStage,
    IReadOnlyDictionary<ApartmentStatus, int> ApartmentsByStatus,
    IReadOnlyList<MoneyTotal> SoldAmount);

public enum PipelineCardKind
{
    Lead,
    Booking,
    Deal
}

public record PipelineCard(
    PipelineCardKind Kind,
    Guid Id,
    string ClientName,
    string Phone,
    Guid? ApartmentId,
    int? ApartmentNumber,
    decimal? Amount,
    Currency? Currency,
    DateTime CreatedAt,
    DateTime? ExpiresAt);

public record PipelineColumn(DealStage Stage, int Count, IReadOnlyList<PipelineCard> Cards);

public record PipelineResponse(IReadOnlyList<PipelineColumn> Columns);
