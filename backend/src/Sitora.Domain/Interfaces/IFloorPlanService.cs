namespace Sitora.Domain.Interfaces;

// Планы этажей дома. Чужой дом, этаж без плана или несуществующий этаж — 404.
public interface IFloorPlanService
{
    // Номера этажей, для которых есть план.
    Task<IReadOnlyList<int>> FloorsWithPlanAsync(Guid buildingId, CancellationToken ct = default);

    // SVG с цветами по статусу квартир, подсказками и data-атрибутами для клика.
    Task<string> RenderAsync(Guid buildingId, int floor, CancellationToken ct = default);

    // Сохраняет шаблон после проверки: только безопасные элементы SVG, без скриптов и обработчиков.
    Task SaveAsync(Guid buildingId, int floor, string svgTemplate, CancellationToken ct = default);
}
