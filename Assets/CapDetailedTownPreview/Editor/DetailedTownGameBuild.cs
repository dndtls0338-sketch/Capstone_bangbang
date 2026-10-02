using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Cap.DetailedPreview {
 public static class DetailedTownGameBuild {
  public static void Build(){
   const string root="Assets/CapDetailedTownPreview";
   Directory.CreateDirectory(root+"/Resources/DetailedTown");
   var source=PrefabUtility.LoadPrefabContents(root+"/Assembled/DetailedTown.prefab");
   PrefabUtility.SaveAsPrefabAsset(source,root+"/Resources/DetailedTown/DetailedTown.prefab");
   PrefabUtility.UnloadPrefabContents(source);
   AssetDatabase.SaveAssets();AssetDatabase.Refresh();
   Directory.CreateDirectory("Builds/DetailedTown");
   var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
    scenes=new[]{"Assets/CapMultiplayer/Scenes/CapRelayTest.unity"},
    locationPathName="Builds/DetailedTown/cap.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
   File.WriteAllText("detailed-town-game-build-result.txt",result.summary.result+" errors="+result.summary.totalErrors);
   if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Detailed town game build failed");
  }
 }
}

