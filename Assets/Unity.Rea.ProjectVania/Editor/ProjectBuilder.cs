using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Rea.ProjectVania.EditorTools
{
    /// <summary>
    /// Sets the project up in code and builds it from the command line (U.VAN-T6). <see cref="Ensure"/> creates the
    /// pipeline and the one empty bootstrap scene if they are missing and lists the scene in the build settings (the
    /// game itself is composed by <see cref="Bootstrap"/> at start-up). <see cref="Build"/> is the batch entry point:
    /// <c>Unity -batchmode -quit -projectPath . -executeMethod Unity.Rea.ProjectVania.EditorTools.ProjectBuilder.Build</c>
    /// (see unity.ps1), exiting non-zero if the build fails. Tests run through the same CLI with -runTests.
    /// </summary>
    public static class ProjectBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        /// <summary>The online scene (U.VAN-T31, written by OnlineSceneBuilder), second in the build after the local game.</summary>
        public const string OnlineScenePath = "Assets/Scenes/Online.unity";
        public const string DefaultOutput = "Build/ProjectVania/ProjectVania.exe";

        [MenuItem("MindAttic/ProjectVania/Ensure Project")]
        public static void Ensure()
        {
            RenderPipelineBuilder.Ensure();
            EnsureScene();
        }

        /// <summary>
        /// The empty bootstrap scene, created only if missing, first in the build settings, with the online scene after it
        /// when it exists (U.VAN-T31), so a launch plays the local game.
        /// </summary>
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            var wanted = Scenes();
            var current = EditorBuildSettings.scenes;
            var same = current.Length == wanted.Length;
            for (var i = 0; same && i < wanted.Length; i++)
            {
                same = current[i].path == wanted[i] && current[i].enabled;
            }

            if (!same)
            {
                EditorBuildSettings.scenes = Array.ConvertAll(wanted, path => new EditorBuildSettingsScene(path, true));
            }
        }

        /// <summary>The build's scenes in order: the local game, then the online scene if it has been written.</summary>
        public static string[] Scenes() => File.Exists(OnlineScenePath) ? new[] { ScenePath, OnlineScenePath } : new[] { ScenePath };

        /// <summary>The batch build. The output path comes from <c>-buildOutput</c> or <see cref="DefaultOutput"/>.</summary>
        public static void Build()
        {
            try
            {
                Ensure();
                var output = ArgumentValue("-buildOutput") ?? DefaultOutput;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Scenes(),
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                var summary = report.summary;
                Debug.Log($"[ProjectVania] Build {summary.result}: {summary.totalErrors} error(s), {summary.totalSize} bytes at {output}");
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProjectVania] Build failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        static string ArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
