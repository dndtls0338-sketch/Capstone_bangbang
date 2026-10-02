using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cap.Editor
{
    public class CapWarmTownTextureImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.Contains("/Resources/WarmTown/") || !assetPath.EndsWith(".png")) return;
            var t=(TextureImporter)assetImporter;
            t.textureType=TextureImporterType.Default;
            t.alphaIsTransparency=true;
            t.mipmapEnabled=false; t.filterMode=FilterMode.Point;
            t.textureCompression=TextureImporterCompression.Uncompressed;
            t.maxTextureSize=4096; t.npotScale=TextureImporterNPOTScale.None;
        }
    }
    [InitializeOnLoad]
    public static class CapWarmTownSetup
    {
        static double next;
        static CapWarmTownSetup() { EditorApplication.update+=Poll; }
        [MenuItem("CAP/변경한 코드 다시 불러오기")]
        public static void ReloadCode()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(UnityEditor.Compilation.RequestScriptCompilationOptions.CleanBuildCache);
        }
        [MenuItem("CAP/새 마을 대기실 열기")]
        public static void Open()
        {
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene("Assets/CapMultiplayer/Scenes/CapRelayTest.unity");
        }
        [MenuItem("CAP/새 마을 Windows 빌드")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            foreach(var file in Directory.GetFiles("Assets/CapMultiplayer/Resources/WarmTown","*.png"))
                AssetDatabase.ImportAsset(file,ImportAssetOptions.ForceUpdate);
            Directory.CreateDirectory("Builds/CapWarmTown");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{"Assets/CapMultiplayer/Scenes/CapRelayTest.unity"},
                locationPathName="Builds/CapWarmTown/cap.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
            });
            File.WriteAllText("CapSetupBackup/warm-town-build-result.txt",DateTime.UtcNow.ToString("O")+" "+report.summary.result+" errors="+report.summary.totalErrors+" warnings="+report.summary.totalWarnings);
            // Refresh editor assemblies too: player-build compilation does not reload the editor's runtime DLL.
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Town build failed");
        }
        private static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next) return; next=EditorApplication.timeSinceStartup+2;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string path="CapSetupBackup/warm-town-build.request";
            if(!File.Exists(path)) return;
            File.Delete(path);
            try { Build(); } catch(Exception e) { File.WriteAllText("CapSetupBackup/warm-town-build-result.txt",e.ToString()); }
        }
    }
}
