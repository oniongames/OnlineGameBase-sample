using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OogbTwoClientSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/OOGBTwoClientTest.unity";

    [MenuItem("OOGB/Rebuild Two Client Test Scene")]
    public static void RebuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
        cameraObject.transform.position = new Vector3(0, 0, -10);

        var harnessObject = new GameObject("OOGB Two Client Harness");
        harnessObject.AddComponent<OogbTwoClientHarness>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
    }

    public static void BuildWindows64()
    {
        RebuildScene();

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/OOGBTwoClientTest/OOGBTwoClientTest.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
            throw new System.InvalidOperationException("OOGB two-client test build failed: " + report.summary.result);

        Debug.Log("OOGB two-client test build succeeded: " + report.summary.outputPath);
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        for (var i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == scenePath)
            {
                scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
