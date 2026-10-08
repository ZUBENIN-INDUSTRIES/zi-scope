using LumeneraViewer.Imaging;

namespace LumeneraViewer.UI.Controls;

/// <summary>
/// Правка точек тональной кривой (без WPF): добавление (не больше <see cref="MaxPoints"/>),
/// удаление (крайние точки не удаляются), перетаскивание с упором в соседей. Координаты 0…255.
/// </summary>
public static class CurveEditing
{
    public const int MaxPoints = 12;

    /// <summary>Минимальный зазор по X между соседними точками.</summary>
    public const double MinGap = 2;

    /// <summary>Точки, отсортированные по X и обрезанные в 0…255.</summary>
    public static List<CurvePoint> Sorted(IEnumerable<CurvePoint> points) =>
        points.Select(p => new CurvePoint(Math.Clamp(p.X, 0, 255), Math.Clamp(p.Y, 0, 255))).OrderBy(p => p.X).ToList();

    /// <summary>Добавить точку. Null — точек уже <see cref="MaxPoints"/> или по X слишком близко к существующей.</summary>
    public static (List<CurvePoint> Points, int Index)? TryAdd(IReadOnlyList<CurvePoint> points, CurvePoint p)
    {
        var list = Sorted(points);
        if (list.Count >= MaxPoints) return null;
        p = new CurvePoint(Math.Clamp(p.X, 0, 255), Math.Clamp(p.Y, 0, 255));
        if (list.Any(q => Math.Abs(q.X - p.X) < MinGap)) return null;
        int i = list.FindIndex(q => q.X > p.X);
        if (i < 0) i = list.Count;
        list.Insert(i, p);
        return (list, i);
    }

    /// <summary>Удалить точку по индексу в отсортированном списке. Null — точка крайняя (первая или последняя).</summary>
    public static List<CurvePoint>? TryRemove(IReadOnlyList<CurvePoint> points, int index)
    {
        var list = Sorted(points);
        if (index <= 0 || index >= list.Count - 1) return null;
        list.RemoveAt(index);
        return list;
    }

    /// <summary>Сдвинуть точку: X — между соседями (с зазором <see cref="MinGap"/>), Y — 0…255.</summary>
    public static List<CurvePoint> Move(IReadOnlyList<CurvePoint> points, int index, CurvePoint p)
    {
        var list = Sorted(points);
        if (index < 0 || index >= list.Count) return list;
        double lo = index > 0 ? list[index - 1].X + MinGap : 0;
        double hi = index < list.Count - 1 ? list[index + 1].X - MinGap : 255;
        double x = lo <= hi ? Math.Clamp(p.X, lo, hi) : list[index].X;
        list[index] = new CurvePoint(x, Math.Clamp(p.Y, 0, 255));
        return list;
    }
}
