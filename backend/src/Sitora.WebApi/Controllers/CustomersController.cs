using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Паспортные данные отдаются только здесь, поэтому весь контроллер — для Manager и Admin.
[ApiController]
[Route("api/v1/customers")]
[Authorize(Roles = Roles.Sales)]
[Produces("application/json")]
public class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CustomerListItem>> List(string? search, int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        customers.GetAsync(search, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<CustomerResponse> Get(Guid id, CancellationToken ct) =>
        customers.GetByIdAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var customer = await customers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, customer);
    }
}
