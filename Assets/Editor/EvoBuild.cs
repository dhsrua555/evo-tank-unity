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
    // 판: 올릴 때 둘 다 바꾼다(안드로이드 번호는 늘기만 해야 덮어 설치된다)
    public const string Version = "0.1.0";
    const int VersionCode = 1;

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

    static void Pc(BuildTarget target, string path)
    {
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        Build(target, path);
    }

    [MenuItem("EvoTank/Build Linux")] public static void Linux() => Pc(BuildTarget.StandaloneLinux64, "Builds/Linux/EvoTank.x86_64");
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
        EditorUserBuildSettings.buildAppBundle = false;
        Build(BuildTarget.Android, "Builds/Android/EvoTank.apk");
    }
}
