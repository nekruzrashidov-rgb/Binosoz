using Sitora.Domain.DTOs.Cash;

namespace Sitora.Domain.Interfaces;

// Кассовая книга дома. Дом и операции чужого дома (или чужой организации) — 404.
public interface ICashService
{
    Task<CashTransactionResponse> AddAsync(Guid buildingId, CreateCashTransactionRequest request, CancellationToken ct = default);
    Task<CashTransactionResponse> ReverseAsync(Guid buildingId, Guid transactionId, string reason, CancellationToken ct = default);
    Task<CashBookResponse> GetBookAsync(Guid buildingId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<byte[]> GetBookExcelAsync(Guid buildingId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<byte[]> GetReceiptPdfAsync(Guid buildingId, Guid transactionId, CancellationToken ct = default);
}
