using System.Net.Http.Json;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;
using Microsoft.Identity.Client;
using XboxAuthNet.Game.Msal;
using XboxAuthNet.Game.Msal.OAuth;

namespace GreenLauncher;

public class LauncherService
{
    private const string McVersion = "26.2";
    private const string ManifestUrl = "https://raw.githubusercontent.com/thisisyousam/Green-Launcher/main/manifest.json";

    // Azure App Registration의 Application (Client) ID. .env 파일(AZURE_CLIENT_ID)에서 로드.
    private static readonly string AzureClientId = LoadAzureClientId();

    private static string LoadAzureClientId()
    {
        var envPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

                var parts = trimmed.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim() == "AZURE_CLIENT_ID")
                    return parts[1].Trim();
            }
        }

        return Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")
            ?? throw new InvalidOperationException(".env 파일에 AZURE_CLIENT_ID가 설정되어 있지 않습니다. .env.example을 참고하세요.");
    }

    public string MinecraftVersion => McVersion;
    public MinecraftPath Path { get; } = new("./test-minecraft");
    public MinecraftLauncher Launcher { get; }

    public event Action<string>? LogMessage;
    public event EventHandler<CmlLib.Core.Installers.InstallerProgressChangedEventArgs>? FileProgressChanged;
    public event EventHandler<CmlLib.Core.ByteProgress>? ByteProgressChanged;

    private ModManifest? _cachedManifest;
    private IPublicClientApplication? _msalApp;

    public LauncherService()
    {
        Launcher = new MinecraftLauncher(Path);
        Launcher.FileProgressChanged += (sender, args) => FileProgressChanged?.Invoke(sender, args);
        Launcher.ByteProgressChanged += (sender, args) => ByteProgressChanged?.Invoke(sender, args);
    }

    private void Log(string message) => LogMessage?.Invoke(message);

    public async Task<MSession> GetSessionAsync()
    {
        _msalApp ??= await MsalClientHelper.BuildApplicationWithCache(AzureClientId);

        var loginHandler = new JELoginHandlerBuilder()
            .WithOAuthProvider(new MsalCodeFlowProvider(_msalApp))
            .Build();

        var session = await loginHandler.Authenticate();
        Log("MS 로그인 성공!");
        return session;
    }

    public async Task<string> InstallFabricAsync()
    {
        var fabricInstaller = new FabricInstaller(new HttpClient());
        var fabricVersionName = await fabricInstaller.Install(McVersion, Path);
        Log($"Fabric 설치 완료: {fabricVersionName}");
        return fabricVersionName;
    }

    public async Task<ModManifest> GetManifestAsync()
    {
        if (_cachedManifest is not null)
            return _cachedManifest;

        using var httpClient = new HttpClient();
        var manifest = await httpClient.GetFromJsonAsync<ModManifest>(ManifestUrl);
        _cachedManifest = manifest!;
        return _cachedManifest;
    }

    public async Task DownloadModsAsync(ModManifest manifest)
    {
        var modsDir = System.IO.Path.Combine(Path.BasePath, "mods");
        Directory.CreateDirectory(modsDir);

        using var httpClient = new HttpClient();

        foreach (var mod in manifest.mods)
        {
            var destPath = System.IO.Path.Combine(modsDir, mod.name);
            Log($"다운로드 중: {mod.name}");

            var bytes = await httpClient.GetByteArrayAsync(mod.downloadUrl);
            await File.WriteAllBytesAsync(destPath, bytes);

            Log($"완료: {mod.name}");
        }
    }

    public async Task LaunchGameAsync(string fabricVersionName, MSession session)
    {
        var launchOption = new MLaunchOption
        {
            Session = session,
            MaximumRamMb = 4096
        };

        await Launcher.InstallAsync(fabricVersionName);
        var process = await Launcher.BuildProcessAsync(fabricVersionName, launchOption);
        process.Start();
        Log("게임 실행됨!");
    }
}

public class ModManifest
{
    public string mcVersion { get; set; } = "";
    public List<ModEntry> mods { get; set; } = new();
}

public class ModEntry
{
    public string name { get; set; } = "";
    public string downloadUrl { get; set; } = "";
    public string version { get; set; } = "";
}
