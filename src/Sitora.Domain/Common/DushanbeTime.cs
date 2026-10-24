namespace Sitora.Domain.Common;

// Таджикистан живёт по UTC+5 без перехода на летнее время.
public static class DushanbeTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    public static DateOnly Today(DateTime utcNow) => DateOnly.FromDateTime(utcNow.Add(Offset));

    public static DateTime ToLocal(DateTime utc) => utc.Add(Offset);
}
