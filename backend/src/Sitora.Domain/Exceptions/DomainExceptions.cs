namespace Sitora.Domain.Exceptions;

public abstract class DomainException(string message) : Exception(message)
{
    // Дополнительные поля ответа Problem Details (например, attemptsLeft, lockedUntil): фронт показывает их без разбора текста.
    public Dictionary<string, object?> Extensions { get; } = new(StringComparer.Ordinal);
}

public class NotFoundException(string message = "Объект не найден") : DomainException(message);

public class ConflictException(string message) : DomainException(message);

public class ForbiddenException(string message = "Недостаточно прав") : DomainException(message);

public class BusinessRuleException(string message) : DomainException(message);

// Лимит тарифа исчерпан (домов, сотрудников) — 402: нужен тариф выше.
public class PaymentRequiredException(string message) : DomainException(message);

// Запрос не к тому адресу (например, покупатель пришёл во вход по паролю) — 400.
public class BadRequestException(string message) : DomainException(message);

public class UnauthorizedException(string message) : DomainException(message);

public class TooManyRequestsException(string message) : DomainException(message);

// Внешний сервис не настроен или недоступен (например, AI-помощник без ключа).
public class ServiceUnavailableException(string message) : DomainException(message);
