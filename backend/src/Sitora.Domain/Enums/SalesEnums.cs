namespace Sitora.Domain.Enums;

public enum BookingStatus
{
    Active,
    Expired,
    Cancelled,
    Converted
}

// Порядок значений важен: по нему определяется движение сделки вперёд и назад.
public enum DealStage
{
    Lead,
    Showing,
    Booking,
    Contract,
    Payment,
    Completed,
    Cancelled
}
