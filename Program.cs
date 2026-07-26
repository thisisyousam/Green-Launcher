using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;

var path = new MinecraftPath("./test-minecraft");
var launcher = new MinecraftLauncher(path);

launcher.FileProgressChanged += (sender, args) =>
{
    Console.WriteLine($"[{args.EventType}] {args.Name} - {args.ProgressedTasks}/{args.TotalTasks}");
};
launcher.ByteProgressChanged += (sender, args) =>
{
    Console.WriteLine($"{args.ProgressedBytes} / {args.TotalBytes} bytes");
};

// ===== 로그인 시도 (실패하면 오프라인으로 대체) =====
MSession session;
try
{
    var loginHandler = JELoginHandlerBuilder.BuildDefault();
    session = await loginHandler.Authenticate();
    Console.WriteLine("MS 로그인 성공!");
}
catch (Exception ex)
{
    Console.WriteLine("로그인 실패, 오프라인 세션으로 진행: " + ex.Message);
    session = MSession.CreateOfflineSession("yousam");
}

// ===== Fabric 설치 =====
string mcVersion = "26.2"; // 실제 타겟 버전으로 변경
var fabricInstaller = new FabricInstaller(new HttpClient());
var fabricVersionName = await fabricInstaller.Install(mcVersion, path);
Console.WriteLine($"Fabric 설치 완료: {fabricVersionName}");

// ===== 모드 파일 복사 =====
var modsDir = Path.Combine(path.BasePath, "mods");
Directory.CreateDirectory(modsDir);

var sourceModsDir = "./mods-to-install"; // 여기에 배포할 jar들 미리 넣어두기
Directory.CreateDirectory(sourceModsDir);

var modJarFiles = Directory.GetFiles(sourceModsDir, "*.jar");

if (modJarFiles.Length == 0)
{
    Console.WriteLine($"경고: {sourceModsDir} 폴더에 jar 파일이 없습니다.");
}

foreach (var modPath in modJarFiles)
{
    var fileName = Path.GetFileName(modPath);
    File.Copy(modPath, Path.Combine(modsDir, fileName), true);
    Console.WriteLine($"모드 복사됨: {fileName}");
}

// ===== 설치 + 실행 =====
var launchOption = new MLaunchOption
{
    Session = session,
    MaximumRamMb = 4096
};

await launcher.InstallAsync(fabricVersionName);
var process = await launcher.BuildProcessAsync(fabricVersionName, launchOption);
process.Start();