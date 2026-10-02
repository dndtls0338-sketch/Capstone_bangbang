using System;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed partial class CapWarmTown
    {
        // The approved art is a shared atlas. Ground cells and depth-sorted object
        // regions have independent renderers; physical footprints use separate data.
        // Coordinates are source-art pixels, top-left origin (1584 x 993).
        public const float AtlasWidth=1584, AtlasHeight=993, AtlasPPU=18;
        [Serializable] public class ArtPiece
        {
            public string name;
            public float x,y,w,h,foot;
            public bool crown;
            public float[] solid;
        }
        [Serializable] public class ArtLayout { public ArtPiece[] pieces; public ArtPiece[] barriers; }
        public static Vector3 AtlasWorld(float x,float y) => new Vector3(OffsetX+(x-AtlasWidth/2)/AtlasPPU,(AtlasHeight/2-y)/AtlasPPU,0);
        public static Vector2 AtlasLocal(float x,float y) { var p=AtlasWorld(x,y);return new Vector2(p.x-OffsetX,p.y); }
        public int ArtLayerCount { get; private set; }
        public int GroundCellCount { get; private set; }

        private void BuildRejectedAtlas()
        {
            var texture=Resources.Load<Texture2D>("WarmTown/Approved/neighborhood");
            var data=Resources.Load<TextAsset>("WarmTown/Approved/layout");
            if(texture==null || data==null) throw new InvalidOperationException("Approved town atlas/layout missing");
            texture.filterMode=FilterMode.Point;
            var layout=JsonUtility.FromJson<ArtLayout>(data.text);
            // Texture coordinates are cropped at runtime. The imported source stays
            // lossless; no blurry stretched screenshot, resizing, or JPEG conversion.
            for(int row=0;row<8;row++) for(int col=0;col<8;col++)
            {
                var piece=new ArtPiece{name=$"Ground cell {col},{row}",x=col*198,y=row*124,w=198,h=row==7?125:124};
                ArtRegion(texture,piece,-12000); GroundCellCount++;
            }
            foreach(var p in layout.pieces)
            {
                ArtRegion(texture,p,Depth(AtlasWorld(0,p.foot).y));ArtLayerCount++;
                if(p.solid!=null && p.solid.Length==4) AtlasSolid(p.name,p.solid);
            }
            foreach(var p in layout.barriers) AtlasSolid(p.name,p.solid);
            Debug.Log($"[CAP-ART] Approved map: {GroundCellCount} ground cells, {ArtLayerCount} depth layers, {Obstacles.Count} physical footprints; atlas {texture.width}x{texture.height}");
        }

        private void ArtRegion(Texture2D texture,ArtPiece p,int order)
        {
            float sx=texture.width/AtlasWidth,sy=texture.height/AtlasHeight;
            var rect=new Rect(p.x*sx,texture.height-(p.y+p.h)*sy,p.w*sx,p.h*sy);
            var sprite=Sprite.Create(texture,rect,new Vector2(.5f,.5f),texture.width/88f,0,SpriteMeshType.FullRect);
            sprite.name=p.name;groundSprites.Add(sprite);
            if(p.crown)
            {
                // An octagonal silhouette prevents a rectangular tree cutout from
                // covering players standing beside the crown.
                float w=sprite.bounds.size.x/2,h=sprite.bounds.size.y/2;
                sprite.OverrideGeometry(new[]{new Vector2(-w*.5f,h),new Vector2(w*.5f,h),new Vector2(w,h*.45f),new Vector2(w,-h*.45f),new Vector2(w*.5f,-h),new Vector2(-w*.5f,-h),new Vector2(-w,-h*.45f),new Vector2(-w,h*.45f)},
                    new ushort[]{0,2,1,0,3,2,0,4,3,0,5,4,0,6,5,0,7,6});
            }
            var sr=Make(p.name);sr.sprite=sprite;sr.sortingOrder=order;
            sr.transform.position=AtlasWorld(p.x+p.w/2,p.y+p.h/2);
        }
        private void AtlasSolid(string name,float[] r)
        {
            if(r==null || r.Length!=4 || r[2]<=0 || r[3]<=0)return;
            var center=AtlasWorld(r[0]+r[2]/2,r[1]+r[3]/2);
            var size=new Vector2(r[2]/AtlasPPU,r[3]/AtlasPPU);
            Obstacles.Add(new Rect(center.x-OffsetX-size.x/2,center.y-size.y/2,size.x,size.y));
            var go=new GameObject("Collision · "+name);go.transform.SetParent(root.transform,false);go.transform.position=center;
            go.AddComponent<BoxCollider2D>().size=size;
        }
    }
}
