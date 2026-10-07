using System.Diagnostics;

namespace LumeneraViewer.Camera;

/// <summary>
/// Демо-камера: «микропрепарат» 1920×1440, ~25 к/с. Сцена рендерится один раз,
/// на кадр — только сдвиг (дрейф), размытие по фокусу, яркость (экспозиция·усиление) и шум.
/// </summary>
public sealed class SimulatedCamera : ICamera
{
    private const int W = 1920, H = 1440;
    private const int Margin = 48;                       // запас сцены под дрейф
    private const int SW = W + 2 * Margin, SH = H + 2 * Margin;
    private const int FramePeriodMs = 40;
    private const float NominalExposure = 30f;
    private const int MaxBlurRadius = 14;

    private static readonly CameraProperty[] Supported = { CameraProperty.Exposure, CameraProperty.Gain };
    private static readonly PropertyRange ExposureRange = new(0.1f, 2000f, 30f);
    private static readonly PropertyRange GainRange = new(1f, 16f, 1f);

    private byte[]? _scene;                // SW×SH, BGR
    private byte[]? _blurH, _blurV;        // рабочие буферы размытия W×H, BGR
    private readonly int[] _gainLut = new int[256];
    private volatile float _exposure = ExposureRange.Default;
    private volatile float _gain = GainRange.Default;
    private volatile float _focus = 0.5f;
    private volatile bool _running;
    private Thread? _thread;
    private long _sequence;

    /// <summary>0…1, 0.5 — резко; размытие ∝ |Focus − 0.5|.</summary>
    public float Focus { get => _focus; set => _focus = Math.Clamp(value, 0f, 1f); }

    public CameraInfo? Info { get; private set; }
    public bool IsStreaming => _running;
    public IReadOnlyList<CameraProperty> SupportedProperties => Supported;
    public event Action<CameraFrame>? FrameReceived;
    public event Action<string>? Error;

    public void Open()
    {
        _scene ??= RenderScene();
        Info = new CameraInfo("Симулятор INFINITY 4", "DEMO", W, H, true);
    }

    public void Start()
    {
        if (_running) return;
        Open();
        _running = true;
        _thread = new Thread(Loop) { IsBackground = true, Name = "Simulated camera" };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        if (_thread is not null && _thread != Thread.CurrentThread) _thread.Join(2000);
        _thread = null;
    }

    public void Close()
    {
        Stop();
        Info = null;
    }

    public void Dispose() => Close();

    public PropertyRange? GetRange(CameraProperty p) => p switch
    {
        CameraProperty.Exposure => ExposureRange,
        CameraProperty.Gain => GainRange,
        _ => null,
    };

    public float? Get(CameraProperty p) => p switch
    {
        CameraProperty.Exposure => _exposure,
        CameraProperty.Gain => _gain,
        _ => null,
    };

    public bool Set(CameraProperty p, float value)
    {
        if (float.IsNaN(value)) return false;
        switch (p)
        {
            case CameraProperty.Exposure: _exposure = Math.Clamp(value, ExposureRange.Min, ExposureRange.Max); return true;
            case CameraProperty.Gain: _gain = Math.Clamp(value, GainRange.Min, GainRange.Max); return true;
            default: return false;
        }
    }

    private void Loop()
    {
        var clock = Stopwatch.StartNew();
        long next = 0;
        while (_running)
        {
            try
            {
                var frame = RenderFrame(clock.Elapsed.TotalSeconds);
                FrameReceived?.Invoke(frame);
            }
            catch (Exception ex)
            {
                Error?.Invoke($"Ошибка симулятора: {ex.Message}");
            }
            next += FramePeriodMs;
            long wait = next - clock.ElapsedMilliseconds;
            if (wait > 0) Thread.Sleep((int)wait);
            else next = clock.ElapsedMilliseconds;   // не догоняем пропущенные кадры
        }
    }

