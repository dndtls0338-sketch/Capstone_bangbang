using System;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed partial class CapWarmTown
    {
        // All placement coordinates are pixels in the approved 1536 x 1024 layout.
        // 3.5 reference-art pixels per layout pixel, 50 reference pixels per world unit.
        public const float MapPPU=50f/3.5f;
        public static Vector3 MapWorld(float x,float y)=>new Vector3(OffsetX+(x-768)/MapPPU,(512-y)/MapPPU,0);
        public const float ReferenceCameraHalfHeight=10.25f;
        public const float ReferencePlayerHeight=1.80f;
        private Sprite SourceSlice(string sheet,string key,int x,int y,int w,int h)
        {
            var texture=Resources.Load<Texture2D>("SourceMap/"+sheet);
            if(texture==null)throw new InvalidOperationException("Missing approved source: "+sheet);
            var s=Sprite.Create(texture,new Rect(x,texture.height-y-h,w,h),new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);
            s.name=key; sprites.Add(key,s); return s;
        }
        private void SourceObject(string key,string name,float centerX,float footY,float width,float bodyFraction=.38f)
        {
            var s=sprites[key];var sr=Make(name);sr.sprite=s;
            float worldWidth=width/MapPPU;
            sr.transform.localScale=Vector3.one*(worldWidth/s.bounds.size.x);
            sr.transform.position=MapWorld(centerX,footY);
            sr.sortingOrder=Depth(sr.transform.position.y);
            ModularObjects.Add(name,sr);
            if(bodyFraction>0)
            {
                float height=s.bounds.size.y*sr.transform.localScale.y;
                Solid(sr,sr.transform.position.x-OffsetX,sr.transform.position.y,worldWidth*.87f,height*bodyFraction);
            }
        }
        private void MapFlat(string name,float x,float y,float w,float h,Color color,int order)
        {
            var p=MapWorld(x+w/2,y+h/2);
            Flat(name,p.x-OffsetX,p.y,w/MapPPU,h/MapPPU,color,order);
        }
        private void SourceGround(string key,float x,float y,float w,float h,int order)
        {
            var source=sprites[key];
            // Tiled rendering uses native source resolution. No texture is stretched over a whole road.
            var tile=Sprite.Create(source.texture,source.rect,new Vector2(.5f,.5f),key=="grass"?100:50,0,SpriteMeshType.FullRect);
            groundSprites.Add(tile);
            var sr=Make("Ground / "+key);sr.sprite=tile;sr.drawMode=SpriteDrawMode.Tiled;
            sr.size=new Vector2(w/MapPPU,h/MapPPU);sr.sortingOrder=order;
            sr.transform.position=MapWorld(x+w/2,y+h/2);GroundSurfaceCount++;
        }
        private void MapKerb(float x,float y,float length,bool vertical=false)
        {
            var sand=new Color(.87f,.72f,.39f);
            if(vertical)
            {
                MapFlat("Vertical kerb",x,y,2.5f,length,sand,-9899);
                return;
            }
            MapFlat("Kerb shadow",x,y,length,2.6f,new Color(.32f,.36f,.37f),-9900);
            MapFlat("Kerb top",x,y,length,1.6f,sand,-9899);
            for(float px=x;px<x+length;px+=16)
                MapFlat("Kerb joint",px,y,.5f,1.6f,new Color(.61f,.58f,.46f),-9898);
        }
        private void MapFence(float x,float y,float length)
        {
            for(float i=0;i<length;i+=56)
                SourceObject("approved-wall","Fence "+x+","+y+","+i,x+i+28,y,56,.18f);
        }
        private void MapTree(float x,float y,float width=60)
        {
            SourceObject("approved-tree","Tree "+x+","+y,x,y,width,0);
            var sr=ModularObjects["Tree "+x+","+y];
            Solid(sr,sr.transform.position.x-OffsetX,sr.transform.position.y,width/MapPPU*.12f,width/MapPPU*.12f);
        }
        private void BuildSourceTown()
        {
            SourceSlice("store","approved-store",390,60,760,685);
            SourceSlice("cafe","approved-cafe",432,39,743,703);
            SourceSlice("hospital","approved-hospital",245,105,1210,650);
            SourceSlice("police","approved-police",385,45,725,680);
            SourceSlice("ordinary","approved-ordinary",402,101,744,714);
            SourceSlice("villa","approved-villa",311,68,916,782);
            SourceSlice("house","approved-house",375,65,804,637);
            SourceSlice("campus","approved-campus",340,12,862,619);
            SourceSlice("student","approved-student",171,36,1318,684);
            SourceSlice("tree","approved-tree",383,75,768,840);
            SourceSlice("props","approved-bench",786,439,645,402);
            SourceSlice("store","approved-racks",850,587,191,154);
            SourceSlice("store","approved-bins",1051,608,192,191);
            SourceSlice("store","store-left-extension",300,680,90,103);
            SourceSlice("store","store-right-extension",1150,600,95,205);
            SourceSlice("store","store-bottom-extension",390,745,760,60);
            SourceSlice("campus","approved-wall",66,841,480,134);
            SourceSlice("reference","reference-paving",796,451,51,51);
            SourceSlice("reference","reference-asphalt",545,572,100,58);
            SourceSlice("reference","reference-drain",880,532,55,39);
            SourceGround("grass",-240,-240,2016,1504,-13000);
            SourceGround("reference-paving",0,0,1536,1024,-12900);
            SourceGround("grass",0,0,690,244,-12890);
            SourceGround("reference-paving",10,112,366,150,-12880);
            SourceGround("reference-paving",399,65,284,199,-12880);
            SourceGround("grass",806,0,730,378,-12890);
            SourceGround("reference-paving",1040,89,242,286,-12880);
            SourceGround("reference-paving",817,286,698,93,-12880);
            SourceGround("reference-paving",1247,215,262,111,-12880);
            SourceGround("reference-asphalt",0,445,1536,77,-12500);
            SourceGround("reference-asphalt",695,0,74,1024,-12500);
            SourceGround("reference-asphalt",0,264,695,24,-12500);
            SourceGround("reference-asphalt",125,705,570,23,-12500);
            var paint=new Color(.93f,.89f,.77f);
            foreach(var span in new[]{new Vector2(0,690),new Vector2(774,1536)})
            {
                MapKerb(span.x,440,span.y-span.x);MapKerb(span.x,523,span.y-span.x);
                for(float x=span.x+12;x<span.y;x+=54)MapFlat("Road dash",x,482,27,1.6f,new Color(.93f,.72f,.34f),-12000);
            }
            foreach(var span in new[]{new Vector2(0,442),new Vector2(527,1024)})
            {
                MapKerb(691,span.x,span.y-span.x,true);MapKerb(770,span.x,span.y-span.x,true);
                for(float y=span.x+20;y<span.y;y+=52)MapFlat("Vertical dash",730,y,1.4f,24,paint,-12000);
            }
            foreach(float x in new[]{385f,659f,800f,1056f})
                for(float y=449;y<520;y+=12)MapFlat("Crosswalk",x-18,y,36,6,paint,-11900);
            foreach(float y in new[]{430f,543f,842f})
                for(float x=700;x<769;x+=12)MapFlat("Crosswalk north-south",x,y-14,6,28,paint,-11900);
            MapKerb(0,261,689);MapKerb(126,730,559);

            // Anchors follow the approved overview; all artwork retains its aspect ratio.
            SourceObject("approved-hospital","Hospital",536,232,248);
            SourceObject("approved-police","Police",519,419,176);
            SourceObject("approved-ordinary","North ordinary",345,421,133);
            SourceObject("01-shops-3","Laundry",99,421,102);
            SourceObject("01-shops-5","Eatery",208,421,114);
            SourceObject("approved-house","North blue house",91,226,152);
            SourceObject("approved-ordinary","North cream house",221,228,94);
            SourceObject("approved-villa","North brick house",322,228,105);

            SourceObject("approved-cafe","Cafe",231,680,150);
            SourceObject("approved-store","Convenience store",390,674,145.4f);
            SourceObject("approved-ordinary","Shop neighbour",558,679,157);
            // Complete the source's clipped bench/bin edges at their exact original offsets,
            // instead of putting duplicate foreground props over the storefront.
            const float storeRatio=145.4f/760;
            SourceObject("store-left-extension","Store bench left edge",390+(345-770)*storeRatio,674+(783-745)*storeRatio,90*storeRatio,.12f);
            SourceObject("store-right-extension","Store bin right edge",390+(1197.5f-770)*storeRatio,674+(805-745)*storeRatio,95*storeRatio,.12f);
            SourceObject("store-bottom-extension","Store apron lower edge",390,674+60*storeRatio,145.4f,0);
            ModularObjects["Store apron lower edge"].sortingOrder=-12000;
            SourceObject("05-props-10","Street parked blue car",244,500,53,.2f);
            SourceObject("05-props-10","Street parked east car",1268,501,53,.2f);
            SourceObject("05-props-6","Cafe terrace",219,701,50,.15f);

            SourceObject("approved-campus","University main hall",1122,178,202);
            SourceObject("approved-campus","Lecture hall",920,309,166);
            SourceObject("approved-student","Student union - detective club",1386,277,268);
            SourceGround("grass",1001,211,232,72,-12800);
            SourceGround("reference-paving",1091,179,73,194,-12790);
            SourceObject("04-infrastructure-5","Campus board",1060,357,39,.18f);
            SourceObject("04-infrastructure-3","Campus bus stop",1264,424,80,.16f);
            MapFence(815,378,240);MapFence(1215,378,286);
            SourceObject("approved-bench","Campus bench",1200,312,34,.18f);
            foreach(var p in new[]{new Vector2(995,321),new Vector2(1240,331),new Vector2(830,232),new Vector2(1494,309),new Vector2(1199,253)})MapTree(p.x,p.y,56);

            SourceObject("01-shops-4","Stationery",961,676,115);
            SourceObject("approved-ordinary","East ordinary",1050,676,83);
            SourceObject("01-shops-5","Snack shop",1130,681,86);
            SourceObject("01-shops-4","Bookshop",1222,681,100);
            SourceObject("approved-villa","East brick house",1435,687,171);
            SourceObject("approved-house","East house",1326,686,71);

            // Joined southwest residential block: no north-south alley through the group.
            SourceObject("approved-ordinary","SW corner house",47,677,87);
            SourceObject("approved-house","SW blue house",54,863,105);
            SourceObject("approved-ordinary","SW cream house",202,873,113);
            SourceObject("approved-ordinary","SW green house",338,875,112);
            SourceObject("approved-villa","SW brick group A",446,877,110);
            SourceObject("approved-villa","SW brick group B",558,877,110);
            SourceObject("approved-house","SW lower blue",177,1006,181);
            SourceObject("approved-ordinary","SW lower cream",373,1013,150);
            SourceObject("approved-villa","SW lower east",618,1012,122);
            SourceObject("approved-house","Park west blue house",846,835,85);
            SourceObject("approved-villa","Park west brick house",845,972,81);
            SourceObject("approved-ordinary","SE edge house",1498,977,120);

            // Open north approach; only the east, west and south edges have fencing.
            SourceGround("grass",945,786,447,158,-12600);
            SourceGround("reference-paving",915,771,31,208,-12550);
            SourceGround("reference-paving",1392,771,29,208,-12550);
            SourceGround("reference-paving",917,942,503,30,-12550);
            SourceGround("reference-paving",1119,786,43,237,-12540);
            SourceGround("reference-paving",1083,817,119,79,-12540);
            MapFence(909,982,196);MapFence(1190,982,238);
            SourceObject("approved-bench","Park bench north",1141,831,37,.17f);
            SourceObject("approved-bench","Park bench east",1267,893,44,.17f);
            SourceObject("approved-bench","Park bench west",1040,899,38,.17f);
            foreach(var p in new[]{new Vector2(997,843),new Vector2(1056,800),new Vector2(1263,800),new Vector2(1340,850),new Vector2(1022,949),new Vector2(1263,954)})MapTree(p.x,p.y,57);
            foreach(var p in new[]{new Vector2(1076,915),new Vector2(1203,872),new Vector2(1310,919),new Vector2(977,886),new Vector2(1178,819)})
                SourceObject("08-nature-6","Park flowers "+p,p.x,p.y,25,0);

            // Modern lamps and distinct utility poles. Their heights are fixed by category.
            foreach(var p in new[]{new Vector2(150,437),new Vector2(530,437),new Vector2(906,437),new Vector2(1175,437),new Vector2(1456,437),new Vector2(310,538),new Vector2(615,538),new Vector2(966,538),new Vector2(1338,538),new Vector2(684,151),new Vector2(787,281),new Vector2(684,685),new Vector2(787,706),new Vector2(684,967),new Vector2(787,982),new Vector2(120,294),new Vector2(506,294),new Vector2(125,736)})
                SourceObject("04-infrastructure-1","Road light "+p,p.x,p.y,20,.045f);
            foreach(var p in new[]{new Vector2(921,789),new Vector2(1130,789),new Vector2(1396,789),new Vector2(925,945),new Vector2(1084,967),new Vector2(1396,945),new Vector2(1077,316),new Vector2(1216,275),new Vector2(1110,182)})
                SourceObject("04-infrastructure-1","Path light "+p,p.x,p.y,12,.045f);
            foreach(var p in new[]{new Vector2(390,254),new Vector2(264,428),new Vector2(664,699),new Vector2(789,193),new Vector2(816,341),new Vector2(1137,561),new Vector2(680,841)})
                SourceObject("04-infrastructure-0","Utility pole "+p,p.x,p.y,24,.07f);
            foreach(float x in new[]{40f,230f,545f,880f,1160f,1460f})SourceObject("reference-drain","Drain "+x,x,449,15,0);
            // Green seams fill property edges without making isolated grassy plots.
            foreach(var p in new[]{new Vector2(18,262),new Vector2(152,259),new Vector2(382,232),new Vector2(663,371),new Vector2(95,541),new Vector2(147,548),new Vector2(299,555),new Vector2(480,546),new Vector2(805,562),new Vector2(868,551),new Vector2(1305,543),new Vector2(1447,424),new Vector2(888,748),new Vector2(1450,750)})MapTree(p.x,p.y,54);
            for(int i=0;i<25;i++)MapTree(15+i*63,65+(i%3)*14,72);
            foreach(var p in new[]{new Vector2(370,118),new Vector2(195,84),new Vector2(290,69),new Vector2(34,80),new Vector2(643,91),new Vector2(871,142),new Vector2(971,134),new Vector2(1285,126),new Vector2(1470,133),new Vector2(995,204),new Vector2(1010,265),new Vector2(1233,205),new Vector2(1315,323),new Vector2(1490,378)})MapTree(p.x,p.y,66);
            // Shared property seams, keeping crossings, store-left passage and park north open.
            for(int i=0;i<10;i++)SourceObject("08-nature-4","NW planting "+i,25+i*66,251,58,.06f);
            for(int i=0;i<9;i++)SourceObject("08-nature-4","Housing seam "+i,30+i*71,747,58,.06f);
            for(int i=0;i<8;i++)SourceObject("08-nature-4","Housing south planting "+i,60+i*76,1009,64,.06f);
            for(int i=0;i<7;i++)SourceObject("08-nature-4","Campus roadside planting "+i,820+i*32,406,31,.05f);
            for(int i=0;i<8;i++)SourceObject("08-nature-4","Campus east planting "+i,1245+i*31,401,30,.05f);
            for(int i=0;i<6;i++)
            {
                SourceObject("08-nature-5","Park west border "+i,958,814+i*25,26,.08f);
                SourceObject("08-nature-5","Park east border "+i,1380,814+i*25,26,.08f);
            }
            for(int i=0;i<7;i++)SourceObject("08-nature-6","Park south flowers "+i,968+i*62,941,31,0);
            foreach(var p in new[]{new Vector2(113,875),new Vector2(270,871),new Vector2(385,878),new Vector2(522,896),new Vector2(663,958),new Vector2(276,1022),new Vector2(490,1017),new Vector2(45,979)})MapTree(p.x,p.y,51);
            for(int i=0;i<14;i++){MapTree(-12,80+i*73,65);MapTree(1540,55+i*75,70);}
            for(int i=0;i<21;i++)MapTree(22+i*74,1060+(i%2)*5,72);
            Debug.Log($"[SOURCE-MAP] {ModularObjects.Count} separate source sprites; {GroundSurfaceCount} native-resolution ground surfaces. Reference 1534x1025, player 90px, camera half-height 10.25.");
        }
    }
}
