using Sitora.Domain.DTOs.Reviews;
using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Sales;

// Первый экран личного кабинета: есть ли у покупателя сделки, кому звонить и короткая сводка по каждой квартире.
public record CabinetOverviewResponse(
    bool HasDeals,
    string CustomerName,
    CabinetContactsResponse Contacts,
    IReadOnlyList<CabinetDealResponse> Deals);

// Контакты застройщика из настроек организации покупателя; не заданы — null (сайт покажет свои).
public record CabinetContactsResponse(string? OrganizationName, string? Phone, string? Email, string? Telegram, string? Address);

// Сделка для кабинета: квартира, деньги в валюте договора (оплачено — за вычетом сторно), менеджер и свой отзыв о доме.
public record CabinetDealResponse(
    Guid DealId,
    Guid BuildingId,
    string BuildingName,
    int ApartmentNumber,
    int Floor,
    int Rooms,
    decimal Area,
    DealStage Stage,
    decimal FinalPrice,
    Currency Currency,
    decimal Paid,
    decimal Remaining,
    int PaidPercent,
    string? ManagerName,
    DateTime CreatedAt,
    ReviewResponse? MyReview);
