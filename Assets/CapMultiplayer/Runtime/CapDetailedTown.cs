using System;
using UnityEngine;
namespace Cap.Multiplayer {
 public sealed partial class CapWarmTown {
  [Serializable] private class ShapeRoad { public string id,axis; public float[] rect; }
  [Serializable] private class ShapeBuilding { public string name,kind; public float[] rect; }
  [Serializable] private class ShapePoint { public float x,y; }
  [Serializable] private class ShapeLayout { public ShapeRoad[] roads; public ShapeBuilding[] buildings; public ShapePoint[] trees; }
  // Layout pixels, top-left origin; 50 pixels = 1 world unit.
  private static Vector3 PixelWorld(float x,float y) => new Vector3(OffsetX+(x-2688)/50,(1792-y)/50,0);
  private void ShapeRect(string name,float x,float y,float w,float h,Color color,int order,bool solid=false) {
   var sr=Make(name);sr.sprite=white;sr.color=color;sr.sortingOrder=order;
   sr.transform.position=PixelWorld(x+w/2,y+h/2);sr.transform.localScale=new Vector3(w/50,h/50,1);
   if(!solid)return;
   // Collision exactly matches the visible rectangle.
   Obstacles.Add(new Rect((x-2688)/50,(1792-y-h)/50,w/50,h/50));
   sr.gameObject.AddComponent<BoxCollider2D>().size=Vector2.one;ModularObjects.Add(name,sr);
  }
  private void BuildShapeTown() {
   var data=Resources.Load<TextAsset>("Blockout/layout");
   if(data==null)throw new InvalidOperationException("Missing Blockout/layout.json");
   var plan=JsonUtility.FromJson<ShapeLayout>(data.text);
   ShapeRect("Shared pavement",0,0,5376,3584,new Color(.78f,.79f,.77f),-12000);
   ShapeRect("Campus lawn",2780,0,2596,1420,new Color(.60f,.72f,.58f),-11995);
   ShapeRect("Campus courtyard",3440,730,1020,700,new Color(.86f,.84f,.77f),-11990);
   ShapeRect("Park lawn",3136,2550,1841,880,new Color(.55f,.71f,.53f),-11995);
   ShapeRect("Park path",3940,2550,135,880,new Color(.86f,.84f,.77f),-11990);
   ShapeRect("Park crossing",3280,3000,1500,110,new Color(.86f,.84f,.77f),-11990);
   foreach(var road in plan.roads) {
    var r=road.rect;
    ShapeRect(road.id+" edge",r[0]-12,r[1]-12,r[2]+24,r[3]+24,new Color(.92f,.77f,.42f),-11900);
    ShapeRect(road.id,r[0],r[1],r[2],r[3],new Color(.31f,.36f,.40f),-11890);
   }
   foreach(var road in plan.roads) {
    if(!road.id.StartsWith("main"))continue;
    var r=road.rect;bool horizontal=road.axis=="h";float length=horizontal?r[2]:r[3];
    for(float n=35;n<length;n+=130) {
     float x=horizontal?r[0]+n:r[0]+r[2]/2,y=horizontal?r[1]+r[3]/2:r[1]+n;
     if(x>2340&&x<2780&&y>1470&&y<1930)continue;
     ShapeRect("Road marking",x,y,horizontal?64:5,horizontal?5:64,new Color(.98f,.83f,.45f),-11880);
    }
   }
   for(int i=0;i<6;i++) {
    float n=i*42;
    ShapeRect("Crosswalk north",2435+n,1460,22,70,Color.white,-11870);
    ShapeRect("Crosswalk south",2435+n,1855,22,70,Color.white,-11870);
    ShapeRect("Crosswalk west",2320,1572+n,70,22,Color.white,-11870);
    ShapeRect("Crosswalk east",2720,1572+n,70,22,Color.white,-11870);
   }
   int index=0;
   foreach(var b in plan.buildings) {
    var r=b.rect;Color c=b.kind=="house"?new Color(.56f,.60f,.65f):new Color(.72f,.52f,.34f);
    if(b.kind=="hospital")c=new Color(.33f,.66f,.65f);
    if(b.kind=="police")c=new Color(.35f,.48f,.69f);
    if(b.kind=="lecture"||b.kind=="campus-main"||b.kind=="student-union")c=new Color(.44f,.49f,.68f);
    ShapeRect(b.name+" "+index++,r[0],r[1],r[2],r[3],c,-2000,true);
    ShapeRect("Door marker",r[0]+r[2]/2-22,r[1]+r[3]-8,44,8,new Color(1,.86f,.48f),-1990);
    ShapeLabel(b.name,r[0]+r[2]/2,r[1]+r[3]/2);
    if(b.kind=="student-union")StudentDoor=PixelWorld(r[0]+r[2]/2,r[1]+r[3]+45);
   }
   foreach(var t in plan.trees)ShapeRect("Tree block "+index++,t.x-24,t.y-48,48,48,new Color(.25f,.47f,.34f),-2000,true);
   ShapeLabel("공원",3590,2780);ShapeLabel("캠퍼스 중앙 마당",3975,1140);
   GroundSurfaceCount=plan.roads.Length+6;
   Debug.Log("[SHAPE-TOWN] Geometry ready; buildings="+plan.buildings.Length+" obstacles="+Obstacles.Count+"; artwork textures=0");
  }
 }
}
