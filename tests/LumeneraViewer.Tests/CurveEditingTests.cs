using LumeneraViewer.Imaging;
using LumeneraViewer.UI.Controls;

namespace LumeneraViewer.Tests;

/// <summary>Правила редактора кривой из таска 05: до 12 точек, крайние не удаляются, точки не перескакивают соседей.</summary>
public sealed class CurveEditingTests
{
    private static readonly CurvePoint[] Diagonal = [new(0, 0), new(255, 255)];

    [Fact]
    public void Add_InsertsInOrderAndReturnsIndex()
    {
        var r = CurveEditing.TryAdd(Diagonal, new CurvePoint(128, 160));

        Assert.NotNull(r);
        Assert.Equal([new CurvePoint(0, 0), new CurvePoint(128, 160), new CurvePoint(255, 255)], r.Value.Points);
        Assert.Equal(1, r.Value.Index);
    }

    [Fact]
    public void Add_RefusesThirteenthPoint()
    {
        // 12 точек: 0, 20, 40 … 200, 255
        var pts = Enumerable.Range(0, 11).Select(i => new CurvePoint(i * 20, i * 20)).Append(new CurvePoint(255, 255)).ToArray();

        Assert.Null(CurveEditing.TryAdd(pts, new CurvePoint(230, 230)));
    }

    [Fact]
    public void Remove_KeepsEndpointsAndDeletesMiddle()
    {
        CurvePoint[] pts = [new(0, 0), new(100, 140), new(255, 255)];

        Assert.Null(CurveEditing.TryRemove(pts, 0));
        Assert.Null(CurveEditing.TryRemove(pts, 2));
        Assert.Equal(Diagonal, CurveEditing.TryRemove(pts, 1));
    }

    [Fact]
    public void Move_StopsAtNeighboursAndClampsY()
    {
        CurvePoint[] pts = [new(0, 0), new(100, 100), new(150, 150), new(255, 255)];

        // Тянем вторую точку за третью и выше 255: X упирается в 150 − 2, Y — в 255
        var moved = CurveEditing.Move(pts, 1, new CurvePoint(200, 300));

        Assert.Equal([new CurvePoint(0, 0), new CurvePoint(148, 255), new CurvePoint(150, 150), new CurvePoint(255, 255)], moved);
    }

    [Fact]
    public void Add_RefusesPointCloserThanMinGap()
    {
        CurvePoint[] pts = [new(0, 0), new(100, 100), new(255, 255)];

        // 101 — в 1 от точки 100 (зазор 2), 254 — в 1 от крайней 255
        Assert.Null(CurveEditing.TryAdd(pts, new CurvePoint(101, 50)));
        Assert.Null(CurveEditing.TryAdd(pts, new CurvePoint(254, 50)));
        Assert.NotNull(CurveEditing.TryAdd(pts, new CurvePoint(102, 50)));
    }

    [Fact]
    public void Move_EndpointsStopAtBoundsAndNeighbour()
    {
        CurvePoint[] pts = [new(0, 0), new(30, 60), new(255, 255)];

        // Первая точка: влево — упор в 0, вправо — в 30 − 2 = 28
        Assert.Equal(new CurvePoint(0, 20), CurveEditing.Move(pts, 0, new CurvePoint(-10, 20))[0]);
        Assert.Equal(new CurvePoint(28, 0), CurveEditing.Move(pts, 0, new CurvePoint(50, -5))[0]);
        // Последняя: вправо и вверх — упор в 255, влево — в 30 + 2 = 32
        Assert.Equal(new CurvePoint(255, 255), CurveEditing.Move(pts, 2, new CurvePoint(300, 400))[2]);
        Assert.Equal(new CurvePoint(32, 200), CurveEditing.Move(pts, 2, new CurvePoint(10, 200))[2]);
    }

    [Fact]
    public void RemoveAndMove_AcceptUnsortedInput()
    {
        // Индекс — в отсортированном порядке: 0 → (0,0), 1 → (100,140), 2 → (255,255)
        CurvePoint[] pts = [new(255, 255), new(100, 140), new(0, 0)];

        Assert.Null(CurveEditing.TryRemove(pts, 0));
        Assert.Equal(Diagonal, CurveEditing.TryRemove(pts, 1));
        Assert.Equal([new CurvePoint(0, 0), new CurvePoint(120, 90), new CurvePoint(255, 255)],
            CurveEditing.Move(pts, 1, new CurvePoint(120, 90)));
    }
}
