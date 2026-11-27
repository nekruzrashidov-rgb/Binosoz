namespace Sitora.Domain.Common;

// Контуры в плане (метры): контур блока Footprint, уступы Setbacks, участок SitePlan. Точка — [x, y] или [x, y, r]:
// r — радиус скругления угла у этой вершины (0 или нет — острый угол). Обход по часовой стрелке (x вправо, y вниз по
// плану, как на экране), без самопересечений. Сайт (frontend/js/planirovki-data.js, S.plans.geom) считает так же.
public static class PlanGeometry
{
    public const int MinVertices = 3, MaxVertices = 64;
    // Скругление рисуется дугой из этого числа отрезков (на сайте — столько же).
    public const int ArcSegments = 6;

    public readonly record struct P(double X, double Y);

    public static IReadOnlyList<P> Vertices(IReadOnlyList<double[]> ring) => ring.Select(p => new P(p[0], p[1])).ToList();

    // Площадь по формуле шнурования (Гаусса), всегда положительная.
    public static double Area(IReadOnlyList<P> ring)
    {
        double s = 0;
        for (var i = 0; i < ring.Count; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Count];
            s += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(s) / 2;
    }

    // Знаковая площадь: > 0 — обход по часовой стрелке в экранных осях (y вниз).
    public static double SignedArea(IReadOnlyList<P> ring)
    {
        double s = 0;
        for (var i = 0; i < ring.Count; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Count];
            s += a.X * b.Y - b.X * a.Y;
        }
        return s / 2;
    }

    // Контур со скруглениями: угол с радиусом r заменяется дугой, вписанной в угол (радиус урезается, если стороны короткие).
    public static IReadOnlyList<P> Expand(IReadOnlyList<double[]> ring)
    {
        var n = ring.Count;
        var result = new List<P>();
        for (var i = 0; i < n; i++)
        {
            var cur = new P(ring[i][0], ring[i][1]);
            var r = ring[i].Length > 2 ? ring[i][2] : 0;
            if (r <= 0)
            {
                result.Add(cur);
                continue;
            }
            var prev = new P(ring[(i - 1 + n) % n][0], ring[(i - 1 + n) % n][1]);
            var next = new P(ring[(i + 1) % n][0], ring[(i + 1) % n][1]);
            var (ux, uy) = Unit(prev.X - cur.X, prev.Y - cur.Y);
            var (vx, vy) = Unit(next.X - cur.X, next.Y - cur.Y);
            var cos = Math.Clamp(ux * vx + uy * vy, -1, 1);
            var angle = Math.Acos(cos);
            if (angle < 1e-3 || Math.PI - angle < 1e-3)
            {
                result.Add(cur);
                continue;
            }
            var tanHalf = Math.Tan(angle / 2);
            var maxT = Math.Min(Dist(prev, cur), Dist(cur, next)) * 0.45;
            var t = Math.Min(r / tanHalf, maxT);
            var rr = t * tanHalf;
            var a = new P(cur.X + ux * t, cur.Y + uy * t);
            var b = new P(cur.X + vx * t, cur.Y + vy * t);
            var (bx, by) = Unit(ux + vx, uy + vy);
            var d = rr / Math.Sin(angle / 2);
            var c = new P(cur.X + bx * d, cur.Y + by * d);
            var a0 = Math.Atan2(a.Y - c.Y, a.X - c.X);
            var a1 = Math.Atan2(b.Y - c.Y, b.X - c.X);
            var sweep = a1 - a0;
            while (sweep > Math.PI) sweep -= 2 * Math.PI;
            while (sweep < -Math.PI) sweep += 2 * Math.PI;
            for (var k = 0; k <= ArcSegments; k++)
            {
                var ang = a0 + sweep * k / ArcSegments;
                result.Add(new P(c.X + rr * Math.Cos(ang), c.Y + rr * Math.Sin(ang)));
            }
        }
        return result;
    }

    // Контур на участке: поворот на rotationDeg (по часовой) вокруг начала координат блока, затем сдвиг на (x, y).
    public static IReadOnlyList<P> Place(IReadOnlyList<P> ring, double x, double y, double rotationDeg)
    {
        var a = rotationDeg * Math.PI / 180;
        var (cos, sin) = (Math.Cos(a), Math.Sin(a));
        return ring.Select(p => new P(x + p.X * cos - p.Y * sin, y + p.X * sin + p.Y * cos)).ToList();
    }

    // Простой многоугольник: несоседние стороны не пересекаются и не касаются, повторяющихся вершин нет.
    public static bool IsSimple(IReadOnlyList<P> ring)
    {
        var n = ring.Count;
        for (var i = 0; i < n; i++)
        {
            if (Dist(ring[i], ring[(i + 1) % n]) < 1e-6)
                return false;
            for (var j = i + 1; j < n; j++)
            {
                if (j == i || (j + 1) % n == i || (i + 1) % n == j)
                    continue;
                if (SegmentsIntersect(ring[i], ring[(i + 1) % n], ring[j], ring[(j + 1) % n]))
                    return false;
            }
        }
        return true;
    }

    // Наименьшее расстояние между контурами; 0 — пересекаются или один внутри другого.
    public static double Distance(IReadOnlyList<P> a, IReadOnlyList<P> b)
    {
        if (Contains(a, b[0]) || Contains(b, a[0]))
            return 0;
        var best = double.MaxValue;
        for (var i = 0; i < a.Count; i++)
        {
            var a1 = a[i];
            var a2 = a[(i + 1) % a.Count];
            for (var j = 0; j < b.Count; j++)
            {
                var b1 = b[j];
                var b2 = b[(j + 1) % b.Count];
                if (SegmentsIntersect(a1, a2, b1, b2))
                    return 0;
                best = Math.Min(best, Math.Min(Math.Min(PointSegment(a1, b1, b2), PointSegment(a2, b1, b2)),
                    Math.Min(PointSegment(b1, a1, a2), PointSegment(b2, a1, a2))));
            }
        }
        return best;
    }

    public static bool Contains(IReadOnlyList<P> ring, P p)
    {
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var (a, b) = (ring[i], ring[j]);
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    // Контур b целиком внутри a (для уступа: верхний этаж не выходит за контур блока).
    public static bool Inside(IReadOnlyList<P> outer, IReadOnlyList<P> inner, double tolerance = 0.01) =>
        inner.All(p => Contains(outer, p) || DistanceToRing(outer, p) <= tolerance)
        && !inner.Select((p, i) => (p, q: inner[(i + 1) % inner.Count]))
            .Any(s => outer.Select((p, i) => (p, q: outer[(i + 1) % outer.Count]))
                .Any(o => ProperCross(s.p, s.q, o.p, o.q)));

    // Контур, сдвинутый внутрь на depth (углы — со срезом по биссектрисе). Ребро, которое сдвиг разворачивает (короткое
    // ребро эркера или скругления), выпадает, соседние сходятся — внутренний контур остаётся простым. Так же считает сайт
    // (S.geom.offsetInward): полоса между контуром и этим контуром — квартиры, внутри — коридор и лифтовой узел.
    public static IReadOnlyList<P> OffsetInward(IReadOnlyList<P> ring, double depth)
    {
        var n = ring.Count;
        var cw = SignedArea(ring) > 0 ? 1 : -1;
        var lines = new (P Origin, P Dir)[n];
        for (var i = 0; i < n; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % n];
            var (dx, dy) = Unit(b.X - a.X, b.Y - a.Y);
            var (nx, ny) = (-dy * cw, dx * cw);
            lines[i] = (new P(a.X + nx * depth, a.Y + ny * depth), new P(dx, dy));
        }

        var alive = Enumerable.Range(0, n).ToList();
        for (var guard = 0; guard < n && alive.Count >= 3; guard++)
        {
            var points = Corners(alive, lines);
            var dropped = -1;
            for (var k = 0; k < alive.Count && dropped < 0; k++)
            {
                var (p, q, d) = (points[k], points[(k + 1) % alive.Count], lines[alive[k]].Dir);
                if ((q.X - p.X) * d.X + (q.Y - p.Y) * d.Y < 1e-6)
                    dropped = k;
            }
            if (dropped < 0)
                break;
            alive.RemoveAt(dropped);
        }
        return alive.Count < 3 ? [] : Corners(alive, lines);

        static List<P> Corners(List<int> alive, (P Origin, P Dir)[] lines) =>
            alive.Select((e, k) => Meet(lines[alive[(k - 1 + alive.Count) % alive.Count]], lines[e])).ToList();

        static P Meet((P Origin, P Dir) l1, (P Origin, P Dir) l2)
        {
            var den = l1.Dir.X * l2.Dir.Y - l1.Dir.Y * l2.Dir.X;
            if (Math.Abs(den) < 1e-9)
                return l2.Origin;
            var t = ((l2.Origin.X - l1.Origin.X) * l2.Dir.Y - (l2.Origin.Y - l1.Origin.Y) * l2.Dir.X) / den;
            return new P(l1.Origin.X + l1.Dir.X * t, l1.Origin.Y + l1.Dir.Y * t);
        }
    }

    public static double DistanceToRing(IReadOnlyList<P> ring, P p) =>
        ring.Select((a, i) => PointSegment(p, a, ring[(i + 1) % ring.Count])).Min();

    private static double PointSegment(P p, P a, P b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var len2 = dx * dx + dy * dy;
        var t = len2 < 1e-12 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
        return Dist(p, new P(a.X + dx * t, a.Y + dy * t));
    }

    private static double Cross(P o, P a, P b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    private static bool OnSegment(P a, P b, P p) =>
        Math.Min(a.X, b.X) - 1e-9 <= p.X && p.X <= Math.Max(a.X, b.X) + 1e-9 &&
        Math.Min(a.Y, b.Y) - 1e-9 <= p.Y && p.Y <= Math.Max(a.Y, b.Y) + 1e-9;

    private static bool SegmentsIntersect(P a, P b, P c, P d)
    {
        var d1 = Cross(c, d, a);
        var d2 = Cross(c, d, b);
        var d3 = Cross(a, b, c);
        var d4 = Cross(a, b, d);
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;
        const double e = 1e-9;
        return (Math.Abs(d1) < e && OnSegment(c, d, a)) || (Math.Abs(d2) < e && OnSegment(c, d, b))
            || (Math.Abs(d3) < e && OnSegment(a, b, c)) || (Math.Abs(d4) < e && OnSegment(a, b, d));
    }

    // Пересечение «насквозь» (общая вершина или касание не считаются).
    private static bool ProperCross(P a, P b, P c, P d)
    {
        var d1 = Cross(c, d, a);
        var d2 = Cross(c, d, b);
        var d3 = Cross(a, b, c);
        var d4 = Cross(a, b, d);
        const double e = 1e-7;
        return ((d1 > e && d2 < -e) || (d1 < -e && d2 > e)) && ((d3 > e && d4 < -e) || (d3 < -e && d4 > e));
    }

    private static double Dist(P a, P b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static (double, double) Unit(double x, double y)
    {
        var l = Math.Sqrt(x * x + y * y);
        return l < 1e-12 ? (0, 0) : (x / l, y / l);
    }
}
