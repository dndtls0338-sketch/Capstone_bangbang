using System;
using System.IO;
using System.Linq;
using Cap.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cap.Editor
{
    [InitializeOnLoad]
    public static class CapMultiplayerSetup
    {
        private const string Root = "Assets/CapMultiplayer";
        private const string ScenePath = Root + "/Scenes/CapRelayTest.unity";
        private static double nextPoll;

        static CapMultiplayerSetup()
        {
            EditorApplication.delayCall += AutoCreate;
            EditorApplication.update += PollBuildRequest;
        }

        private static void AutoCreate()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorApplication.delayCall += AutoCreate; return; }
            if (!File.Exists(ScenePath)) CreateTestScene();
        }

        [MenuItem("CAP/멀티 테스트 씬 열기")]
        public static void OpenTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateTestScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        private static void CreateTestScene()
        {
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Art");
            string imagePath = Root + "/Art/TestSquare.png";
            if (!File.Exists(imagePath))
            {
                var texture = new Texture2D(16, 16);
                texture.SetPixels(Enumerable.Repeat(Color.white, 256).ToArray());
                texture.Apply();
                File.WriteAllBytes(imagePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(imagePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);

            // Build in a separate scene so the user's current scene is never overwritten.
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.transform.position = new Vector3(-3.4f, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.065f,.09f,.14f);
            cameraGo.AddComponent<AudioListener>();

            var ground = new GameObject("Test Floor");
            var groundSprite = ground.AddComponent<SpriteRenderer>();
            groundSprite.sprite = sprite;
            groundSprite.color = new Color(.14f,.22f,.26f);
            groundSprite.sortingOrder = -10;
            ground.transform.position = new Vector3(0,-.7f,0);
            ground.transform.localScale = new Vector3(15, 6.8f, 1);
            for (int x = -7; x <= 7; x++)
            {
                var line = new GameObject("Grid " + x);
                var renderer = line.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color(.18f,.28f,.32f);
                renderer.sortingOrder = -9;
                line.transform.position = new Vector3(x,-.7f,0);
                line.transform.localScale = new Vector3(.025f,6.8f,1);
            }

            var player = new GameObject("CapPlayer");
            var sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 10;
            player.transform.localScale = new Vector3(.6f,.8f,1);
            player.AddComponent<NetworkObject>();
            var nt = player.AddComponent<NetworkTransform>();
            nt.SyncPositionZ = false;
            nt.SyncRotAngleX = nt.SyncRotAngleY = nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            player.AddComponent<CapNetworkPlayer>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(player, Root + "/Prefabs/CapPlayer.prefab");
            UnityEngine.Object.DestroyImmediate(player);

            var networkGo = new GameObject("NetworkManager");
            var transport = networkGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777);
            var manager = networkGo.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = prefab;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.TickRate = 30;
            manager.NetworkConfig.EnableSceneManagement = true;
            networkGo.AddComponent<CapRelaySession>();
            networkGo.AddComponent<CapTestBootstrap>();
            new GameObject("Connection UI").AddComponent<CapLobbyUI>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(scene, true);
            var existing = EditorBuildSettings.scenes;
            if (!existing.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = existing.Concat(new[] { new EditorBuildSettingsScene(ScenePath,true) }).ToArray();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("CapSetupBackup");
            File.WriteAllText("CapSetupBackup/setup-result.txt", "OK: generated " + ScenePath + " at " + DateTime.UtcNow.ToString("O"));
            Debug.Log("[CAP] Setup complete. Open CAP > 멀티 테스트 씬 열기, then Play.");
        }

        [MenuItem("CAP/Windows 테스트 빌드 만들기")]
        public static void BuildTest()
        {
            Directory.CreateDirectory("Builds/CapRelay");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/CapRelay/cap.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Directory.CreateDirectory("CapSetupBackup");
            File.WriteAllText("CapSetupBackup/build-result.txt", report.summary.result +
                " errors=" + report.summary.totalErrors + " warnings=" + report.summary.totalWarnings);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("CAP build failed; see Console.");
        }

        private static void PollBuildRequest()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 2;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string request = "CapSetupBackup/build-test.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { BuildTest(); }
            catch (Exception e) { File.WriteAllText("CapSetupBackup/build-result.txt", "FAILED: " + e); }
        }
    }
}
