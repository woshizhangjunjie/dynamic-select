using System.Drawing;
using Forms = System.Windows.Forms;

namespace DailyWords.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _playMenuItem;
    private readonly Forms.ToolStripMenuItem _filterMenuItem;

    public event EventHandler? ShowRequested;

    public event EventHandler? PreviousRequested;

    public event EventHandler? TogglePlayRequested;

    public event EventHandler? NextRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? RebuildRequested;

    public event EventHandler? ToggleFilterRequested;

    public event EventHandler? ExitRequested;

    public TrayService()
    {
        _playMenuItem = new Forms.ToolStripMenuItem("暂停");
        _filterMenuItem = new Forms.ToolStripMenuItem("仅看未掌握");

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("上一个", null, (_, _) => PreviousRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(_playMenuItem);
        menu.Items.Add("下一个", null, (_, _) => NextRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_filterMenuItem);
        menu.Items.Add("打开设置", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("重建词库", null, (_, _) => RebuildRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _playMenuItem.Click += (_, _) => TogglePlayRequested?.Invoke(this, EventArgs.Empty);
        _filterMenuItem.Click += (_, _) => ToggleFilterRequested?.Invoke(this, EventArgs.Empty);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "悬浮置顶单词本",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateState(bool isPlaying, bool showOnlyUnmastered)
    {
        _playMenuItem.Text = isPlaying ? "暂停" : "继续";
        _filterMenuItem.Checked = showOnlyUnmastered;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
