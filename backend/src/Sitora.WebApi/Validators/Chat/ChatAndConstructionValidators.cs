using FluentValidation;
using Sitora.Domain.DTOs.Chat;
using Sitora.Domain.DTOs.Construction;
using Sitora.Domain.Entities;

namespace Sitora.WebApi.Validators.Chat;

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().WithMessage("Напишите сообщение")
            .MaximumLength(ChatMessage.MaxLength).WithMessage($"Сообщение не длиннее {ChatMessage.MaxLength} символов");
    }
}

public class SaveStageRequestValidator : AbstractValidator<SaveStageRequest>
{
    public SaveStageRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Укажите название этапа").MaximumLength(200);
        RuleFor(x => x.Percent).InclusiveBetween(0, 100).WithMessage("Процент готовности должен быть от 0 до 100");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10_000);
    }
}
