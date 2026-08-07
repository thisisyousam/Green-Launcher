using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using CmlLib.Core.Auth;

namespace GreenLauncher;

public partial class MainWindow : Window
{
    private static readonly HttpClient AvatarHttpClient = new();
    private static readonly IBrush InactiveIconBrush = Brush.Parse("#9CA3AF");

    private readonly LauncherService _launcherService = new();
    private bool _has3D = true;
    private MSession? _session;
    private string _currentPage = "home";
    private (Button Button, Avalonia.Controls.Shapes.Path Icon, TextBlock Label, string Key)[] _navItems = [];

    public MainWindow()
    {
        InitializeComponent();

        AvatarViewer3D.SetBackgroundColor(0xE5, 0xE8, 0xE1);
        AvatarViewer3D.SetBackgroundImage(Rendering.SkinViewerControl.LoadBundledBackground("bg_sunset.png"));
        AvatarViewer3D.InitFailed += (_, _) =>
        {
            _has3D = false;
            if (AvatarViewer3D.Parent is Panel avatarHost) avatarHost.Children.Remove(AvatarViewer3D);
        };

        _navItems =
        [
            (NavHomeButton, NavHomeIcon, NavHomeLabel, "home"),
            (NavSkinsButton, NavSkinsIcon, NavSkinsLabel, "skins"),
            (NavSettingsButton, NavSettingsIcon, NavSettingsLabel, "settings"),
            (NavAccountButton, NavAccountIcon, NavAccountLabel, "account"),
        ];

        HomePage.PlayRequested += OnPlayRequested;
        AccountPage.LogoutRequested += OnLogoutRequested;

        _launcherService.LogMessage += message => Dispatcher.UIThread.Post(() => HomePage.AppendLog(message));
        _launcherService.FileProgressChanged += (_, args) => Dispatcher.UIThread.Post(() =>
        {
            HomePage.SetProgressStatus($"{args.Name} ({args.ProgressedTasks}/{args.TotalTasks})");
        });
        _launcherService.ByteProgressChanged += (_, args) => Dispatcher.UIThread.Post(() =>
        {
            if (args.TotalBytes <= 0) return;
            var percent = (double)args.ProgressedBytes / args.TotalBytes * 100;
            HomePage.SetProgress(percent);
        });

        SettingsPage.SetGameDirectory(System.IO.Path.GetFullPath(_launcherService.Path.BasePath));
        SettingsPage.SetJavaPath(DetectJavaPath());

        UpdateThemeToggle();
        ApplyNavHighlight();
        LoadModList();
    }

    private static string DetectJavaPath()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (string.IsNullOrEmpty(javaHome)) return "시스템 기본값 사용";

