using System.Text.Json.Serialization;

namespace LumeneraViewer.Imaging;

/// <summary>Точка тональной кривой: X — вход, Y — выход, обе 0…255.</summary>
public readonly record struct CurvePoint(double X, double Y);

/// <summary>Уровни канала: точка чёрного, точка белого (0…255) и гамма (&gt; 0, 1 — без изменений).</summary>
public sealed record ChannelLevels(double Black = 0, double White = 255, double Gamma = 1)
{
    public static ChannelLevels Neutral { get; } = new();

    [JsonIgnore]
    public bool IsNeutral => Black == 0 && White == 255 && Gamma == 1;

    /// <summary>Значение 0…1 → 0…1.</summary>
    internal double Map(double v)
    {
        if (IsNeutral) return v;
        double b = Math.Clamp(Black, 0, 255) / 255.0;
        double w = Math.Clamp(White, 0, 255) / 255.0;
        // Белое не выше чёрного (меньше 1 шага) — жёсткий порог по точке чёрного
        if (w - b < 1 / 255.0) return v >= b ? 1 : 0;
        v = Math.Clamp((v - b) / (w - b), 0, 1);
        return Gamma == 1 ? v : Math.Pow(v, 1 / Math.Max(0.01, Gamma));
    }
}

/// <summary>Коррекция одного цветового сектора HSL: оттенок, насыщенность, светлота −100…100.</summary>
public sealed record HslAdjust(double Hue = 0, double Saturation = 0, double Luminance = 0)
{
    [JsonIgnore]
    public bool IsNeutral => Hue == 0 && Saturation == 0 && Luminance == 0;
}

/// <summary>Точечная тональная кривая с гладкой монотонной интерполяцией (Фрич — Карлсон).</summary>
public sealed record ToneCurve(IReadOnlyList<CurvePoint> Points)
{
    private static readonly CurvePoint[] IdentityPoints = [new(0, 0), new(255, 255)];

    public static ToneCurve Identity { get; } = new(IdentityPoints);

    /// <summary>Null и пустой список — диагональ (Identity); нормализуется и в конструкторе, и в <c>with</c>.</summary>
    public IReadOnlyList<CurvePoint> Points { get => _points; init => _points = Normalize(value); }

    private readonly IReadOnlyList<CurvePoint> _points = Normalize(Points);

    private static IReadOnlyList<CurvePoint> Normalize(IReadOnlyList<CurvePoint>? p) => p is { Count: > 0 } ? p : IdentityPoints;

    /// <summary>Все точки лежат на диагонали — кривая ничего не меняет.</summary>
    [JsonIgnore]
    public bool IsIdentity
    {
        get
        {
            foreach (var p in Points)
                if (Math.Abs(Math.Clamp(p.X, 0, 255) - Math.Clamp(p.Y, 0, 255)) > 1e-9) return false;
            return true;
        }
    }

    /// <summary>Таблица 256 значений: точки сортируются по X, за крайними точками — их Y, выход в 0…255.</summary>
    public byte[] BuildLut()
    {
        var f = CreateEvaluator();
        var lut = new byte[256];
        for (int i = 0; i < 256; i++) lut[i] = (byte)Math.Round(Math.Clamp(f(i), 0, 255));
        return lut;
    }

