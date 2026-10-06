using UnityEngine;
namespace Cap.Multiplayer
{
    public sealed partial class CapNetworkPlayer
    {
        // Players may share positions; terrain and room boundaries still block movement.
        private static bool TerrainBlocked(Vector3 position,bool town,bool room)
        {
            if(!town)return position.x < -7.2f || position.x > 7.2f || position.y < -3.8f || position.y > 2.4f;
            if(room)return CapWarmTown.MeetingBlocked((Vector2)(position-CapWarmTown.MeetingCenter));
            return CapWarmTown.Instance==null || CapWarmTown.Instance.Blocked(new Vector2(position.x-CapWarmTown.OffsetX,position.y));
        }
        private bool TryFreePosition(Vector3 preferred,bool town,bool room,out Vector3 position)
        {
            for(int ring=0;ring<=8;ring++)
            {
                bool found=false;float nearest=float.MaxValue;position=preferred;
                for(int x=-ring;x<=ring;x++)for(int y=-ring;y<=ring;y++)
                {
                    if(Mathf.Max(Mathf.Abs(x),Mathf.Abs(y))!=ring)continue;
                    var offset=new Vector3(x*1.3f,y*1.9f,0);
                    var candidate=preferred+offset;
                    if(offset.sqrMagnitude>=nearest || TerrainBlocked(candidate,town,room))continue;
                    found=true;nearest=offset.sqrMagnitude;position=candidate;
                }
                if(found)return true;
            }
            position=preferred;return false;
        }
        private Vector3 MoveInEnvironment(Vector3 position,Vector2 delta)
        {
            var town=CapWarmTown.Instance;
            if(InTown.Value && town!=null)return InMeetingRoom.Value ? town.MoveInMeetingRoom(position,delta) : town.Move(position,delta);
            position+=(Vector3)delta;position.x=Mathf.Clamp(position.x,-7.2f,7.2f);position.y=Mathf.Clamp(position.y,-3.8f,2.4f);position.z=0;return position;
        }
    }
}
