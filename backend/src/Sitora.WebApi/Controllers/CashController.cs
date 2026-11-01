using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Cash;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Кассовая книга дома: бухгалтер и руководители. Чужой дом — 404 (фильтры домов в AppDbContext).
[ApiController]
[Route("api/v1/buildings/{buildingId:guid}/cash")]
[Authorize(Roles = Roles.Accounting)]
[Produces("application/json")]
public class CashController(ICashService cash) : ControllerBase
{
    private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Операции за период (по умолчанию — текущий месяц по Душанбе) и итоги в сомони.</summary>
    [HttpGet]
    [ProducesResponseType<CashBookResponse>(StatusCodes.Status200OK)]
    public Task<CashBookResponse> GetBook(Guid buildingId, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        cash.GetBookAsync(buildingId, from, to, ct);

    [HttpGet("export.xlsx")]
    [Produces(ExcelType)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportExcel(Guid buildingId, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        File(await cash.GetBookExcelAsync(buildingId, from, to, ct), ExcelType, "kassovaya-kniga.xlsx");

    /// <summary>Приход или расход. Сумма положительная, знак ставит направление.</summary>
    [HttpPost]
    [ProducesResponseType<CashTransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CashTransactionResponse>> Add(Guid buildingId, CreateCashTransactionRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await cash.AddAsync(buildingId, request, ct));

    /// <summary>Сторно: обратная запись со ссылкой на исходную операцию. Операции не удаляются.</summary>
    [HttpPost("{txId:guid}/reverse")]
    [ProducesResponseType<CashTransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CashTransactionResponse>> Reverse(Guid buildingId, Guid txId, ReverseCashTransactionRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await cash.ReverseAsync(buildingId, txId, request.Reason, ct));

    [HttpGet("{txId:guid}/receipt.pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Receipt(Guid buildingId, Guid txId, CancellationToken ct) =>
        File(await cash.GetReceiptPdfAsync(buildingId, txId, ct), "application/pdf", "kvitanciya.pdf");
}
