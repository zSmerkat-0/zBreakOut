using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZBreakOut.Editor
{
    public static class ProjectBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("zBreakOut/Configure Project")]
        public static void ConfigureProject()
        {
            string scenesDirectory = Path.Combine(Application.dataPath, "Scenes");
            Directory.CreateDirectory(scenesDirectory);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("zBreakOut");
            root.AddComponent<BreakoutGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            PlayerSettings.companyName = "zSmerkat";
            PlayerSettings.productName = "zBreakOut";
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 540;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.bundleVersion = "1.0.0";

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("zBreakOut scene and player settings configured.");
        }

        [MenuItem("zBreakOut/Build Windows")]
        public static void BuildWindows()
        {
            ConfigureProject();
            string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "build", "zBreakOut.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            }

            Debug.Log("zBreakOut Windows build completed: " + outputPath);
        }
    }
}