        var exeName = OperatingSystem.IsWindows() ? "javaw.exe" : "java";
        return System.IO.Path.Combine(javaHome, "bin", exeName);
    }

    private async void LoadModList()
    {
        try
        {
            var manifest = await _launcherService.GetManifestAsync();
            HomePage.SetVersionBadge($"Fabric · {manifest.mcVersion}");
            HomePage.SetSummary($"모드 {manifest.mods.Count}개 설치됨");
            HomePage.SetModList(manifest.mods);
        }
        catch (Exception ex)
        {
            HomePage.SetSummary("모드 목록을 불러오지 못했습니다");
            HomePage.AppendLog("모드 목록 로드 실패: " + ex.Message);
        }
    }

    private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
    {
        var app = Application.Current!;
        var isDark = app.ActualThemeVariant == ThemeVariant.Dark;
        app.RequestedThemeVariant = isDark ? ThemeVariant.Light : ThemeVariant.Dark;
        UpdateThemeToggle();
        ApplyNavHighlight();
    }

    private void UpdateThemeToggle()
    {
        var isDark = Application.Current!.ActualThemeVariant == ThemeVariant.Dark;
        ToggleThumb.Margin = new Thickness(isDark ? 21 : 3, 0, 0, 0);
    }

    private void OnNavHomeClick(object? sender, RoutedEventArgs e) => NavigateTo("home");
    private void OnNavSkinsClick(object? sender, RoutedEventArgs e) => NavigateTo("skins");
    private void OnNavSettingsClick(object? sender, RoutedEventArgs e) => NavigateTo("settings");
    private void OnNavAccountClick(object? sender, RoutedEventArgs e) => NavigateTo("account");

    private void NavigateTo(string page)
    {
        _currentPage = page;
        HomePage.IsVisible = page == "home";
        SkinsPage.IsVisible = page == "skins";
        SettingsPage.IsVisible = page == "settings";
        AccountPage.IsVisible = page == "account";
        ApplyNavHighlight();
    }

    private void ApplyNavHighlight()
{
    var isDark = Application.Current!.ActualThemeVariant == ThemeVariant.Dark;
    var textPrimary = Brush.Parse(isDark ? "#F0F0F0" : "#1A1B1E");
    var accentTint = Brush.Parse(isDark ? "#2989D22F" : "#2489D22F");
    var accentTextOnTint = Brush.Parse(isDark ? "#A9E35D" : "#4F7A1A");

    foreach (var (button, icon, label, key) in _navItems)
    {
        var active = key == _currentPage;
        button.Background = active ? accentTint : Brushes.Transparent;
        label.Foreground = active ? accentTextOnTint : textPrimary;
        icon.Fill = active ? accentTextOnTint : InactiveIconBrush;
    }
}

    private async void OnLoginClick(object? sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        LoginStatusText.Text = "로그인 중입니다. 브라우저 창에서 Microsoft 계정으로 로그인해주세요...";

        try
        {
            var session = await _launcherService.GetSessionAsync();
            _session = session;

            ShowAccount(session);
            _ = LoadAvatarAsync(session.UUID);

            LoginScreen.IsVisible = false;
            Shell.IsVisible = true;
            NavigateTo("home");
        }
        catch (Exception ex)
        {
            LoginStatusText.Text = "로그인 실패: " + ex.Message;
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void ShowAccount(MSession session)
    {
        NicknameText.Text = session.Username;
        AccountPage.SetProfile(session.Username ?? "", session.UUID ?? "");
    }

    private async Task LoadAvatarAsync(string? uuid)
    {
        if (string.IsNullOrEmpty(uuid)) return;

        try
        {
            var (skinUrl, isSlim) = await _launcherService.GetSkinUrlAsync(uuid);
            if (string.IsNullOrEmpty(skinUrl)) return;

            var bytes = await AvatarHttpClient.GetByteArrayAsync(skinUrl);
            using var stream = new MemoryStream(bytes);
            var skin = new Bitmap(stream);

            if (_has3D)
            {
                AvatarViewer3D.SetSkin(skin, isSlim);
                AvatarViewer3D.IsVisible = true;
            }

            SkinsPage.SetCurrentSkin(skin, isSlim);

            // 베이스 얼굴 레이어: 포맷(64x32/64x64) 관계없이 항상 (8,8)-(16,16)
            var face = new CroppedBitmap(skin, new PixelRect(8, 8, 8, 8));

            // 모자/헬멧 오버레이 레이어: 항상 (40,8)-(48,16), 투명 부분은 그대로 유지됨
            var hat = new CroppedBitmap(skin, new PixelRect(40, 8, 8, 8));

            AvatarImage.Source = face;
            AvatarImage.IsVisible = !_has3D;

            AvatarHatImage.Source = hat;
            AvatarHatImage.IsVisible = !_has3D;

            AvatarPlaceholder.IsVisible = false;
            AccountPage.SetAvatar(face); // 필요하면 hat도 같이 넘기도록 SetAvatar 시그니처 확장 가능
        }
        catch (Exception ex)
        {
            // 실패 시 실루엣 플레이스홀더 유지
            Console.WriteLine("[avatar] LoadAvatarAsync EXCEPTION: " + ex);
        }
    }

    private async void OnPlayRequested()
    {
        HomePage.SetPlayEnabled(false);
        HomePage.SetProgressVisible(true);
        try
        {
            var session = _session!;
            var maxRamMb = SettingsPage.MemoryGb * 1024;

            HomePage.SetProgressStatus("Fabric 설치 중...");
            var fabricVersionName = await _launcherService.InstallFabricAsync();

            HomePage.SetProgressStatus("모드 다운로드 중...");
            var manifest = await _launcherService.GetManifestAsync();
            await _launcherService.DownloadModsAsync(manifest);

            HomePage.SetProgressStatus("게임 실행 중...");
            await _launcherService.LaunchGameAsync(fabricVersionName, session, maxRamMb);

            HomePage.SetProgressStatus("실행 완료");
        }
        catch (Exception ex)
        {
            HomePage.SetProgressStatus("오류 발생");
            HomePage.AppendLog("오류: " + ex.Message);
            HomePage.SetPlayEnabled(true);
        }
    }

    private void OnLogoutRequested()
    {
        _session = null;

        Shell.IsVisible = false;
        LoginScreen.IsVisible = true;
        LoginStatusText.Text = "";

        NicknameText.Text = "";
        AvatarImage.IsVisible = false;
        AvatarHatImage.IsVisible = false;
        if (_has3D) AvatarViewer3D.IsVisible = false;
        AvatarPlaceholder.IsVisible = true;
        AccountPage.ResetAvatar();

        HomePage.SetPlayEnabled(true);
        HomePage.SetProgressVisible(false);
        NavigateTo("home");
    }
}
