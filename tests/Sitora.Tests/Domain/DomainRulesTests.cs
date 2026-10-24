using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;

namespace Sitora.Tests.Domain;

public class DomainRulesTests
{
    [Theory]
    [InlineData("+992 90 123 45 67", "+992901234567")]
    [InlineData("992901234567", "+992901234567")]
    [InlineData("90-123-45-67", "+992901234567")]
    [InlineData("12345", null)]
    [InlineData("+7 900 123 45 67", null)]
    [InlineData("", null)]
    public void PhoneNumber_NormalizesTajikNumbers(string input, string? expected)
    {
        Assert.Equal(expected, PhoneNumber.Normalize(input));
    }

    [Theory]
    [InlineData(46, 9675, 445100)]
    [InlineData(34, 9295, 316000)]
    [InlineData(50.5, 10000, 505000)]
    public void Apartment_PriceIsAreaTimesPricePerM2RoundedTo100(decimal area, decimal pricePerM2, decimal expected)
    {
        Assert.Equal(expected, Apartment.CalculatePrice(area, pricePerM2));
    }

    [Fact]
    public void Apartment_StatusTransitions()
    {
        var apartment = new Apartment();

        apartment.Book();
        Assert.Equal(ApartmentStatus.Booked, apartment.Status);
        Assert.Throws<BusinessRuleException>(apartment.Book);
        Assert.Throws<BusinessRuleException>(() => apartment.SetOnRequest(true));

        apartment.ReleaseBooking();
        Assert.Equal(ApartmentStatus.Free, apartment.Status);

        apartment.SetOnRequest(true);
        Assert.Throws<BusinessRuleException>(apartment.Sell);
        apartment.SetOnRequest(false);

        apartment.Sell();
        Assert.Equal(ApartmentStatus.Sold, apartment.Status);
        Assert.Throws<BusinessRuleException>(apartment.ReleaseBooking);
    }

    [Theory]
    [InlineData(0, 0, 1, PageRequest.DefaultPageSize)]
    [InlineData(3, 500, 3, PageRequest.MaxPageSize)]
    [InlineData(2, 10, 2, 10)]
    public void PageRequest_NormalizesValues(int page, int pageSize, int expectedPage, int expectedSize)
    {
        var result = PageRequest.Normalize(page, pageSize);

        Assert.Equal((expectedPage, expectedSize), (result.Page, result.PageSize));
    }
}
