namespace Sitora.Domain.DTOs.Auth;

public record SmsSendRequest(string Phone);

public record SmsSendResponse(string Message, int ExpiresInSeconds);

public record SmsVerifyRequest(string Phone, string Code);
