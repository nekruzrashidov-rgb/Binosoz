using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Services;

public class CustomerService(AppDbContext db, EncryptionService encryption, IAuditService audit) : ICustomerService
{
    public async Task<PagedResult<CustomerListItem>> GetAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var p = PageRequest.Normalize(page, pageSize);
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            var digits = new string(text.Where(char.IsDigit).ToArray());
            query = query.Where(c => EF.Functions.ILike(c.FullName, "%" + text + "%")
                                     || (digits.Length >= 3 && c.Phone.Contains(digits)));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.CreatedAt)
            .Skip(p.Skip).Take(p.PageSize)
            .Select(c => new CustomerListItem(c.Id, c.FullName, c.Phone, c.Email, c.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<CustomerListItem>(items, p.Page, p.PageSize, total);
    }

    public async Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Клиент не найден");
        return ToResponse(customer);
    }

    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        var phone = PhoneNumber.Normalize(request.Phone)
            ?? throw new BusinessRuleException("Неверный номер телефона");

        var customer = new Customer
        {
            FullName = request.FullName.Trim(),
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            PassportDataEncrypted = encryption.Encrypt(request.PassportData?.Trim())
        };
        db.Customers.Add(customer);

        if (request.LeadId is { } leadId)
            await ConvertLeadAsync(leadId, customer, ct);

        // Паспортные данные в аудит не попадают: там только факт их наличия.
        audit.Record(nameof(Customer), customer.Id, "Created", null,
            new { customer.FullName, customer.Phone, customer.Email, HasPassportData = customer.PassportDataEncrypted is not null });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsUniqueViolation())
        {
            throw new ConflictException("Клиент с таким телефоном уже есть");
        }

        return ToResponse(customer);
    }

    private async Task ConvertLeadAsync(Guid leadId, Customer customer, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct)
            ?? throw new NotFoundException("Заявка не найдена");

        var before = new { lead.Status };
        lead.Status = LeadStatus.Converted;
        audit.Record(nameof(Lead), lead.Id, "Converted", before, new { lead.Status, CustomerId = customer.Id });

        var bookings = await db.Bookings
            .Where(b => b.LeadId == leadId && b.Status == BookingStatus.Active)
            .ToListAsync(ct);
        foreach (var booking in bookings)
            booking.CustomerId = customer.Id;
    }

    private CustomerResponse ToResponse(Customer c) =>
        new(c.Id, c.FullName, c.Phone, c.Email, encryption.Decrypt(c.PassportDataEncrypted), c.UserId, c.CreatedAt);
}
