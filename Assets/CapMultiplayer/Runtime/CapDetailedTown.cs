using System;
using System.Linq;
using UnityEngine;
namespace Cap.Multiplayer {
 public sealed partial class CapWarmTown {
  private void BuildDetailedTown() {
   var prefab=Resources.Load<GameObject>("DetailedTown/DetailedTown");
   if(prefab==null)throw new InvalidOperationException("Detailed town prefab missing");
   var town=Instantiate(prefab,root.transform);
   town.name="Detailed town - playable";
   // Preview art is 100 px/unit; multiplayer uses 50 px/unit, same screen pixel size.
   town.transform.position=new Vector3(OffsetX-53.76f,35.84f,0);
   town.transform.localScale=Vector3.one*2;
   var buildingIds=new[]{"convenience","cafe","hospital","police","ordinary","campus-main","lecture","student-union","laundry","restaurant","stationery","general-store","snack","bookstore","house-blue","house-gray","house-orange","house-brick","house-green","residential-tall","duplex","house-flat-blue"};
   foreach(var sr in town.GetComponentsInChildren<SpriteRenderer>()){
    sr.sharedMaterial=ArtMaterial;
    if(sr.transform.parent.name.StartsWith("Ground")){
     // Unity's temporary whiteTexture cannot be relied on as a saved prefab dependency.
     sr.sprite=white;
     sr.sortingOrder=-15000+sr.sortingOrder;GroundSurfaceCount++;continue;
    }
    sr.sortingOrder=Depth(sr.transform.position.y);
    string key=sr.sprite.name;
    string name=sr.name+" #"+ModularObjects.Count;
    ModularObjects.Add(name,sr);
    if(key=="convenience")ModularObjects["Convenience store"]=sr;
    float w=sr.bounds.size.x,h=sr.bounds.size.y;
    float cw=0,ch=0;
    if(buildingIds.Contains(key)){cw=w*.90f;ch=h*.55f;}
    else if(key.StartsWith("tree-")){cw=w*.13f;ch=.35f;}
    else if(key=="fence"){cw=w*.96f;ch=.24f;}
    else if(key=="bus-shelter"){cw=w*.86f;ch=h*.25f;}
    else if(key=="bench"||key=="cafe-table"||key=="bike-rack"){cw=w*.86f;ch=h*.25f;}
    else if(key=="blue-car"||key=="police-car"||key=="van"||key=="ambulance"){cw=w*.84f;ch=h*.65f;}
    else if(key=="streetlamp"||key=="pathlamp"||key=="utility-pole"||key=="traffic-light"||key=="bus-sign"){cw=.20f;ch=.22f;}
    else if(key=="pot"||key=="planter"||key=="round-planter"||key=="shrub"){cw=w*.55f;ch=h*.25f;}
    else if(key=="noticeboard"||key=="gatepost"||key=="vending"||key=="recycle"||key=="bin"||key=="rack"||key=="emergency-box"){cw=w*.70f;ch=h*.20f;}
    if(cw>0){
     var r=new Rect(sr.transform.position.x-OffsetX-cw/2,sr.transform.position.y,cw,ch);
     Obstacles.Add(r);
     var box=sr.gameObject.AddComponent<BoxCollider2D>();
     box.size=new Vector2(cw/sr.transform.lossyScale.x,ch/sr.transform.lossyScale.y);
     box.offset=new Vector2(0,ch/(2*sr.transform.lossyScale.y));
    }
   }
   Debug.Log("[DETAILED-TOWN] Runtime map ready; sprites="+ModularObjects.Count+" footprints="+Obstacles.Count);
  }
 }
}
