namespace Sitora.Domain.Exceptions;

public abstract class DomainException(string message) : Exception(message);

public class NotFoundException(string message = "Объект не найден") : DomainException(message);

public class ConflictException(string message) : DomainException(message);

public class ForbiddenException(string message = "Недостаточно прав") : DomainException(message);

public class BusinessRuleException(string message) : DomainException(message);

public class UnauthorizedException(string message) : DomainException(message);

public class TooManyRequestsException(string message) : DomainException(message);

// Внешний сервис не настроен или недоступен (например, AI-помощник без ключа).
public class ServiceUnavailableException(string message) : DomainException(message);
