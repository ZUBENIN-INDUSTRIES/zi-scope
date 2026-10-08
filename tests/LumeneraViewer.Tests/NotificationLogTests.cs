using LumeneraViewer.ViewModels;
using Xunit;

namespace LumeneraViewer.Tests;

public class NotificationLogTests
{
    private static NotificationItem Item(string t, NotifyKind k = NotifyKind.Info) => new(DateTime.Now, t, "", k);

    [Fact]
    public void Add_NewestFirst_AndCountsUnread()
    {
        var log = new NotificationLog();
        log.Add(Item("a"));
        log.Add(Item("b"));
        Assert.Equal(new[] { "b", "a" }, log.Items.Select(i => i.Title));
        Assert.Equal(2, log.UnreadCount);
        Assert.True(log.HasUnread);
    }

    [Fact]
    public void UnreadSeverity_IsTheWorstUnread()
    {
        var log = new NotificationLog();
        log.Add(Item("a", NotifyKind.Success));
        log.Add(Item("b", NotifyKind.Error));
        log.Add(Item("c", NotifyKind.Warning));
        Assert.Equal(NotifyKind.Error, log.UnreadSeverity);
    }

    [Fact]
    public void Viewing_MarksReadAndKeepsNewOnesRead()
    {
        var log = new NotificationLog();
        log.Add(Item("a", NotifyKind.Warning));
        log.IsViewing = true;
        Assert.Equal(0, log.UnreadCount);
        Assert.Null(log.UnreadSeverity);
        log.Add(Item("b", NotifyKind.Error));
        Assert.Equal(0, log.UnreadCount);
        Assert.Equal(2, log.Items.Count);
        log.IsViewing = false;
        log.Add(Item("c"));
        Assert.Equal(1, log.UnreadCount);
        Assert.Equal(NotifyKind.Info, log.UnreadSeverity);
    }

    [Fact]
    public void Capacity_DropsOldest()
    {
        var log = new NotificationLog();
        for (int i = 0; i < NotificationLog.Capacity + 5; i++) log.Add(Item(i.ToString()));
        Assert.Equal(NotificationLog.Capacity, log.Items.Count);
        Assert.Equal((NotificationLog.Capacity + 4).ToString(), log.Items[0].Title);
        Assert.Equal("5", log.Items[^1].Title);
        Assert.Equal(NotificationLog.Capacity, log.UnreadCount);
    }

    [Fact]
    public void Clear_EmptiesAndResetsUnread()
    {
        var log = new NotificationLog();
        log.Add(Item("a", NotifyKind.Error));
        log.Clear();
        Assert.Empty(log.Items);
        Assert.False(log.HasUnread);
        Assert.Null(log.UnreadSeverity);
    }
}

public class SettingsPathArgTests
{
    [Fact]
    public void Parse_FindsSettingsArgument()
    {
        var p = App.ParseSettingsPath(new[] { "--other", "--settings", "x/s.json" });
        Assert.Equal(System.IO.Path.GetFullPath("x/s.json"), p);
    }

    [Fact]
    public void Parse_NoArgumentOrNoValue_ReturnsNull()
    {
        Assert.Null(App.ParseSettingsPath(Array.Empty<string>()));
        Assert.Null(App.ParseSettingsPath(new[] { "--settings" }));
    }
}
