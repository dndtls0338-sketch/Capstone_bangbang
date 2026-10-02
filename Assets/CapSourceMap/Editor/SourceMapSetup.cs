using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class SourceMapTextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.Contains("/Resources/SourceMap/")||!assetPath.EndsWith(".png"))return;
        var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;
        t.sRGBTexture=true;t.alphaIsTransparency=true;t.mipmapEnabled=false;
        t.filterMode=FilterMode.Point;t.textureCompression=TextureImporterCompression.Uncompressed;
        t.maxTextureSize=4096;t.npotScale=TextureImporterNPOTScale.None;
    }
}
public static class SourceMapSetup
{
    [MenuItem("CAP/원본 배율 마을/분리 에셋과 편집용 프리팹 생성")]
    public static void ExportAssembly()
    {
        string folder="Assets/CapSourceMap/Assembled";
        Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        string unique=AssetDatabase.GenerateUniqueAssetPath(folder+"/SourceSprites.asset");
        var library=ScriptableObject.CreateInstance<SourceMapSpriteLibrary>();AssetDatabase.CreateAsset(library,unique);
        var holder=new GameObject("Source map export");
        var town=holder.AddComponent<Cap.Multiplayer.CapWarmTown>();
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        typeof(Cap.Multiplayer.CapWarmTown).GetMethod("Awake",flags).Invoke(town,null);
        var root=(GameObject)typeof(Cap.Multiplayer.CapWarmTown).GetField("root",flags).GetValue(town);
        root.SetActive(true);root.name="SourceTown";
        var material=town.ArtMaterial;AssetDatabase.AddObjectToAsset(material,library);
        var list=new System.Collections.Generic.List<Sprite>();
        foreach(var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            if(renderer.sprite!=null&&!list.Contains(renderer.sprite))
            {list.Add(renderer.sprite);AssetDatabase.AddObjectToAsset(renderer.sprite,library);}
        library.sprites=list.ToArray();EditorUtility.SetDirty(library);
        foreach(Transform child in root.transform)child.position-=new Vector3(Cap.Multiplayer.CapWarmTown.OffsetX,0,0);
        AssetDatabase.SaveAssets();
        string prefabPath=AssetDatabase.GenerateUniqueAssetPath(folder+"/SourceTown.prefab");
        PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        typeof(Cap.Multiplayer.CapWarmTown).GetField("root",flags).SetValue(town,null);
        ((System.Collections.Generic.Dictionary<string,Sprite>)typeof(Cap.Multiplayer.CapWarmTown).GetField("sprites",flags).GetValue(town)).Clear();
        ((System.Collections.Generic.List<Sprite>)typeof(Cap.Multiplayer.CapWarmTown).GetField("groundSprites",flags).GetValue(town)).Clear();
        typeof(Cap.Multiplayer.CapWarmTown).GetField("white",flags).SetValue(town,null);
        typeof(Cap.Multiplayer.CapWarmTown).GetProperty("ArtMaterial").SetValue(town,null);
        Object.DestroyImmediate(root);Object.DestroyImmediate(holder);
        Debug.Log("[SOURCE-EXPORT] "+prefabPath+"; independent sprite sub-assets="+list.Count);
    }
    [MenuItem("CAP/원본 배율 마을/Windows 빌드")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        ExportAssembly();
        Directory.CreateDirectory("Builds/SourceMap");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=new[]{"Assets/CapMultiplayer/Scenes/CapRelayTest.unity"},
            locationPathName="Builds/SourceMap/cap.exe",target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.Development});
        File.WriteAllText("source-map-build-result.txt",report.summary.result+" errors="+report.summary.totalErrors);
        if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Source map build failed");
    }
}
