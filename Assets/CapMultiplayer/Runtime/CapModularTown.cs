using System.Collections.Generic;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed partial class CapWarmTown
    {
        public readonly Dictionary<string,SpriteRenderer> ModularObjects=new Dictionary<string,SpriteRenderer>();
        public int GroundSurfaceCount { get; private set; }

        private void Build() { BuildDetailedTown(); }

        private void BuildLegacyModularTown()
        {
            // Continuous ground surfaces: no tile grid or repeated texture patches.
            Paint("grass",0,0,88,64);
            // Continuous shared surfaces. Buildings never get individual paving islands.
            Paint("pavement",2,2,23,22);Paint("pavement",29,2,27,20);
            Paint("pavement",61,1,25,40);Paint("pavement",2,29,23,14);
            Paint("pavement",29,30,27,13);Paint("pavement",0,40,88,12);
            Paint("pavement",2,50,54,14);Paint("pavement",62,50,24,14);
            Paint("asphalt",0,43,88,6);
            Paint("asphalt",25,0,4,56);Paint("asphalt",27,18,34,4);
            Paint("asphalt",56,18,5,46);Paint("asphalt",61,19,12,3);
            Paint("asphalt",4,24,23,3);Paint("asphalt",2,56,59,3.5f);
            Paint("asphalt",82,49,4,15);Paint("asphalt",66,61,20,3);
            // Front-facing continuous curb: small repeated segments, gaps at roads.
            foreach(var range in new[]{new Vector2(0,25),new Vector2(29,56),new Vector2(61,88)})
            { Curb(range.x,43,range.y-range.x);Curb(range.x,49,range.y-range.x); }
            for(float x=1;x<88;x+=3)PlanRect("Centre marking",x,45.94f,1.3f,.12f,new Color(.93f,.85f,.56f),-10000);
            foreach(float x in new[]{27f,58.5f,73f})
                for(float y=43.2f;y<49;y+=.85f) PlanRect("Crosswalk stripe",x-1,y,2,.4f,new Color(.94f,.93f,.84f),-9990);
            // Small curb returns delineate entrances without drawing sidewalks around alleys.
            foreach(float x in new[]{24.7f,29.1f,55.7f,61.1f})
                PlanRect("Kerb return",x,41.7f,.18f,1.25f,new Color(.60f,.63f,.62f),-10020);

            // Connected house groups: narrow gaps for party walls, shared courtyards.
            Place("02-buildings-0","A north red",6.8f,12,8,7.0f,4.6f);
            Place("02-buildings-1","A north cream",14,12,6.4f,5.7f,4.5f);
            Place("02-buildings-0","A north red east",21,12,7.4f,6.5f,4.5f);
            Place("02-buildings-1","A south west",6,23,6.6f,5.8f,4.5f);
            Place("02-buildings-0","A south east",21,23,7.1f,6.2f,4.5f);
            Place("02-buildings-0","B north red",34,12,8.4f,7.4f,5);
            Place("02-buildings-1","B north cream",42,12,7,6.2f,5);
            Place("02-buildings-2","B north apartment",50.5f,12,9.2f,8.1f,5);
            Place("02-buildings-1","B courtyard east",51.5f,20,6.4f,5.6f,3.8f);
            // Full-width street fronts share a single sidewalk, not scattered lots.
            Place("01-shops-3","Laundry",7.7f,40.4f,11,10,5.8f);
            Place("01-shops-0","Convenience store",18.5f,40.4f,11,10,5.8f);
            Place("01-shops-5","Hardware",35.5f,40.4f,12,11,5.8f);
            Place("01-shops-4","Book shop central",47.5f,40.4f,12,11,5.8f);
            Place("01-shops-1","Coffee shop",66.6f,40.4f,10.8f,9.7f,5.8f);
            Place("01-shops-4","Campus bookstore",77.5f,40.4f,10.8f,9.7f,5.8f);
            // University club, side wing and a common courtyard with one open gate.
            Place("02-buildings-5","Campus club",74,11.5f,18,16,6.5f);
            Place("02-buildings-3","Campus west wing",64.8f,18,7.5f,6.6f,4);
            Place("04-infrastructure-5","Campus noticeboard",82.4f,17,2,1.65f,.3f);
            Place("05-props-7","Campus bicycle",69,16.3f,2.1f,1.65f,.32f);
            Place("05-props-7","Campus bicycle 2",71.1f,16.3f,2.1f,1.65f,.32f);
            Fence(61,18.8f,8.5f);Fence(76,18.8f,10);
            // Larger park with broad continuous paths; edge furniture leaves passages clear.
            Paint("grass",30,22.5f,25,8.5f);
            Paint("pavement",40.5f,22,2.5f,10);Paint("pavement",31,26,23,2);
            Place("03-greenery-2","Park west tree",33,26,3.9f,1.2f,.7f);
            Place("03-greenery-0","Park east tree",52.4f,26,3.6f,1.1f,.6f);
            Place("03-greenery-3","Park north tree",47.5f,24.5f,2.5f,.8f,.5f);
            Place("05-props-0","Park bench west",36.4f,29.5f,2.1f,1.85f,.4f);
            Place("05-props-0","Park bench east",47.3f,29.5f,2.1f,1.85f,.4f);
            Place("03-greenery-7","Park flowers west",35,31,3.4f,3,.5f);
            Place("03-greenery-7","Park flowers east",50.5f,31,3.4f,3,.5f);
            // Southwest: main-road frontage, asphalt back alley, second housing row.
            for(int i=0;i<3;i++)Place("02-buildings-"+(i%2),"SW front "+i,6+i*7.2f,55.7f,7.1f,6.3f,4);
            for(int i=0;i<3;i++)Place("02-buildings-"+(i%2),"South front "+i,33+i*7.2f,55.7f,7.1f,6.3f,4);
            for(int i=0;i<7;i++)Place("02-buildings-"+(i%2),"South rear "+i,5.8f+i*7.2f,63.7f,7.1f,6.3f,3.9f);
            Place("01-shops-2","Hospital",67,59.5f,10,9,5.5f);
            Place("02-buildings-4","Workshop",76.5f,59.5f,9,8.1f,4.5f);
            // Concentrate activity around entrances; do not fill every empty patch.
            Place("05-props-10","Parked car",20.5f,30.8f,2.5f,2.1f,2.2f);
            Paint("asphalt",17,27,7,4.5f);
            for(float x=17.3f;x<24;x+=2.2f)PlanRect("Parking bay",x,27.3f,.07f,3.2f,new Color(.9f,.88f,.8f),-10000);
            Place("04-infrastructure-0","Shop utility pole",24,42.1f,.85f,.25f,.3f);
            Place("04-infrastructure-1","Library streetlight",54.7f,42.1f,1.15f,.3f,.3f);
            Place("04-infrastructure-1","Cafe streetlight",61.6f,42.1f,1.15f,.3f,.3f);
            Place("04-infrastructure-0","South utility pole",29.8f,55.7f,.85f,.25f,.3f);
            Place("04-infrastructure-3","Bus shelter",3.9f,42.1f,2.6f,2.2f,.55f);
            Place("05-props-4","Standalone vending machine",23.2f,40.5f,1, .8f,.35f);
            Place("05-props-1","Recycling bin",23.5f,41.9f,.55f,.45f,.35f);
            foreach(var p in new[]{new Vector2(10.5f,16.5f),new Vector2(44,16),new Vector2(64,28),new Vector2(80,29),new Vector2(83,13),new Vector2(79,17)})
                Place("03-greenery-3","Courtyard tree "+p,p.x,p.y,2.4f,.75f,.5f);
            Place("05-props-0","Campus bench",80,16.7f,2.1f,1.8f,.4f);
            Place("05-props-6","Cafe terrace",64,30,2.5f,2.1f,.8f);
            Place("03-greenery-7","Campus flower bed",80.5f,18.7f,3.4f,3,.4f);
            // Perimeter and property seams, made of small independent repeatable pieces.
            Fence(2,24,7);Fence(16,24,8);Fence(30,21.5f,8);Fence(46,21.5f,9);
            for(int i=0;i<15;i++)
            {
                float y=3.5f+i*4.05f;
                Place("08-nature-"+(i%4),"West boundary tree "+i,.5f+(i%3)*.25f,y,3.5f+(i%2)*.4f,.5f,.4f);
                Place("08-nature-"+((i+2)%4),"East boundary tree "+i,87.4f-(i%2)*.3f,y+.7f,3.7f,.5f,.4f);
            }
            for(int i=0;i<21;i++)Place("08-nature-"+(i%4),"North boundary tree "+i,3+i*4.1f,2.3f+(i%3)*.2f,3.7f,.5f,.4f);
            foreach(var p in new[]{new Vector2(32,30.4f),new Vector2(38.5f,30.6f),new Vector2(45,30.5f),new Vector2(53.4f,30.5f),new Vector2(37,24),new Vector2(49.5f,24)})
                Place("08-nature-5","Park shrub "+p,p.x,p.y,1.7f,0,0);
            foreach(var p in new[]{new Vector2(31,24.5f),new Vector2(54.4f,29),new Vector2(45.5f,23.5f)})
                Place("08-nature-6","Park small flowers "+p,p.x,p.y,1.1f,0,0);
            Debug.Log($"[CAP-MODULAR] {GroundSurfaceCount} continuous ground surfaces, {ModularObjects.Count} independent transparent sprites; NO overview texture");
        }

        private void Paint(string key,float x,float y,float w,float h)
        {
            Color color=key=="grass"?new Color(.52f,.65f,.32f):
                key=="asphalt"?new Color(.48f,.52f,.55f):new Color(.79f,.77f,.69f);
            PlanRect("Continuous "+key,x,y,w,h,color,-12000+GroundSurfaceCount);
            GroundSurfaceCount++;
        }
        private void Place(string key,string name,float x,float foot,float width,float solidWidth,float solidDepth)
        {
            var s=sprites[key];var sr=Make(name);sr.sprite=s;
            float scale=width/s.bounds.size.x;sr.transform.localScale=Vector3.one*scale;sr.transform.position=PlanWorld(x,foot);
            sr.sortingOrder=Depth(sr.transform.position.y);ModularObjects.Add(name,sr);
            if(solidWidth>0) Solid(sr,x-44,32-foot+.04f,solidWidth,solidDepth);
        }
        private void Curb(float x,float y,float width)
        {
            PlanRect("Continuous kerb shadow",x,y-.04f,width,.18f,new Color(.39f,.45f,.46f),-10020);
            PlanRect("Continuous kerb cap",x,y-.12f,width,.09f,new Color(.77f,.78f,.7f),-10019);
            PlanRect("Continuous road edge",x,y+.14f,width,.08f,new Color(.87f,.69f,.32f),-10018);
        }
        private void Fence(float x,float y,float length)
        {
            for(float p=x+1;p<x+length;p+=2)Place("04-infrastructure-6","Fence "+x+","+y+","+p,p,y,2,1.85f,.2f);
        }
    }
}
