using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cap.Editor
{
    // Editor-only shortcuts. Opening the project never generates or replaces assets.
    public static class CapProjectTools
    {
        private const string ScenePath = "Assets/Scenes/CapRelayTest.unity";

        [MenuItem("CAP/랜덤 증거 설정")]
        public static void SelectEvidence()
        {
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<Cap.Multiplayer.CapEvidenceDefinition>("Assets/Resources/Evidence/RandomEvidence.asset");
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("CAP/경찰서 NPC 설정")]
        public static void SelectPoliceNpc()
        {
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<Cap.Multiplayer.CapPoliceNpcDefinition>("Assets/Resources/Dialogue/PoliceNpc.asset");
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("CAP/게임 씬 열기")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("CAP/Windows 빌드")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("게임 씬이 없습니다: " + ScenePath);
            Directory.CreateDirectory("Builds/Game");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Game/cap.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Debug.Log($"[CAP-BUILD] {report.summary.result}; errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}");
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("빌드에 실패했습니다. Console을 확인하세요.");
        }
    }
}
