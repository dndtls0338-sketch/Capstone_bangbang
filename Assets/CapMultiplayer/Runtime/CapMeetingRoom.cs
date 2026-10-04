using Unity.Netcode;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed partial class CapWarmTown
    {
        // The interior is local geometry; the host replicates each player's room flag and position.
        public Vector3 StudentDoor { get; private set; }
        public static readonly Vector3 MeetingCenter = new Vector3(300,0,0);
        public static readonly Vector3 MeetingDoor = new Vector3(300,-9.5f,0);
        public static readonly Vector3 MeetingSpawn = new Vector3(300,-8.2f,0);
        public static readonly Rect MeetingBounds = new Rect(-15,-10,30,20);
        private static readonly Rect MeetingTable = new Rect(-4,-1.5f,8,3);
        private GameObject meetingRoot;
        public bool IsMeetingRoom => NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>()?.InMeetingRoom.Value==true;

        public bool NearMeetingDoor(CapNetworkPlayer player) => player!=null && player.IsSpawned && player.InTown.Value &&
            Vector2.Distance(player.transform.position,player.InMeetingRoom.Value ? MeetingDoor : StudentDoor)<=1.65f;

        private void RoomRect(string name,Vector2 center,Vector2 size,Color color,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(meetingRoot.transform,false);
            go.transform.position=MeetingCenter+(Vector3)center;go.transform.localScale=new Vector3(size.x,size.y,1);
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=white;sr.sharedMaterial=ArtMaterial;sr.color=color;sr.sortingOrder=order;
        }
        private void RoomLabel(string text,Vector2 position)
        {
            var go=new GameObject(text);go.transform.SetParent(meetingRoot.transform,false);go.transform.position=MeetingCenter+(Vector3)position;
            var tm=go.AddComponent<TextMesh>();tm.font=blockoutFont;tm.fontSize=48;tm.characterSize=.14f;
            tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=Color.white;tm.text=text;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=blockoutFont.material;renderer.sortingOrder=5000;
        }
        private void BuildMeetingRoom()
        {
            meetingRoot=new GameObject("Student union - detective club meeting room");
            RoomRect("Floor",Vector2.zero,new Vector2(30,20),new Color(.78f,.75f,.65f),-12000);
            var wall=new Color(.32f,.39f,.47f);
            RoomRect("North wall",new Vector2(0,10.4f),new Vector2(31,.8f),wall,-2000);
            RoomRect("West wall",new Vector2(-15.4f,0),new Vector2(.8f,21.6f),wall,-2000);
            RoomRect("East wall",new Vector2(15.4f,0),new Vector2(.8f,21.6f),wall,-2000);
            RoomRect("South wall left",new Vector2(-8.1f,-10.4f),new Vector2(14.6f,.8f),wall,-2000);
            RoomRect("South wall right",new Vector2(8.1f,-10.4f),new Vector2(14.6f,.8f),wall,-2000);
            RoomRect("Exit marker",new Vector2(0,-9.8f),new Vector2(1.6f,.4f),new Color(1,.85f,.43f),-1990);
            RoomRect("Conference table",Vector2.zero,new Vector2(8,3),new Color(.58f,.39f,.25f),-2000);
            for(int i=0;i<4;i++)RoomRect("Seat "+i,new Vector2(i%2==0?-2.4f:2.4f,i<2?2.3f:-2.3f),new Vector2(1,1),new Color(.37f,.46f,.65f),-2100);
            RoomRect("Notice board",new Vector2(0,9),new Vector2(9,.7f),new Color(.25f,.40f,.36f),-2000);
            RoomLabel("탐정 동아리 회의실",new Vector2(0,7.7f));
            RoomLabel("회의 테이블",Vector2.zero);
            RoomLabel("출구",new Vector2(0,-9.6f));
            meetingRoot.SetActive(false);
        }
        public static bool MeetingBlocked(Vector2 p,float radius=.23f) =>
            p.x<MeetingBounds.xMin+radius || p.x>MeetingBounds.xMax-radius || p.y<MeetingBounds.yMin+radius || p.y>MeetingBounds.yMax-radius ||
            (p.x>MeetingTable.xMin-radius && p.x<MeetingTable.xMax+radius && p.y>MeetingTable.yMin-radius && p.y<MeetingTable.yMax+radius);
        public Vector3 MoveInMeetingRoom(Vector3 world,Vector2 delta)
        {
            var p=(Vector2)(world-MeetingCenter);
            int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.12f));delta/=steps;
            for(int i=0;i<steps;i++)
            {
                var next=p+new Vector2(delta.x,0);if(!MeetingBlocked(next))p=next;
                next=p+new Vector2(0,delta.y);if(!MeetingBlocked(next))p=next;
            }
            return MeetingCenter+(Vector3)p;
        }
        private void DrawMeetingDoorHint()
        {
            var player=NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>();
            if(player==null || Overview || !NearMeetingDoor(player))return;
            var area=new Rect(Screen.width/2-225,Screen.height-95,450,52);
            var style=new GUIStyle(GUI.skin.box){font=font,fontSize=20,alignment=TextAnchor.MiddleCenter};
            GUI.Box(area,$"[{CapControls.Label(CapAction.Interact)}] "+(player.InMeetingRoom.Value?"마을로 나가기":"학생회관 회의실 들어가기"),style);
        }
    }
}
