using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LumeneraViewer.ViewModels;

/// <summary>Одна запись журнала уведомлений.</summary>
public sealed record NotificationItem(DateTime When, string Title, string Message, NotifyKind Kind)
{
    public string TimeText => When.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
}

/// <summary>
/// Журнал уведомлений вместо всплывающих сообщений: новые сверху, не больше <see cref="Capacity"/>.
/// Непрочитанные считаются, пока журнал не открыт (<see cref="IsViewing"/>). Только UI-поток.
/// </summary>
public sealed partial class NotificationLog : ObservableObject
{
    public const int Capacity = 200;

    public ObservableCollection<NotificationItem> Items { get; } = new();

    /// <summary>Сколько записей пришло, пока журнал не смотрели.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasUnread))]
    private int _unreadCount;

    /// <summary>Самая серьёзная из непрочитанных (цвет значка); null — непрочитанных нет.</summary>
    [ObservableProperty]
    private NotifyKind? _unreadSeverity;

    public bool HasUnread => UnreadCount > 0;

    /// <summary>Журнал открыт на экране: новые записи сразу считаются прочитанными.</summary>
    public bool IsViewing
    {
        get => _isViewing;
        set
        {
            if (!SetProperty(ref _isViewing, value)) return;
            if (value) MarkRead();
        }
    }
    private bool _isViewing;

    public void Add(NotificationItem item)
    {
        Items.Insert(0, item);
        while (Items.Count > Capacity) Items.RemoveAt(Items.Count - 1);
        if (IsViewing) return;
        UnreadCount = Math.Min(UnreadCount + 1, Capacity);
        if (UnreadSeverity is not { } s || item.Kind > s) UnreadSeverity = item.Kind;
    }

    public void MarkRead()
    {
        UnreadCount = 0;
        UnreadSeverity = null;
    }

    public void Clear()
    {
        Items.Clear();
        MarkRead();
    }
}
