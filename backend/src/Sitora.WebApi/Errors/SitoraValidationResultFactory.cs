using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Results;

namespace Sitora.WebApi.Errors;

// Ответ FluentValidation (автопроверка SharpGrip) — тот же вид, что у встроенной проверки модели: ApiErrors.ValidationResult.
public class SitoraValidationResultFactory : IFluentValidationAutoValidationResultFactory
{
    public Task<IActionResult?> CreateActionResult(ActionExecutingContext context, ValidationProblemDetails validationProblemDetails,
        IDictionary<IValidationContext, ValidationResult> validationResults) =>
        Task.FromResult<IActionResult?>(ApiErrors.ValidationResult(context));
}
