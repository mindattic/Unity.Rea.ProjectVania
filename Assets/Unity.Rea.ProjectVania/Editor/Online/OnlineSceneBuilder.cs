using System.Collections.Generic;
using System.IO;
using Unity.Multiplayer;        // NetPrefabList, NetKey
using Unity.Multiplayer.Editor; // NetFrontEndSetup, NetSceneSetupOptions
using Unity.Multiplayer.UI;     // NetFrontEnd, NetFrontEndSettings
using Unity.Netcode;
using Unity.Rea.ProjectVania.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Rea.ProjectVania.EditorTools.Online
{
    /// <summary>
    /// Writes the online scene (U.VAN-T31) in code, beside the local game's empty Main.unity: a black camera for the menus,
    /// the kit's NetworkManager with Unity Transport, NetBootstrap and drop-in NetFrontEnd (through the kit's own Add Front
    /// End), the front end's settings (its in-game Esc menu off: Esc is the game's own menu), the
    /// <see cref="VaniaNetPlayer"/> Player Prefab and the keyed <see cref="VaniaWorldSync"/> in the scene's prefab list,
    /// and <see cref="VaniaOnline"/>. The scene goes second in the build, so a launch still plays the local game first
    /// (<c>-online</c> opens this one). Everything is rewritten on every run, so what is committed is what this writes.
    ///
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod Unity.Rea.ProjectVania.EditorTools.Online.OnlineSceneBuilder.BuildAll
    /// </summary>
    public static class OnlineSceneBuilder
    {
        public const string ScenePath = ProjectBuilder.OnlineScenePath;
        public const string Folder = "Assets/Unity.Rea.ProjectVania/Online/Prefabs";
        public const string PlayerPrefabPath = Folder + "/VaniaNetPlayer.prefab";
        public const string WorldSyncPrefabPath = Folder + "/VaniaWorldSync.prefab";
        public const string PrefabListPath = Folder + "/VaniaNetPrefabs.asset";
        public const string FrontEndSettingsPath = Folder + "/VaniaFrontEnd.asset";
        public const string MpHostPath = "mp-host.json";

        /// <summary>mp-host's defaults: the server and the smoke test's bots run the online scene.</summary>
        public const string MpHostJson = "{\n  \"name\": \"unity-projectvania\",\n  \"scene\": \"" + ScenePath + "\",\n  \"bots\": \"A,B\"\n}\n";

        [MenuItem("MindAttic/ProjectVania/Rebuild Online Scene")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            var player = BuildPlayerPrefab();
            var sync = BuildWorldSyncPrefab();
            var list = BuildPrefabList(player, sync);
            var settings = BuildFrontEndSettings();
            WriteScene(player, sync, list, settings);
            ProjectBuilder.EnsureScene();
            File.WriteAllText(MpHostPath, MpHostJson);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectVania] Online scene written to " + ScenePath);
        }

        static GameObject BuildPlayerPrefab()
        {
            var go = new GameObject("VaniaNetPlayer");
            go.AddComponent<NetworkObject>();
            go.AddComponent<VaniaNetPlayer>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PlayerPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject BuildWorldSyncPrefab()
        {
            var go = new GameObject("VaniaWorldSync");
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetKey>();
            go.AddComponent<VaniaWorldSync>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, WorldSyncPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static NetPrefabList BuildPrefabList(GameObject player, GameObject sync)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetPrefabList>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetPrefabList>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }

            var listed = new HashSet<GameObject>(list.Prefabs);
            if (!listed.Contains(player)) list.Add(player);
            if (!listed.Contains(sync)) list.Add(sync);
            EditorUtility.SetDirty(list);
            return list;
        }

        static NetFrontEndSettings BuildFrontEndSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<NetFrontEndSettings>(FrontEndSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<NetFrontEndSettings>();
                AssetDatabase.CreateAsset(settings, FrontEndSettingsPath);
            }

            settings.title = "ProjectVania";
            // Esc (and pad Start) is the game's own main menu, as in the original; leaving is that menu's Quit.
            settings.pauseMenu = false;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        static void WriteScene(GameObject player, GameObject sync, NetPrefabList list, NetFrontEndSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The menus' backdrop until the game is composed (its own cameras then draw over it).
            var cameraObject = new GameObject("MenuCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.depth = -2;

            var setup = NetFrontEndSetup.Run(scene, new NetSceneSetupOptions { PrefabList = list, RegisterProjectPrefabs = false });
            if (!setup.Succeeded)
            {
                throw new System.InvalidOperationException("[ProjectVania] The kit's Add Front End failed: " + setup.Message);
            }

            setup.Scene.Manager.NetworkConfig.PlayerPrefab = player;
            EditorUtility.SetDirty(setup.Scene.Manager);

            var frontEnd = new SerializedObject(setup.FrontEnd);
            frontEnd.FindProperty("settings").objectReferenceValue = settings;
            frontEnd.ApplyModifiedPropertiesWithoutUndo();

            var online = new GameObject("VaniaOnline").AddComponent<VaniaOnline>();
            online.WorldSyncPrefab = sync;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
