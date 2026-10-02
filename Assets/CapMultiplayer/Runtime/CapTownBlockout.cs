using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed partial class CapWarmTown
    {
        // Plan coordinates match the approved 88x64 diagram: origin at northwest.
        private Font blockoutFont;
        private readonly System.Collections.Generic.List<TextMesh> blockoutLabels=new System.Collections.Generic.List<TextMesh>();
        private void UpdateBlockoutLabels() { foreach(var tm in blockoutLabels) tm.transform.localScale=Vector3.one*(Overview?3.5f:1); }
        public static Vector3 PlanWorld(float x,float y) => new Vector3(OffsetX+x-44,32-y,0);
        private static readonly Color Ground=new Color(.86f,.85f,.79f);
        private static readonly Color Roof=new Color(.58f,.62f,.63f);
        private static readonly Color CampusRoof=new Color(.44f,.57f,.65f);
        private static readonly Color Paving=new Color(.94f,.91f,.83f);
        private static readonly Color LaneColor=new Color(.72f,.73f,.70f);
        private static readonly Color Ink=new Color(.19f,.25f,.28f);

        private void PlanRect(string name,float x,float y,float w,float h,Color color,int order=-9900,bool solid=false)
        {
            float right=Mathf.Min(88,x+w),bottom=Mathf.Min(64,y+h); x=Mathf.Max(0,x);y=Mathf.Max(0,y);w=right-x;h=bottom-y;
            if(w<=0 || h<=0) return;
            Flat(name,x+w/2-44,32-y-h/2,w,h,color,order);
            if(solid)
            {
                var rect=new Rect(x-44,32-y-h,w,h); Obstacles.Add(rect);
                var go=new GameObject(name+" footprint");go.transform.SetParent(root.transform,false);
                go.transform.position=PlanWorld(x+w/2,y+h/2);
                go.AddComponent<BoxCollider2D>().size=new Vector2(w,h);
            }
        }
        private void PlanLine(string name,Vector2 a,Vector2 b,float width,Color color,int order=-9890,bool solid=false)
        {
            PlanRect(name,Mathf.Min(a.x,b.x)-width/2,Mathf.Min(a.y,b.y)-width/2,
                Mathf.Abs(a.x-b.x)+width,Mathf.Abs(a.y-b.y)+width,color,order,solid);
        }
        private void Road(string name,float width,params Vector2[] points)
        {
            for(int i=1;i<points.Length;i++)
            {
                var a=points[i-1];var b=points[i];
                PlanLine(name+" shallow gutter",a,b,width+.16f,new Color(.35f,.38f,.39f),-9886);
                float x=Mathf.Min(a.x,b.x)-width/2,y=Mathf.Min(a.y,b.y)-width/2;
                float w=Mathf.Abs(a.x-b.x)+width,h=Mathf.Abs(a.y-b.y)+width;
                float right=Mathf.Min(88,x+w),bottom=Mathf.Min(64,y+h);x=Mathf.Max(0,x);y=Mathf.Max(0,y);w=right-x;h=bottom-y;
                Fill(name,"asphalt",x+w/2-44,32-y-h/2,w,h,-9885);
            }
        }
        private void RoadNetwork()
        {
            // A single unmarked, shared vehicle/pedestrian loop; small branches serve actual homes.
            Road("Residential access road",3.4f,new Vector2(27,43.8f),new Vector2(27,19.5f),new Vector2(58,19.5f));
            Road("College local road",4.2f,new Vector2(58,19.5f),new Vector2(58,43.8f));
            Road("Northern residential spur",3.4f,new Vector2(27,19.5f),new Vector2(27,1.7f));
            Road("Shared court short access",3.2f,new Vector2(27,25),new Vector2(11,25));
            Road("Campus gate street",4,new Vector2(58,20),new Vector2(72,20));
            Road("South housing road",3.4f,new Vector2(27,48.3f),new Vector2(27,52),new Vector2(38,52),new Vector2(38,57),new Vector2(60,57));
            Road("South court road",3.2f,new Vector2(27,52),new Vector2(3,52));
            Road("Workshop delivery access",3.4f,new Vector2(84,48.3f),new Vector2(84,62),new Vector2(68,62));
            // Campus/park paths remain pedestrian paving; they are not an extra road network.
            PlanLine("Gate pedestrian threshold",new Vector2(72,16),new Vector2(72,18),3.4f,Paving,-9875);
            PlanLine("Court driveway",new Vector2(23,13.5f),new Vector2(27,13.5f),2.5f,LaneColor,-9875);
            PlanLine("Court south driveway",new Vector2(13.5f,21.5f),new Vector2(13.5f,25),2.5f,LaneColor,-9875);
            // Off-lane parking demonstrates vehicle scale without narrowing the through route.
            PlanRect("Residential parking recess",17,27,7,4.5f,new Color(.64f,.65f,.62f),-9880);
            for(int i=0;i<3;i++) PlanRect("Parking division",17.3f+i*3.1f,27.3f,.07f,3.8f,Paving,-9870);
            PlanRect("Parked vehicle footprint",18,27.7f,1.8f,3.5f,new Color(.39f,.48f,.55f),-9780,true);
            PlanRect("Parked vehicle window",18.2f,28.4f,1.4f,.7f,new Color(.68f,.77f,.79f),-9770);
            foreach(var p in new[]{new Vector2(25.7f,31),new Vector2(32,18),new Vector2(56.1f,30),new Vector2(72,21.6f)})
            {
                PlanRect("Roadside drainage grate",p.x-.18f,p.y-.4f,.36f,.8f,Ink,-9870);
                for(int i=0;i<4;i++)PlanRect("Grate slot",p.x-.13f,p.y-.3f+i*.18f,.26f,.06f,LaneColor,-9865);
            }
        }
        private void Mass(string name,float x,float y,float w,float h,bool campus=false)
        {
            // Contiguous volumes share party walls; no private sprite-sized paving islands.
            PlanRect(name+" shadow",x+.25f,y+.35f,w,h,new Color(.67f,.67f,.62f),-9850);
            PlanRect(name,x,y,w,h,campus?CampusRoof:Roof,-9800,true);
            PlanRect(name+" south wall",x,y+h-.55f,w,.55f,campus?new Color(.33f,.44f,.51f):new Color(.42f,.47f,.48f),-9798);
        }
        private void Wall(float x1,float y1,float x2,float y2)
        {
            PlanLine("Shared boundary wall",new Vector2(x1,y1),new Vector2(x2,y2),.24f,Ink,-9780,true);
        }
        private void Door(float x,float y)
        {
            PlanRect("Entrance",x-.6f,y-.45f,1.2f,.55f,new Color(.95f,.73f,.35f),-9760);
            PlanRect("Entrance threshold",x-.65f,y+.12f,1.3f,.35f,Paving,-9840);
        }
        private void MapLabel(string value,float x,float y,float size=.12f, bool light=false)
        {
            if(blockoutFont==null) blockoutFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},48);
            blockoutFont.RequestCharactersInTexture(value,48,FontStyle.Normal);
            var go=new GameObject("Label: "+value);go.transform.SetParent(root.transform,false);go.transform.position=PlanWorld(x,y);
            var tm=go.AddComponent<TextMesh>();blockoutLabels.Add(tm);tm.font=blockoutFont;tm.fontSize=48;tm.characterSize=size;
            tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=light?new Color(.98f,.98f,.94f):Ink;tm.text=value;
            var mr=go.GetComponent<MeshRenderer>();mr.sharedMaterial=blockoutFont.material;mr.sortingOrder=31000;
        }
        private void BuildBlockout()
        {
            PlanRect("88 x 64 neighborhood",0,0,88,64,Ground,-10000);
            // Public surfaces are shared continuously across properties.
            PlanRect("Continuous high street pavement",0,41,88,10,Paving,-9950);
            PlanRect("High street",0,43,88,6,new Color(.47f,.51f,.53f),-9900);
            for(float x=1;x<88;x+=3) PlanRect("Road center dash",x,45.94f,1.4f,.12f,new Color(.94f,.90f,.77f),-9880);
            foreach(float x in new[]{27f,58f,73f}) for(float y=43.25f;y<49;y+=.9f)
                PlanRect("Pedestrian crossing",x-.9f,y,1.8f,.5f,Paving,-9870);
            PlanRect("Shared residential court A",9.5f,9.5f,15.6f,12,Paving);
            PlanRect("Shared residential court B",38,10.5f,9,7.5f,Paving);
            PlanRect("University plaza",62,19,13,13,Paving);
            PlanRect("Campus forecourt",68,9,17.5f,9,Paving);
            PlanRect("Campus lawn",62,1,24,17,new Color(.75f,.79f,.67f),-9940);
            PlanRect("Campus common court",68,9,17.5f,8.8f,Paving,-9930);
            PlanRect("Neighborhood park",30,22,25.6f,10.3f,new Color(.68f,.76f,.59f));
            PlanLine("Park shared path",new Vector2(35.8f,26.5f),new Vector2(45,26.5f),2.4f,Paving);
            PlanLine("Park shared path",new Vector2(45,26.5f),new Vector2(45,30.2f),2.4f,Paving);
            PlanRect("South shared court",9,56.8f,13,5.2f,Paving);
            PlanRect("Rear service yard",72,57,9,4,Paving);

            RoadNetwork();

            Mass("Courtyard A north row",3,3,22.1f,6.5f);Mass("Courtyard A west wing",3,9.5f,6.5f,12);Mass("Courtyard A south home",16,17,9.1f,4.5f);
            Mass("Courtyard B north row",29,4,26.6f,6.5f);Mass("Courtyard B west wing",29,10.5f,9,7);Mass("Courtyard B east wing",47,11,8.6f,6.5f);
            Mass("West attached shops",3,34,21.3f,7);Mass("East attached shops",29,34.5f,26.6f,6.5f);Mass("Shop rear wing",3,29,6,5);
            Mass("Campus cafe and bookstore",60.4f,33,22.6f,8);Mass("College retail return",77,21,6,12);Mass("Plaza cafe wing",60.4f,23,8.6f,6);
            Mass("University club building",62,3,22,6,true);Mass("University west wing",62,9,6,7,true);
            Mass("South court west home",3,55,6,7);Mass("South court north row",9,53.8f,13,3);Mass("South court east home",22,56,10,6);Mass("South rear row",42,60,18,3);
            Mass("Hospital",64,52,8,8);Mass("Workshop",72,52,9,5);

            // Seams designate attached units, without gaps, repeated paving or individual prop bundles.
            foreach(float x in new[]{10f,16.5f}) PlanRect("Party wall",x,3,.08f,6.5f,Ink,-9790);
            foreach(float x in new[]{40f,47f}) PlanRect("Party wall",x,4,.08f,6.5f,Ink,-9790);
            foreach(float x in new[]{10.5f,18f}) PlanRect("Party wall",x,34,.08f,7,Ink,-9790);
            foreach(float x in new[]{36.3f,43.6f}) PlanRect("Party wall",x,34.5f,.08f,6.5f,Ink,-9790);
            foreach(float x in new[]{71f,77f}) PlanRect("Party wall",x,33,.08f,8,Ink,-9790);
            foreach(var p in new[]{new Vector2(12,9.5f),new Vector2(20,21.5f),new Vector2(41,10.5f),new Vector2(7,41),new Vector2(14,41),new Vector2(22,41),new Vector2(33,41),new Vector2(40,41),new Vector2(48,41),new Vector2(67,41),new Vector2(74,41),new Vector2(72,9),new Vector2(26,62),new Vector2(68,60)}) Door(p.x,p.y);

            // Campus enclosure makes the starting point an interior court with one distinct south gate.
            Wall(61,2,86,2);Wall(61,2,61,18);Wall(86,2,86,18);
            Wall(61,18,69,18);Wall(75,18,86,18);
            PlanRect("Gate left pier",68.7f,17.65f,.6f,.7f,Ink,-9770,true);
            PlanRect("Gate right pier",74.7f,17.65f,.6f,.7f,Ink,-9770,true);
            // Private courts are bounded by shared walls, leaving purposeful entrances on the lane side.
            Wall(25.1f,9.5f,25.1f,12);Wall(25.1f,15,25.1f,17);Wall(9.5f,21.5f,12,21.5f);Wall(15,21.5f,16,21.5f);
            Wall(38,17.5f,41,17.5f);Wall(44,17.5f,47,17.5f);
            Wall(9,62,13,62);Wall(16,62,22,62);

            // Plot boundaries sit directly on the shared roadway, rather than islands of sidewalk.
            Wall(25.1f,26.9f,25.1f,32.8f);Wall(9,32.8f,25.1f,32.8f);
            Wall(28.9f,22,28.9f,27);Wall(28.9f,29.7f,28.9f,34.3f);
            Wall(55.7f,22,55.7f,25);Wall(55.7f,28,55.7f,32.3f);
            Wall(30,21.7f,40,21.7f);Wall(43,21.7f,55.7f,21.7f);
            MapLabel("동아리실",72,6,.16f,true);MapLabel("캠퍼스 안마당",77.5f,12.2f,.10f);
            MapLabel("정문",72,17.2f,.10f);MapLabel("대학 앞 골목도로",65.5f,20,.09f,true);
            MapLabel("공동 마당 A",16,13,.10f);MapLabel("공동 마당 B",42,14,.085f);
            MapLabel("생활 상가",14,37,.15f,true);MapLabel("생활 상가",40,37,.15f,true);
            MapLabel("카페 · 서점",73,36.5f,.15f,true);MapLabel("작은 광장",72.5f,25,.10f);
            MapLabel("동네 공원",42,24,.11f);MapLabel("주택 골목도로",27,24.6f,.075f,true);
            MapLabel("남쪽 주거지",16,59.2f,.10f);MapLabel("병원",68,55.5f,.13f,true);MapLabel("작업장",76.5f,54.5f,.10f,true);
            MapLabel("큰길",44,46,.14f,true);
            MapLabel("주차 포켓",21.6f,29.2f,.07f);
        }
    }
}