    /// <summary>Функция кривой на 0…255 (без округления).</summary>
    internal Func<double, double> CreateEvaluator()
    {
        // Сортировка, обрезка в 0…255, одинаковые X — берём последнюю
        var pts = Points
            .Select(p => new CurvePoint(Math.Clamp(p.X, 0, 255), Math.Clamp(p.Y, 0, 255)))
            .OrderBy(p => p.X)
            .ToList();
        for (int i = pts.Count - 2; i >= 0; i--)
            if (pts[i + 1].X - pts[i].X < 1e-9) pts.RemoveAt(i);
        if (pts.Count == 0) return x => x;
        if (pts.Count == 1) { double y0 = pts[0].Y; return _ => y0; }

        int n = pts.Count;
        var xs = pts.Select(p => p.X).ToArray();
        var ys = pts.Select(p => p.Y).ToArray();
        var d = new double[n - 1];
        for (int i = 0; i < n - 1; i++) d[i] = (ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]);
        var m = new double[n];
        m[0] = d[0];
        m[n - 1] = d[n - 2];
        for (int i = 1; i < n - 1; i++) m[i] = d[i - 1] * d[i] <= 0 ? 0 : (d[i - 1] + d[i]) / 2;
        // Фрич — Карлсон: ограничение касательных для монотонности
        for (int i = 0; i < n - 1; i++)
        {
            if (d[i] == 0) { m[i] = 0; m[i + 1] = 0; continue; }
            double a = m[i] / d[i], b = m[i + 1] / d[i];
            if (a < 0) { m[i] = 0; a = 0; }
            if (b < 0) { m[i + 1] = 0; b = 0; }
            double s = a * a + b * b;
            if (s > 9)
            {
                double t = 3 / Math.Sqrt(s);
                m[i] = t * a * d[i];
                m[i + 1] = t * b * d[i];
            }
        }

        return x =>
        {
            if (x <= xs[0]) return ys[0];
            if (x >= xs[n - 1]) return ys[n - 1];
            int k = Array.BinarySearch(xs, x);
            if (k >= 0) return ys[k];
            k = ~k - 1;
            double h = xs[k + 1] - xs[k], t = (x - xs[k]) / h, t2 = t * t, t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * ys[k] + (t3 - 2 * t2 + t) * h * m[k]
                 + (-2 * t3 + 3 * t2) * ys[k + 1] + (t3 - t2) * h * m[k + 1];
        };
    }

    public bool Equals(ToneCurve? other) =>
        other is not null && (ReferenceEquals(this, other) || Points.SequenceEqual(other.Points));

    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (var p in Points) h.Add(p);
        return h.ToHashCode();
    }
}

/// <summary>
/// Глубокая цветокоррекция. Усиления RGB 0.5…2 (1 — без изменений); ExposureEv −3…3;
/// Highlights, Shadows, Whites, Blacks, Clarity, Vibrance, Dehaze −100…100;
/// Hsl — ровно 8 секторов: Red, Orange, Yellow, Green, Aqua, Blue, Purple, Magenta;
/// SharpenAmount 0…150, SharpenRadius 0.5…3 px.
/// </summary>
public sealed record DeepColor
{
    /// <summary>Число секторов HSL.</summary>
    public const int HslCount = 8;

    private static readonly HslAdjust[] NeutralHsl = Enumerable.Range(0, HslCount).Select(_ => new HslAdjust()).ToArray();

    public static DeepColor Neutral { get; } = new();

    public double RedGain { get; init; } = 1;
    public double GreenGain { get; init; } = 1;
    public double BlueGain { get; init; } = 1;

    public ChannelLevels Master { get => _master; init => _master = value ?? ChannelLevels.Neutral; }
    public ChannelLevels Red { get => _red; init => _red = value ?? ChannelLevels.Neutral; }
    public ChannelLevels Green { get => _green; init => _green = value ?? ChannelLevels.Neutral; }
    public ChannelLevels Blue { get => _blue; init => _blue = value ?? ChannelLevels.Neutral; }

    public double ExposureEv { get; init; }
    public double Highlights { get; init; }
    public double Shadows { get; init; }
    public double Whites { get; init; }
    public double Blacks { get; init; }

    public double Clarity { get; init; }
    public double Vibrance { get; init; }
    public double Dehaze { get; init; }

    /// <summary>Ровно 8 элементов; при чтении недостающие дополняются нейтральными, лишние отбрасываются.</summary>
    public IReadOnlyList<HslAdjust> Hsl { get => _hsl; init => _hsl = NormalizeHsl(value); }

    public double SharpenAmount { get; init; }
    public double SharpenRadius { get; init; } = 1.0;

