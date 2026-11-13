using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// 명령줄 빌드: Unity -batchmode -quit -projectPath <evo-tank-unity> -buildTarget Linux64 -executeMethod EvoBuild.Linux
//              Unity -batchmode -quit -projectPath <evo-tank-unity> -buildTarget Win64 -executeMethod EvoBuild.Win
//              Unity -batchmode -quit -projectPath <evo-tank-unity> -buildTarget Android -executeMethod EvoBuild.Android
// 장면은 비어 있고 EvoTank가 시작할 때 스스로 만들어진다(RuntimeInitializeOnLoadMethod)
public static class EvoBuild
{
    const string Scene = "Assets/Main.unity";
    // 판: 올릴 때 둘 다 바꾼다(안드로이드 번호는 늘기만 해야 덮어 설치된다). README 내려받기 링크의 판 번호도 함께
    public const string Version = "0.3.3";
    const int VersionCode = 17;

    static void Build(BuildTarget target, string path)
    {
        if (!System.IO.File.Exists(Scene))
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene), Scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
        PlayerSettings.companyName = "MyApps";
        PlayerSettings.productName = "EvoTank";
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { Icon("Assets/Icons/AppIcon.png") }, IconKind.Any);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Scene }, locationPathName = path, target = target, targetGroup = BuildPipeline.GetBuildTargetGroup(target) });
        Debug.Log($"[Build] {report.summary.result}");
        if (report.summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
    }

    // 아이콘 그림은 압축·밉맵 없이 들여와야 작게 줄여도 깨끗하다
    static Texture2D Icon(string path)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti.textureCompression != TextureImporterCompression.Uncompressed || ti.mipmapEnabled)
        {
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void Pc(BuildTarget target, string path, bool mono = false)
    {
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        // 리눅스는 IL2CPP(C#을 C++로 옮겨 컴파일): 시뮬 계산이 Mono JIT보다 빠르다. 윈도우 IL2CPP는 윈도우에서만 빌드돼 Mono로 둔다(모듈이 없으면 리눅스도 Mono)
        bool il2cpp = !mono && target == BuildTarget.StandaloneLinux64 && System.IO.Directory.Exists(BuildPipeline.GetPlaybackEngineDirectory(target, BuildOptions.None) + "/Variations/linux64_player_nondevelopment_il2cpp");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, il2cpp ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
        PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Standalone, Il2CppCompilerConfiguration.Master);   // 링크 때 최적화(LTO): 파일 사이에도 인라인
        Build(target, path);
    }

    [MenuItem("EvoTank/Build Linux")] public static void Linux() => Pc(BuildTarget.StandaloneLinux64, "Builds/Linux/EvoTank.x86_64");
    [MenuItem("EvoTank/Build Linux (Mono, 속도 비교용)")] public static void LinuxMono() => Pc(BuildTarget.StandaloneLinux64, "Builds/LinuxMono/EvoTank.x86_64", true);
    [MenuItem("EvoTank/Build Windows")] public static void Win() => Pc(BuildTarget.StandaloneWindows64, "Builds/Windows/EvoTank.exe");

    // 휴대폰용 APK: 64비트 ARM(요즘 휴대폰 전부). 안드로이드에서 Mono는 32비트만 돼서 IL2CPP로 만든다. 가로 화면만
    [MenuItem("EvoTank/Build Android")]
    public static void Android()
    {
        var t = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(t, "com.myapps.evotank");
        PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.bundleVersionCode = VersionCode;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.Android.predictiveBackSupport = false;   // 뒤로 버튼을 Esc로 받는다(창 닫기·고르기 풀기)
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;   // GameActivity(기본)는 실제 기기에서 터치·UI가 전혀 안 먹는 Unity 버그가 있다(IN-87521, 11-07 태블릿에서 겪음)
        EditorUserBuildSettings.buildAppBundle = false;
        Build(BuildTarget.Android, "Builds/Android/EvoTank.apk");
    }
}
