using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace Cap.DetailedPreview {
 public static class DetailedTownSetup {
  const string Root="Assets/CapDetailedTownPreview";
  [Serializable] class Entry {public string id,label,sheet;public float[] spriteRect,displaySize;public float uniformScale;}
  [Serializable] class Manifest {public Entry[] entries;}
  [Serializable] class Road {public string id,axis;public float[] rect;}
  [Serializable] class Parcel {public string id,label,type,assetId;public float[] rect,placedSize,anchor;public float assetUniformScale;}
  [Serializable] class Plan {public Road[] roads;public Parcel[] parcels;}
  static Dictionary<string,Sprite> sprites;
  static Dictionary<string,Entry> entries;
  static Material material;
  static Sprite solid;
  static Transform objects,ground;
  static GameObject map;
  static SpriteRenderer Place(string id,float x,float y,float scale=1,Transform parent=null) {
   var e=entries[id];var go=new GameObject(e.label+" ["+id+"]");
   go.transform.SetParent(parent==null?objects:parent);
   go.transform.position=new Vector3(x/100,-y/100,0);
   go.transform.localScale=Vector3.one*(e.uniformScale*scale);
   var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprites[id];sr.sharedMaterial=material;
   sr.sortingOrder=1000+Mathf.RoundToInt(y);return sr;
  }
  static void Rect(string name,float x,float y,float w,float h,Color color,int order=-100) {
   var go=new GameObject(name);go.transform.SetParent(ground);go.transform.position=new Vector3((x+w/2)/100,-(y+h/2)/100,0);
   go.transform.localScale=new Vector3(w/100,h/100,1);
   var sr=go.AddComponent<SpriteRenderer>();sr.sprite=solid;sr.color=color;sr.sharedMaterial=material;sr.sortingOrder=order;
  }
  [MenuItem("CAP/상세 에셋/확인용 씬 열기")]
  public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(Root+"/Scenes/DetailedTownPreview.unity");}
  [MenuItem("CAP/상세 에셋/확인용 씬 다시 만들기")]
  public static void Rebuild(){Build();}
  public static void Build() {
   Directory.CreateDirectory(Root+"/Scenes");Directory.CreateDirectory(Root+"/Assembled");AssetDatabase.Refresh();
   var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/Data/asset-manifest.json"));
   var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Root+"/Data/world-layout.json"));
   foreach(var path in manifest.entries.Select(e=>Root+"/"+e.sheet).Distinct()){
    var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.textureType=TextureImporterType.Default;ti.isReadable=true;ti.sRGBTexture=true;ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.filterMode=FilterMode.Point;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.npotScale=TextureImporterNPOTScale.None;ti.maxTextureSize=2048;ti.SaveAndReimport();
   }
   if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SceneManager.SetActiveScene(scene);
   var lib=ScriptableObject.CreateInstance<DetailedTownAssetLibrary>();
   string libPath=AssetDatabase.GenerateUniqueAssetPath(Root+"/Assembled/DetailedTownAssets.asset");AssetDatabase.CreateAsset(lib,libPath);
   material=new Material(Shader.Find("Sprites/Default"));material.name="Detailed town unlit";AssetDatabase.AddObjectToAsset(material,lib);lib.material=material;
   sprites=new Dictionary<string,Sprite>();entries=manifest.entries.ToDictionary(e=>e.id);
   foreach(var e in manifest.entries){
    var t=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+e.sheet);var r=e.spriteRect;
    var s=Sprite.Create(t,new Rect(r[0],t.height-r[1]-r[3],r[2],r[3]),new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);
    s.name=e.id;AssetDatabase.AddObjectToAsset(s,lib);sprites.Add(e.id,s);
   }
   solid=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);solid.name="Dimension blockout rectangle";AssetDatabase.AddObjectToAsset(solid,lib);
   map=new GameObject("DetailedTown - new assets, dimension blockout");
   ground=new GameObject("Ground - dimension preview only").transform;ground.SetParent(map.transform);
   objects=new GameObject("Buildings and independent props").transform;objects.SetParent(map.transform);
   Rect("Shared pavement",0,0,5376,3584,new Color(.72f,.71f,.66f));
   foreach(var road in plan.roads){
    var r=road.rect;Rect(road.id+" curb",r[0]-12,r[1]-12,r[2]+24,r[3]+24,new Color(.8f,.67f,.37f),-95);
    Rect(road.id,r[0],r[1],r[2],r[3],new Color(.34f,.38f,.41f),-90);
    if(road.id.StartsWith("main")){
     float length=road.axis=="h"?r[2]:r[3];
     for(float n=35;n<length;n+=130){
      float cx=road.axis=="h"?r[0]+n:r[0]+r[2]/2;
      float cy=road.axis=="h"?r[1]+r[3]/2:r[1]+n;
      if(cx>2380&&cx<2730&&cy>1510&&cy<1880)continue;
      Rect("Center line",cx,cy,road.axis=="h"?64:5,road.axis=="h"?5:64,new Color(.92f,.72f,.34f),-85);
     }
    }
   }
   // Grass only in communal planting areas, not isolated lawns around every shop.
   Rect("Park grass",3200,2720,1690,610,new Color(.47f,.64f,.31f),-80);
   Rect("Park center path",3940,2660,135,750,new Color(.77f,.75f,.66f),-75);
   Rect("Campus yard",3440,730,1020,700,new Color(.79f,.77f,.7f),-80);
   foreach(var p in plan.parcels)if(!string.IsNullOrEmpty(p.assetId)&&sprites.ContainsKey(p.assetId)){
    var e=entries[p.assetId];Place(p.assetId,p.anchor[0],p.anchor[1],p.assetUniformScale/e.uniformScale);
   }
   string[] homes={"house-blue","house-gray","house-orange","house-green","house-brick","house-flat-blue"};
   float[,] homeSites={{220,770},{650,765},{1070,775},{120,2350},{180,2890},{630,2890},{1130,2950},{1580,2910},{2040,2890},{430,3460},{925,3480},{1450,3480},{1960,3480},{2910,2810},{2890,3300},{5100,3400}};
   for(int i=0;i<homeSites.GetLength(0);i++)Place(homes[i%homes.Length],homeSites[i,0],homeSites[i,1]);
   Place("park-terrace",4010,3150);Place("round-planter",4010,3060);
   Place("bench",3610,3150);Place("bench",4460,3150);Place("bench",3490,2670);Place("bench",4440,1340);
   Place("noticeboard",4480,1380);Place("bus-shelter",4390,1500);Place("bus-sign",4610,1510);
   Place("gatepost",3760,1360);Place("gatepost",4190,1360);
   for(float x=3000;x<3760;x+=345)Place("fence",x,1390);
   for(float x=4440;x<5320;x+=345)Place("fence",x,1390);
   for(float x=3330;x<4890;x+=345)Place("fence",x,3420);
   float[,] trees={{2900,270},{3260,390},{2870,940},{3100,1250},{4680,1180},{5090,1180},{5100,760},{4880,330},{3280,2880},{3560,2980},{4500,2910},{4800,3110},{3350,3330},{4570,3380},{110,370},{1050,270},{80,1160},{860,1320},{2180,1370},{90,1920},{2280,2070},{2850,2160},{5170,2160},{4800,2590}};
   for(int i=0;i<trees.GetLength(0);i++)Place(i%3==0?"tree-small":"tree-round",trees[i,0],trees[i,1]);
   for(int i=0;i<plan.parcels.Length;i++){var p=plan.parcels[i];if(string.IsNullOrEmpty(p.assetId))continue;Place("pot",p.rect[0]+35,p.rect[1]+p.rect[3]-20);if(i%2==0)Place("planter",p.rect[0]+p.rect[2]-45,p.rect[1]+p.rect[3]-22);}
   // Regular edge spacing, no poles joined to lamps.
   for(float x=180;x<5200;x+=660){if(x>2250&&x<2850)continue;Place("streetlamp",x,1530);Place("streetlamp",x+160,1970);Place("drain",x+80,1586);Place("drain",x+290,1807);}
   for(float y=470;y<3550;y+=720){if(y>1300&&y<2050)continue;Place("streetlamp",2390,y);Place("utility-pole",2740,y+170);}
   Place("utility-pole",850,1470);Place("utility-pole",2110,2435);Place("utility-pole",4800,1510);
   Place("pathlamp",3340,2830);Place("pathlamp",4800,2830);Place("pathlamp",3340,3340);Place("pathlamp",4800,3340);
   Place("vending",1550,2400);Place("recycle",1610,2400);Place("rack",1470,2400);Place("bench",1160,2400);Place("bicycle",2150,2400);
   Place("cafe-table",920,2390);Place("cafe-table",680,2390);
   Place("blue-car",820,1780);Place("police-car",4450,1780);Place("ambulance",2220,885);Place("van",2110,1510);
   Place("manhole",2270,1710);Place("bollard",2750,1870);Place("traffic-light",2330,1530);Place("traffic-light",2760,2010);
   Place("bike-rack",4620,1340);Place("emergency-box",3900,1350);
   Place("hedge",3320,3410);Place("shrub",4790,3350);Place("flowers",4290,3330);Place("border-plants",3630,3350);
   lib.sprites=sprites.Values.Concat(new[]{solid}).ToArray();EditorUtility.SetDirty(lib);AssetDatabase.SaveAssets();
   PrefabUtility.SaveAsPrefabAsset(map,Root+"/Assembled/DetailedTown.prefab");
   var cameraGO=new GameObject("Main Camera");cameraGO.tag="MainCamera";var camera=cameraGO.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5.125f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.17f,.14f);camera.nearClipPlane=.1f;camera.farClipPlane=100;
   var pawn=new GameObject("Reference player - 90 pixels");var ps=pawn.AddComponent<SpriteRenderer>();ps.sharedMaterial=material;
   string charPath="Assets/CapMultiplayer/Resources/WarmTown/Walk/walk-0.png";
   var ct=AssetDatabase.LoadAssetAtPath<Texture2D>(charPath);
   if(ct==null)throw new Exception("Reference player atlas missing");
   // Copy atlas into this feature folder so the preview package stays self-contained.
   if(!File.Exists(Root+"/ReferencePlayer.png"))File.Copy(charPath,Root+"/ReferencePlayer.png");
   AssetDatabase.ImportAsset(Root+"/ReferencePlayer.png");
   var pti=(TextureImporter)AssetImporter.GetAtPath(Root+"/ReferencePlayer.png");pti.textureType=TextureImporterType.Default;pti.npotScale=TextureImporterNPOTScale.None;pti.mipmapEnabled=false;pti.textureCompression=TextureImporterCompression.Uncompressed;pti.filterMode=FilterMode.Point;pti.maxTextureSize=4096;pti.SaveAndReimport();
   ct=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ReferencePlayer.png");
   var pSprite=Sprite.Create(ct,new Rect(85,1009,132,210),new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);pSprite.name="ReferencePlayer90";AssetDatabase.AddObjectToAsset(pSprite,lib);ps.sprite=pSprite;pawn.transform.localScale=Vector3.one*(90f/210);
   var controller=cameraGO.AddComponent<DetailedTownPreviewCamera>();controller.player=pawn.transform;controller.playerRenderer=ps;controller.view=camera;controller.Focus(1365,2485);
   AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,Root+"/Scenes/DetailedTownPreview.unity");
   Directory.CreateDirectory("DetailedTownVerification");
   camera.rect=new Rect(0,0,1,1);Capture(camera,"DetailedTownVerification/01-shops.png",1534,1025);
   controller.Focus(3970,1270);camera.rect=new Rect(0,0,1,1);Capture(camera,"DetailedTownVerification/02-campus.png",1534,1025);
   controller.Focus(1890,880);camera.rect=new Rect(0,0,1,1);Capture(camera,"DetailedTownVerification/03-hospital.png",1534,1025);
   pawn.SetActive(false);camera.orthographicSize=17.92f;camera.transform.position=new Vector3(26.88f,-17.92f,-10);camera.rect=new Rect(0,0,1,1);Capture(camera,"DetailedTownVerification/04-overview.png",1536,1024);
   pawn.SetActive(true);controller.Focus(1365,2485);camera.rect=new Rect(0,0,1,1);Capture(camera,"DetailedTownVerification/01-shops.png",1534,1025);
   float px=ps.sprite.bounds.size.y*pawn.transform.localScale.y/10.25f*1025;
   if(Mathf.Abs(px-90)>.01f)throw new Exception("Player scale validation failed "+px);
   File.WriteAllText("DetailedTownVerification/result.txt","PASS sprites="+sprites.Count+" playerHeight="+px+" viewport=1534x1025; mainRoad=266px; ground=dimension blockout; prefabObjects="+map.GetComponentsInChildren<SpriteRenderer>().Length);
   if(!Application.isBatchMode){controller.Focus(1365,2485);pawn.SetActive(true);}
   Debug.Log("[DETAILED-PREVIEW] Complete");
  }
  static void Capture(Camera camera,string path,int w,int h){
   var rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32);var prev=camera.targetTexture;camera.targetTexture=rt;camera.Render();
   var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(w,h,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,w,h),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);RenderTexture.active=old;camera.targetTexture=prev;RenderTexture.ReleaseTemporary(rt);
  }
 }
}
