namespace Sitora.Domain.Entities;

// Планировка застройщика (CRM → «Планировки»): квартира с LayoutCode = Code рисуется на сайте по ней (2D-план и 3D
// «внутри»), без подписи «концепт». Json — тот же формат, что у сайта (frontend/js/planirovki-data.js): контур, фасадные
// стороны, комнаты-многоугольники, двери, вход, балкон. Площадь комнат сверяется с площадью квартиры из каталога на сайте.
public class Layout : BaseEntity
{
    public const int CodeMaxLength = 32;

    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Json { get; set; } = "{}";
}
