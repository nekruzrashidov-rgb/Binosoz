using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.Entities;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Квартиры на типовых этажах блоков (этап 2г): 8 на этаж вокруг лифтового ядра, угловые и торцевые — самые большие,
// сумма квартир + коридор + ядро = площадь этажа по контуру (допуск 5 %).
public class FloorPlanTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task TypicalFloors_8Apartments_CornersLargest_AreasAddUpToTheOutline()
    {
        var blocks = await factory.WithDbAsync(db => db.Buildings.AsNoTracking().Where(b => b.BlockCode != null)
            .Include(b => b.Floors).ThenInclude(f => f.Apartments).OrderBy(b => b.SortOrder).ToListAsync());
        Assert.Equal(3, blocks.Count);
        foreach (var b in blocks)
        {
            var outline = PlanGeometry.Area(PlanGeometry.Expand(b.Footprint!));
            var inner = FloorPlanMath.InnerArea(b.Footprint!, FloorPlanMath.Depth);
            Assert.True(inner > 0, b.BlockCode + ": внутри полосы квартир есть коридор и лифтовой узел");
            foreach (var floor in b.Floors.Where(f => f.Number >= 3 && f.Number < b.FloorsCount))
            {
                var apts = floor.Apartments.OrderBy(a => a.PlanSlot).ToList();
                Assert.Equal(8, apts.Count);
                var sum = (double)apts.Sum(a => a.Area) + inner;
                Assert.True(Math.Abs(sum - outline) / outline <= 0.05, $"{b.BlockCode} {floor.Number}: {sum:0.0} против {outline:0.0} м²");
                // Угловые и торцевые (3–4 комнаты) больше всех остальных квартир этажа.
                var corner = apts.Where(a => a.Rooms >= 3).ToList();
                Assert.True(corner.Count >= 2, b.BlockCode);
                Assert.True(corner.Min(a => a.Area) > apts.Where(a => a.Rooms < 3).Max(a => a.Area), b.BlockCode);
                Assert.Equal(2, apts.Count(a => a.Rooms == 0));
            }
        }
    }

    [Fact]
    public void BandOfASquare_IsOutlineMinusInnerSquare()
    {
        double[][] square = [[0, 0], [20, 0], [20, 20], [0, 20]];
        Assert.Equal(400 - 36, FloorPlanMath.BandArea(square, 7), 6);
        var areas = FloorPlanMath.UnitAreas(square, 7, [1, 1, 2]);
        Assert.Equal([91m, 91m, 182m], areas);
    }
}
