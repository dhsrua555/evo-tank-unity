using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// 명령줄 빌드: Unity -batchmode -quit -projectPath <evo-tank-unity> -buildTarget Linux64 -executeMethod EvoBuild.Linux
//              Unity -batchmode -quit -projectPath <evo-tank-unity> -buildTarget Win64 -executeMethod EvoBuild.Win
// 장면은 비어 있고 EvoTank가 시작할 때 스스로 만들어진다(RuntimeInitializeOnLoadMethod)
public static class EvoBuild
{
    const string Scene = "Assets/Main.unity";
    public const string Version = "0.1.0";

    static void Build(BuildTarget target, string path)
    {
        if (!System.IO.File.Exists(Scene))
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene), Scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
        PlayerSettings.companyName = "MyApps";
        PlayerSettings.productName = "EvoTank";
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Scene }, locationPathName = path, target = target, targetGroup = BuildPipeline.GetBuildTargetGroup(target) });
        Debug.Log($"[Build] {report.summary.result}");
        if (report.summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
    }

    [MenuItem("EvoTank/Build Linux")] public static void Linux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/EvoTank.x86_64");
    [MenuItem("EvoTank/Build Windows")] public static void Win() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/EvoTank.exe");
}
