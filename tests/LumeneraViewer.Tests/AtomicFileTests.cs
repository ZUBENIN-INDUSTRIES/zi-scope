using System.IO;
using System.Text;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Tests;

/// <summary>Общая атомарная запись: временный файл рядом → Flush(true) → замена; при сбое старый файл цел, tmp убран.</summary>
public sealed class AtomicFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "zs-atomic-" + Guid.NewGuid().ToString("N"));

    public AtomicFileTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* уборка */ }
    }

    [Fact]
    public void Write_CreatesFile_NoTmpLeft()
    {
        var path = Path.Combine(_dir, "a.json");

        AtomicFile.Write(path, s => s.Write(Encoding.UTF8.GetBytes("привет")));

        Assert.Equal("привет", File.ReadAllText(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_dir));
    }

    [Fact]
    public void WriteAllText_ReplacesExisting_Utf8WithoutBom()
    {
        var path = Path.Combine(_dir, "b.json");
        File.WriteAllText(path, "старое");

        AtomicFile.WriteAllText(path, "{\"a\":\"ё\"}");

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(Encoding.UTF8.GetBytes("{\"a\":\"ё\"}"), bytes);   // как File.WriteAllText: без BOM
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void FailureMidWrite_KeepsOldFile_RemovesTmp()
    {
        var path = Path.Combine(_dir, "c.json");
        File.WriteAllText(path, "старое");

        Assert.Throws<IOException>(() => AtomicFile.Write(path, s =>
        {
            s.Write(Encoding.UTF8.GetBytes("половина нов"));
            throw new IOException("диск кончился");
        }));

        Assert.Equal("старое", File.ReadAllText(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_dir));
    }

    [Fact]
    public void NoOverwrite_ExistingFile_Throws_OldKept_NoTmp()
    {
        var path = Path.Combine(_dir, "d.png");
        File.WriteAllText(path, "старое");

        Assert.Throws<IOException>(() => AtomicFile.Write(path, s => s.WriteByte(1), overwrite: false));

        Assert.Equal("старое", File.ReadAllText(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_dir));
    }
}