    private unsafe CameraFrame RenderFrame(double t)
    {
        var scene = _scene!;
        // Медленный дрейф препарата
        int ox = Margin + (int)Math.Round(Margin * 0.9 * Math.Sin(2 * Math.PI * t / 90));
        int oy = Margin + (int)Math.Round(Margin * 0.9 * Math.Sin(2 * Math.PI * t / 70 + 1));
        int radius = (int)Math.Round(Math.Abs(_focus - 0.5f) * 2 * MaxBlurRadius);
        float gain = _gain;
        float factor = _exposure * gain / NominalExposure;
        int noiseAmp = (int)Math.Round(3 * Math.Sqrt(gain));
        for (int i = 0; i < 256; i++) _gainLut[i] = (int)Math.Round(i * factor);

        byte[] src;
        int srcStride, srcOffset;
        if (radius == 0)
        {
            src = scene; srcStride = SW * 3; srcOffset = oy * SW * 3 + ox * 3;
        }
        else
        {
            _blurH ??= new byte[W * H * 3];
            _blurV ??= new byte[W * H * 3];
            BlurHorizontal(scene, _blurH, ox, oy, radius);
            BlurVertical(_blurH, _blurV, radius);
            src = _blurV; srcStride = W * 3; srcOffset = 0;
        }

        var dst = new byte[W * H * 3];
        uint seed = (uint)Interlocked.Increment(ref _sequence);
        long seq = seed;
        var lutArr = _gainLut;
        fixed (byte* sp = src)
        fixed (byte* dp = dst)
        fixed (int* lp = lutArr)
        {
            nint s0 = (nint)(sp + srcOffset), d0 = (nint)dp, l0 = (nint)lp;
            Parallel.For(0, H, y =>
            {
                byte* s = (byte*)s0 + (long)y * srcStride;
                byte* d = (byte*)d0 + (long)y * W * 3;
                int* lut = (int*)l0;
                uint rng = (seed * 2654435761u) ^ ((uint)y * 40503u) | 1u;
                int span = 2 * noiseAmp + 1;
                for (int i = 0; i < W * 3; i++)
                {
                    rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                    int v = lut[s[i]] + (int)(rng % (uint)span) - noiseAmp;
                    d[i] = (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
                }
            });
        }
        return new CameraFrame { Bgr24 = dst, Width = W, Height = H, Sequence = seq, Timestamp = DateTime.Now };
    }

    // Box-размытие по строкам окна W×H сцены, начиная с (ox, oy); соседи берутся из запаса сцены
    private static void BlurHorizontal(byte[] scene, byte[] dst, int ox, int oy, int r)
    {
        int div = 2 * r + 1;
        Parallel.For(0, H, y =>
        {
            int row = (y + oy) * SW * 3;
            int outRow = y * W * 3;
            for (int c = 0; c < 3; c++)
            {
                int sum = 0;
                for (int k = -r; k <= r; k++) sum += scene[row + Math.Clamp(ox + k, 0, SW - 1) * 3 + c];
                for (int x = 0; x < W; x++)
                {
                    dst[outRow + x * 3 + c] = (byte)(sum / div);
                    int add = Math.Clamp(ox + x + r + 1, 0, SW - 1), sub = Math.Clamp(ox + x - r, 0, SW - 1);
                    sum += scene[row + add * 3 + c] - scene[row + sub * 3 + c];
                }
            }
        });
    }

    // Box-размытие по столбцам полосами по 64 px (бережно к кэшу)
    private static void BlurVertical(byte[] src, byte[] dst, int r)
    {
        const int Band = 64 * 3;
        int stride = W * 3, div = 2 * r + 1;
        int bands = (stride + Band - 1) / Band;
        Parallel.For(0, bands, bi =>
        {
            int c0 = bi * Band, c1 = Math.Min(stride, c0 + Band), n = c1 - c0;
            Span<int> sums = stackalloc int[Band];
            sums.Clear();
            for (int k = -r; k <= r; k++)
            {
                int row = Math.Clamp(k, 0, H - 1) * stride;
                for (int i = 0; i < n; i++) sums[i] += src[row + c0 + i];
            }
            for (int y = 0; y < H; y++)
            {
                int outRow = y * stride;
                int addRow = Math.Clamp(y + r + 1, 0, H - 1) * stride, subRow = Math.Clamp(y - r, 0, H - 1) * stride;
                for (int i = 0; i < n; i++)
                {
                    dst[outRow + c0 + i] = (byte)(sums[i] / div);
                    sums[i] += src[addRow + c0 + i] - src[subRow + c0 + i];
                }
            }
        });
    }

    // Сцена светлого поля: фон с виньеткой и пятнами + 40–80 окрашенных клеток (пропускание перемножается)
    private static byte[] RenderScene()
    {
        var rnd = new Random(20261007);
        // Пропускание по каналам R, G, B
        var tr = new float[SW * SH];
        var tg = new float[SW * SH];
        var tb = new float[SW * SH];
        Array.Fill(tr, 1f); Array.Fill(tg, 1f); Array.Fill(tb, 1f);

        (float r, float g, float b)[] stains =
        {
            (0.92f, 0.55f, 0.78f),   // розовый эозин
            (0.70f, 0.50f, 0.85f),   // сиреневый
            (0.85f, 0.65f, 0.70f),   // бледно-розовый
            (0.60f, 0.62f, 0.88f),   // голубоватый
        };

        int count = rnd.Next(40, 81);
        for (int n = 0; n < count; n++)
        {
            float cx = rnd.Next(SW), cy = rnd.Next(SH);
            float a = 25 + (float)rnd.NextDouble() * 45;
            float bAxis = a * (0.6f + (float)rnd.NextDouble() * 0.4f);
            float ang = (float)(rnd.NextDouble() * Math.PI);
            float cos = MathF.Cos(ang), sin = MathF.Sin(ang);
            var st = stains[rnd.Next(stains.Length)];
            float density = 0.35f + (float)rnd.NextDouble() * 0.3f;
            // Ядро: смещено от центра, тёмно-фиолетовое
            float nu = ((float)rnd.NextDouble() - 0.5f) * 0.4f * a, nv = ((float)rnd.NextDouble() - 0.5f) * 0.4f * bAxis;
            float na = a * (0.28f + (float)rnd.NextDouble() * 0.12f), nb = na * (0.75f + (float)rnd.NextDouble() * 0.25f);

            int x0 = Math.Max(0, (int)(cx - a - 4)), x1 = Math.Min(SW - 1, (int)(cx + a + 4));
            int y0 = Math.Max(0, (int)(cy - a - 4)), y1 = Math.Min(SH - 1, (int)(cy + a + 4));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float u = dx * cos + dy * sin, v = -dx * sin + dy * cos;
                    float d = MathF.Sqrt(u * u / (a * a) + v * v / (bAxis * bAxis));
                    if (d > 1.08f) continue;
                    float inside = 1 - SmoothStep(0.9f, 1.0f, d);
                    float ring = MathF.Exp(-MathF.Pow((d - 0.95f) / 0.045f, 2));
                    float du = (u - nu) / na, dv = (v - nv) / nb;
                    float nuc = 1 - SmoothStep(0.85f, 1.0f, MathF.Sqrt(du * du + dv * dv));

                    int i = y * SW + x;
                    tr[i] *= Mix(st.r, density * inside) * Mix(st.r * 0.6f, 0.55f * ring) * Mix(0.38f, 0.85f * nuc);
                    tg[i] *= Mix(st.g, density * inside) * Mix(st.g * 0.6f, 0.55f * ring) * Mix(0.25f, 0.85f * nuc);
                    tb[i] *= Mix(st.b, density * inside) * Mix(st.b * 0.6f, 0.55f * ring) * Mix(0.55f, 0.85f * nuc);
                }
        }

        var scene = new byte[SW * SH * 3];
        float hx = SW / 2f, hy = SH / 2f, maxR2 = hx * hx + hy * hy;
        for (int y = 0; y < SH; y++)
            for (int x = 0; x < SW; x++)
            {
                float dx = x - hx, dy = y - hy;
                float vignette = 1 - 0.35f * (dx * dx + dy * dy) / maxR2;
                float mottle = 1 + 0.025f * MathF.Sin(x * 0.011f + MathF.Sin(y * 0.007f) * 2) * MathF.Cos(y * 0.013f);
                float light = 0.85f * vignette * mottle;
                int i = y * SW + x, p = i * 3;
                scene[p] = ToByte(light * 0.93f * tb[i]);
                scene[p + 1] = ToByte(light * 0.96f * tg[i]);
                scene[p + 2] = ToByte(light * 1.00f * tr[i]);
            }
        return scene;
    }

    private static float Mix(float tint, float amount) => 1 + (tint - 1) * Math.Clamp(amount, 0f, 1f);

    private static float SmoothStep(float e0, float e1, float x)
    {
        float t = Math.Clamp((x - e0) / (e1 - e0), 0f, 1f);
        return t * t * (3 - 2 * t);
    }

    private static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255), 0, 255);
}