    public ToneCurve CurveMaster { get => _curveMaster; init => _curveMaster = value ?? ToneCurve.Identity; }
    public ToneCurve CurveRed { get => _curveRed; init => _curveRed = value ?? ToneCurve.Identity; }
    public ToneCurve CurveGreen { get => _curveGreen; init => _curveGreen = value ?? ToneCurve.Identity; }
    public ToneCurve CurveBlue { get => _curveBlue; init => _curveBlue = value ?? ToneCurve.Identity; }

    private readonly ChannelLevels _master = ChannelLevels.Neutral, _red = ChannelLevels.Neutral,
        _green = ChannelLevels.Neutral, _blue = ChannelLevels.Neutral;
    private readonly IReadOnlyList<HslAdjust> _hsl = NeutralHsl;
    private readonly ToneCurve _curveMaster = ToneCurve.Identity, _curveRed = ToneCurve.Identity,
        _curveGreen = ToneCurve.Identity, _curveBlue = ToneCurve.Identity;

    /// <summary>Заменить один сектор HSL (0 — Red … 7 — Magenta).</summary>
    public DeepColor WithHsl(int index, HslAdjust value)
    {
        var list = Hsl.ToArray();
        list[index] = value;
        return this with { Hsl = list };
    }

    [JsonIgnore]
    public bool IsNeutral => IsPointNeutral && !HasLocalOps;

    /// <summary>Нет поточечных изменений (шаги 1–6 конвейера).</summary>
    [JsonIgnore]
    internal bool IsPointNeutral =>
        RedGain == 1 && GreenGain == 1 && BlueGain == 1 && ExposureEv == 0 && !HasTone
        && !HasLevelsOrCurves && !HasHslOrVibrance;

    internal bool HasTone => Highlights != 0 || Shadows != 0 || Whites != 0 || Blacks != 0;

    internal bool HasLevelsOrCurves =>
        !Master.IsNeutral || !Red.IsNeutral || !Green.IsNeutral || !Blue.IsNeutral
        || !CurveMaster.IsIdentity || !CurveRed.IsIdentity || !CurveGreen.IsIdentity || !CurveBlue.IsIdentity;

    internal bool HasHslOrVibrance => Vibrance != 0 || Hsl.Any(x => !x.IsNeutral);

    internal bool HasLocalOps => Clarity != 0 || Dehaze != 0 || SharpenAmount > 0;

    private static IReadOnlyList<HslAdjust> NormalizeHsl(IReadOnlyList<HslAdjust>? v)
    {
        if (v is null) return NeutralHsl;
        var a = new HslAdjust[HslCount];
        for (int i = 0; i < HslCount; i++) a[i] = i < v.Count && v[i] is { } x ? x : new HslAdjust();
        return a;
    }

    public bool Equals(DeepColor? o) =>
        o is not null && (ReferenceEquals(this, o) ||
        RedGain == o.RedGain && GreenGain == o.GreenGain && BlueGain == o.BlueGain
        && Master == o.Master && Red == o.Red && Green == o.Green && Blue == o.Blue
        && ExposureEv == o.ExposureEv && Highlights == o.Highlights && Shadows == o.Shadows
        && Whites == o.Whites && Blacks == o.Blacks
        && Clarity == o.Clarity && Vibrance == o.Vibrance && Dehaze == o.Dehaze
        && Hsl.SequenceEqual(o.Hsl)
        && SharpenAmount == o.SharpenAmount && SharpenRadius == o.SharpenRadius
        && CurveMaster == o.CurveMaster && CurveRed == o.CurveRed
        && CurveGreen == o.CurveGreen && CurveBlue == o.CurveBlue);

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(RedGain); h.Add(GreenGain); h.Add(BlueGain);
        h.Add(Master); h.Add(Red); h.Add(Green); h.Add(Blue);
        h.Add(ExposureEv); h.Add(Highlights); h.Add(Shadows); h.Add(Whites); h.Add(Blacks);
        h.Add(Clarity); h.Add(Vibrance); h.Add(Dehaze);
        foreach (var x in Hsl) h.Add(x);
        h.Add(SharpenAmount); h.Add(SharpenRadius);
        h.Add(CurveMaster); h.Add(CurveRed); h.Add(CurveGreen); h.Add(CurveBlue);
        return h.ToHashCode();
    }
}
