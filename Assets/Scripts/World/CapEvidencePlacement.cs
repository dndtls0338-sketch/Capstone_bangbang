using System.Collections.Generic;
using UnityEngine;

namespace Cap.Multiplayer
{
    public static class CapEvidencePlacement
    {
        private const float CellSize=.5f;
        // Larger than the movement collider: keep the whole avatar clear of buildings.
        private const float Clearance=CapWarmTown.ReferencePlayerHeight/2+.15f;

        public static bool CanWalkStraight(CapWarmTown town,Vector2 from,Vector2 to)
        {
            if(town==null)return false;
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(from,to)/.1f));
            for(int i=0;i<=steps;i++)
            {
                Vector2 p=Vector2.Lerp(from,to,(float)i/steps);
                if(town.Blocked(new Vector2(p.x-CapWarmTown.OffsetX,p.y)))return false;
            }
            return true;
        }

        // Flood fill from the actual outdoor entrance. A random empty rectangle alone
        // could select an enclosed courtyard that players cannot enter.
        public static List<Vector2> ReachablePositions(CapWarmTown town,float evidenceSize=0)
        {
            var result=new List<Vector2>();
            if(town==null)return result;
            Rect bounds=CapWarmTown.Bounds;
            float clearance=Mathf.Max(Clearance,evidenceSize/2+.15f);
            int width=Mathf.CeilToInt(bounds.width/CellSize),height=Mathf.CeilToInt(bounds.height/CellSize);
            var clear=new bool[width*height];var visited=new bool[clear.Length];
            var points=new Vector2[clear.Length];
            Vector2 entrance=town.StudentDoor+Vector3.down*.6f;
            int seed=-1;float nearest=float.MaxValue;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int index=y*width+x;
                var p=new Vector2(bounds.xMin+(x+.5f)*CellSize,bounds.yMin+(y+.5f)*CellSize);
                points[index]=p+Vector2.right*CapWarmTown.OffsetX;
                // Inflate by half a cell as well, so every edge connecting grid cells is clear.
                clear[index]=!town.Blocked(p,clearance+CellSize/2);
                float distance=(points[index]-entrance).sqrMagnitude;
                if(clear[index] && distance<nearest && distance<36 && CanWalkStraight(town,entrance,points[index]))
                {nearest=distance;seed=index;}
            }
            if(seed<0)return result;
            var queue=new Queue<int>();queue.Enqueue(seed);visited[seed]=true;
            while(queue.Count>0)
            {
                int index=queue.Dequeue(),x=index%width,y=index/width;
                Vector2 p=points[index];
                bool nearInteraction=Vector2.Distance(p,town.StudentDoor)<5 ||
                    (CapPoliceNpc.Instance!=null && Vector2.Distance(p,CapPoliceNpc.Instance.transform.position)<5);
                if(!nearInteraction)result.Add(p);
                if(x>0)Visit(index-1);if(x+1<width)Visit(index+1);
                if(y>0)Visit(index-width);if(y+1<height)Visit(index+width);
            }
            return result;

            void Visit(int index)
            {
                if(!clear[index] || visited[index])return;
                visited[index]=true;queue.Enqueue(index);
            }
        }

        public static List<Vector2> Choose(CapWarmTown town,int count,float spacing,float evidenceSize=0)
        {
            var candidates=ReachablePositions(town,evidenceSize);var selected=new List<Vector2>();
            count=Mathf.Clamp(count,1,64);spacing=Mathf.Max(2,spacing);
            // Sample without replacement. A finite candidate list also makes a crowded map safe.
            for(int i=candidates.Count-1;i>=0 && selected.Count<count;i--)
            {
                int index=Random.Range(0,i+1);Vector2 p=candidates[index];candidates[index]=candidates[i];
                bool separated=true;
                foreach(Vector2 existing in selected)if((existing-p).sqrMagnitude<spacing*spacing){separated=false;break;}
                if(separated)selected.Add(p);
            }
            return selected;
        }
    }
}
