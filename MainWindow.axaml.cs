using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using CmlLib.Core.Auth;

namespace GreenLauncher;

public partial class MainWindow : Window
{
    private static readonly HttpClient AvatarHttpClient = new();

    private readonly LauncherService _launcherService = new();
    private MSession? _session;

    public MainWindow()
    {
        InitializeComponent();

        _launcherService.LogMessage += message => Dispatcher.UIThread.Post(() => AppendLog(message));
        _launcherService.FileProgressChanged += (_, args) => Dispatcher.UIThread.Post(() =>
        {
            ProgressStatusText.Text = $"{args.Name} ({args.ProgressedTasks}/{args.TotalTasks})";
        });
        _launcherService.ByteProgressChanged += (_, args) => Dispatcher.UIThread.Post(() =>
        {
            if (args.TotalBytes <= 0) return;
            var percent = (double)args.ProgressedBytes / args.TotalBytes * 100;
            Progress.Value = percent;
            ProgressPercentText.Text = $"{(int)percent}%";
        });

        UpdateThemeIcon();
        LoadModList();
    }

    private void AppendLog(string message)
    {
        LogBox.Text += message + Environment.NewLine;
        LogScroll.ScrollToEnd();
    }

    private async void LoadModList()
    {
        try
        {
            var manifest = await _launcherService.GetManifestAsync();
            VersionInfoText.Text = $"Minecraft {manifest.mcVersion} · Fabric";
            ModListItems.ItemsSource = manifest.mods;
        }
        catch (Exception ex)
        {
            VersionInfoText.Text = "모드 목록을 불러오지 못했습니다";
            AppendLog("모드 목록 로드 실패: " + ex.Message);
        }
    }

    private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
    {
        var app = Application.Current!;
        var isDark = app.ActualThemeVariant == ThemeVariant.Dark;
        app.RequestedThemeVariant = isDark ? ThemeVariant.Light : ThemeVariant.Dark;
        UpdateThemeIcon();
    }

    private void UpdateThemeIcon()
    {
        var isDark = Application.Current!.ActualThemeVariant == ThemeVariant.Dark;
        SunIcon.IsVisible = isDark;
        SunRays.IsVisible = isDark;
        MoonIcon.IsVisible = !isDark;
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
            MainScreen.IsVisible = true;
        }
        catch (Exception ex)
        {
            LoginStatusText.Text = "로그인 실패: " + ex.Message;
            AppendLog("로그인 실패: " + ex.Message);
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private async void OnAccountClick(object? sender, RoutedEventArgs e)
    {
        AccountButton.IsEnabled = false;
        var previousNickname = NicknameText.Text;
        NicknameText.Text = "로그인 중...";
        AccountStatusText.Text = "";

        try
        {
            var session = await _launcherService.GetSessionAsync();
            _session = session;
            ShowAccount(session);
            _ = LoadAvatarAsync(session.UUID);
        }
        catch (Exception ex)
        {
            NicknameText.Text = previousNickname;
            AccountStatusText.Text = "재로그인 실패, 다시 클릭해서 재시도";
            AppendLog("재로그인 실패: " + ex.Message);
        }
        finally
        {
            AccountButton.IsEnabled = true;
        }
    }

    private void ShowAccount(MSession session)
    {
        NicknameText.Text = session.Username;
        AccountStatusText.Text = "Microsoft 계정";
    }

    private async Task LoadAvatarAsync(string? uuid)
    {
        if (string.IsNullOrEmpty(uuid)) return;

        try
        {
            var bytes = await AvatarHttpClient.GetByteArrayAsync($"https://crafatar.com/avatars/{uuid}?size=64&overlay");
            using var stream = new MemoryStream(bytes);
            AvatarImage.Source = new Bitmap(stream);
            AvatarImage.IsVisible = true;
            AvatarPlaceholder.IsVisible = false;
        }
        catch
        {
            // 실패 시 실루엣 플레이스홀더 유지
        }
    }

    private async void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        // 성공하면 재활성화하지 않음(재실행 방지) — 오류 시에만 catch 블록에서 재활성화
        PlayButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        try
        {
            var session = _session!;

            ProgressStatusText.Text = "Fabric 설치 중...";
            var fabricVersionName = await _launcherService.InstallFabricAsync();

            ProgressStatusText.Text = "모드 다운로드 중...";
            var manifest = await _launcherService.GetManifestAsync();
            await _launcherService.DownloadModsAsync(manifest);

            ProgressStatusText.Text = "게임 실행 중...";
            await _launcherService.LaunchGameAsync(fabricVersionName, session);

            ProgressStatusText.Text = "실행 완료";
        }
        catch (Exception ex)
        {
            ProgressStatusText.Text = "오류 발생";
            AppendLog("오류: " + ex.Message);
            PlayButton.IsEnabled = true;
        }
    }
}
