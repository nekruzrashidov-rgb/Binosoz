namespace Sitora.Domain.Enums;

public enum PaymentMethod
{
    Cash,
    BankTransfer,
    Card,
    Other
}

public enum ScheduleItemKind
{
    DownPayment,
    Monthly
}

public enum ScheduleItemStatus
{
    Pending,
    PartiallyPaid,
    Paid
}

public enum RateSource
{
    Manual,
    Nbt
}

public enum DocumentType
{
    Contract,
    Annex,
    Act,
    Invoice,
    Receipt,
    Other
}
