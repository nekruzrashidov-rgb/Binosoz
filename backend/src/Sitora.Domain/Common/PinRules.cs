namespace Sitora.Domain.Common;

// Код входа покупателя (PIN): ровно 6 цифр, не одинаковые и не подряд (123456, 654321, 012345…).
// Хранится как пароль Identity (стандартный хешер); правила применяет PinAwarePasswordValidator только к роли Customer.
public static class PinRules
{
    public const int Length = 6;
    public const string FormatMessage = "Код входа — ровно 6 цифр";
    public const string SameDigitsMessage = "Код слишком простой: не используйте одинаковые цифры";
    public const string SequenceMessage = "Код слишком простой: не используйте цифры подряд, как 123456 или 654321";

    // Текст ошибки или null, если код подходит.
    public static string? Check(string? pin)
    {
        if (pin is null || pin.Length != Length || !pin.All(char.IsAsciiDigit))
            return FormatMessage;
        if (pin.All(c => c == pin[0]))
            return SameDigitsMessage;
        if (IsStep(pin, 1) || IsStep(pin, -1))
            return SequenceMessage;
        return null;
    }

    public static bool IsValid(string? pin) => Check(pin) is null;

    private static bool IsStep(string pin, int step)
    {
        for (var i = 1; i < pin.Length; i++)
            if (pin[i] - pin[i - 1] != step)
                return false;
        return true;
    }
}
