using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed partial class CapNetworkPlayer
    {
        // Same dimensions as the visible prototype rectangle, plus a small separation gap.
        public static readonly Vector2 BodySize = new Vector2(CapWarmTown.ReferencePlayerHeight*.67f,CapWarmTown.ReferencePlayerHeight);
        private static readonly Vector2 Separation = BodySize+Vector2.one*.04f;

        public static bool BodiesOverlap(Vector3 a,Vector3 b) => Mathf.Abs(a.x-b.x)<Separation.x-.001f && Mathf.Abs(a.y-b.y)<Separation.y-.001f;
        private bool Occupied(Vector3 position,bool town,bool room,CapNetworkPlayer[] players)
        {
            foreach(var other in players)
                if(other!=this && other.IsSpawned && other.NetworkManager==NetworkManager && other.InTown.Value==town && other.InMeetingRoom.Value==room && BodiesOverlap(position,other.transform.position))return true;
            return false;
        }
        private static bool TerrainBlocked(Vector3 position,bool town,bool room)
        {
            if(!town)return position.x < -7.2f || position.x > 7.2f || position.y < -3.8f || position.y > 2.4f;
            if(room)return CapWarmTown.MeetingBlocked((Vector2)(position-CapWarmTown.MeetingCenter));
            return CapWarmTown.Instance==null || CapWarmTown.Instance.Blocked(new Vector2(position.x-CapWarmTown.OffsetX,position.y));
        }
        private bool TryFreePosition(Vector3 preferred,bool town,bool room,out Vector3 position)
        {
            var players=FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None);
            for(int ring=0;ring<=8;ring++)
            {
                bool found=false;float nearest=float.MaxValue;position=preferred;
                for(int x=-ring;x<=ring;x++)for(int y=-ring;y<=ring;y++)
                {
                    if(Mathf.Max(Mathf.Abs(x),Mathf.Abs(y))!=ring)continue;
                    var offset=new Vector3(x*(Separation.x+.08f),y*(Separation.y+.08f),0);
                    var candidate=preferred+offset;
                    if(offset.sqrMagnitude>=nearest || TerrainBlocked(candidate,town,room) || Occupied(candidate,town,room,players))continue;
                    found=true;nearest=offset.sqrMagnitude;position=candidate;
                }
                if(found)return true;
            }
            position=preferred;return false;
        }
        private Vector3 EnvironmentStep(Vector3 position,Vector2 delta)
        {
            var town=CapWarmTown.Instance;
            if(InTown.Value && town!=null)return InMeetingRoom.Value ? town.MoveInMeetingRoom(position,delta) : town.Move(position,delta);
            position+=(Vector3)delta;position.x=Mathf.Clamp(position.x,-7.2f,7.2f);position.y=Mathf.Clamp(position.y,-3.8f,2.4f);position.z=0;return position;
        }
        private Vector3 ClampAgainstPlayers(Vector3 from,Vector3 to,int axis,CapNetworkPlayer[] players)
        {
            int perpendicular=1-axis;float travel=to[axis]-from[axis];
            if(Mathf.Abs(travel)<.000001f)return from;
            foreach(var other in players)
            {
                if(other==this || !other.IsSpawned || other.NetworkManager!=NetworkManager || !SameSpace(other))continue;
                var obstacle=other.transform.position;
                if(Mathf.Abs(from[perpendicular]-obstacle[perpendicular])>=Separation[perpendicular])continue;
                float before=from[axis]-obstacle[axis];
                if(travel>0 && before<=-Separation[axis]+.001f)to[axis]=Mathf.Min(to[axis],obstacle[axis]-Separation[axis]);
                else if(travel<0 && before>=Separation[axis]-.001f)to[axis]=Mathf.Max(to[axis],obstacle[axis]+Separation[axis]);
                // Recovery for externally placed/legacy overlapping players: allow only movement out.
                else if(Mathf.Abs(before)<Separation[axis] && before*travel<0)to[axis]=from[axis];
            }
            return to;
        }
        private Vector3 MoveWithPlayers(Vector3 position,Vector2 delta)
        {
            var players=FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None);
            // Substeps prevent a fast bicycle or large physics tick from skipping another player.
            int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.12f));delta/=steps;
            for(int i=0;i<steps;i++)
            {
                var next=EnvironmentStep(position,new Vector2(delta.x,0));position=ClampAgainstPlayers(position,next,0,players);
                next=EnvironmentStep(position,new Vector2(0,delta.y));position=ClampAgainstPlayers(position,next,1,players);
            }
            return position;
        }
    }
}
