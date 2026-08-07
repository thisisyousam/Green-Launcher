using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace GreenLauncher.Views;

public partial class SettingsView : UserControl
{
    // Java 경로 / 게임 디렉토리는 읽기 전용 표시입니다 — 실제로 조정 가능한 설정은 메모리 할당뿐입니다.
    public int MemoryGb { get; private set; } = 6;

    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnMemoryChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        MemoryGb = (int)e.NewValue;
        MemoryLabelText.Text = $"{MemoryGb}GB";
    }

    public void SetJavaPath(string path) => JavaPathText.Text = path;

    public void SetGameDirectory(string path) => GameDirText.Text = path;
}
