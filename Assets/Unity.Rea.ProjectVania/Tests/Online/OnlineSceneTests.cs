using System.Linq;
using NUnit.Framework;
using Unity.Multiplayer;
using Unity.Multiplayer.UI;
using Unity.Netcode;
using Unity.Rea.ProjectVania.EditorTools;
using Unity.Rea.ProjectVania.EditorTools.Online;
using Unity.Rea.ProjectVania.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Rea.ProjectVania.Tests.Online
{
    /// <summary>
    /// U.VAN-T31: the online scene OnlineSceneBuilder writes holds the kit's networking and front end (its Esc menu off,
    /// Esc being the game's own menu), the game's Player Prefab and keyed world sync, and comes second in the build after
    /// the local game.
    /// </summary>
    public class OnlineSceneTests
    {
        [Test]
        public void TheOnlineSceneIsTheKitsFrontEndAroundTheGame()
        {
            var scene = EditorSceneManager.OpenScene(OnlineSceneBuilder.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var manager = roots.Select(r => r.GetComponent<NetworkManager>()).Single(m => m != null);
            Assert.That(manager.GetComponent<NetBootstrap>(), Is.Not.Null);
            var frontEnd = manager.GetComponent<NetFrontEnd>();
            Assert.That(frontEnd, Is.Not.Null);
            Assert.That(frontEnd.Settings, Is.Not.Null);
            Assert.That(frontEnd.Settings.pauseMenu, Is.False, "Esc is the game's own main menu");
            Assert.That(manager.NetworkConfig.PlayerPrefab.GetComponent<VaniaNetPlayer>(), Is.Not.Null);

            var online = roots.Select(r => r.GetComponent<VaniaOnline>()).Single(o => o != null);
            Assert.That(online.WorldSyncPrefab.GetComponent<VaniaWorldSync>(), Is.Not.Null);
            Assert.That(online.WorldSyncPrefab.GetComponent<NetKey>(), Is.Not.Null);

            var list = AssetDatabase.LoadAssetAtPath<NetPrefabList>(OnlineSceneBuilder.PrefabListPath);
            Assert.That(list.Prefabs, Has.Member(manager.NetworkConfig.PlayerPrefab));
            Assert.That(list.Prefabs, Has.Member(online.WorldSyncPrefab));
        }

        [Test]
        public void TheLocalGameStaysFirstInTheBuild()
        {
            var scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray();
            Assert.That(scenes, Is.EqualTo(new[] { ProjectBuilder.ScenePath, ProjectBuilder.OnlineScenePath }));
            Assert.That(Bootstrap.OnlineScene, Is.EqualTo(System.IO.Path.GetFileNameWithoutExtension(ProjectBuilder.OnlineScenePath)));
        }
    }
}
