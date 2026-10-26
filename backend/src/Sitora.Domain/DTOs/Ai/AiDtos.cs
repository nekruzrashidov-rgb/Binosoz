namespace Sitora.Domain.DTOs.Ai;

// Role: "user" — реплика человека, "assistant" — ответ помощника. История живёт только в браузере.
public record AiChatMessage(string Role, string Content);

public record AiChatRequest(string Message, IReadOnlyList<AiChatMessage>? History);

public record AiChatResponse(string Reply);
