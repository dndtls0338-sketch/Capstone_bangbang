using Unity.Netcode;
using UnityEngine;

namespace Cap.Multiplayer
{
    // Runs after the local environment/camera update. State changes are covered before rendering.
    [DefaultExecutionOrder(1000)]
    public sealed class CapLoadingScreen : MonoBehaviour
    {
        public static CapLoadingScreen Instance { get; private set; }
        public const float MinimumVisibleSeconds=.75f;
        public const float FadeSeconds=.3f;
        public static bool Blocking=>Instance!=null && Instance.active;
        public float Alpha { get; private set; }
        public string Caption { get; private set; }
        public int TransitionCount { get; private set; }
        private bool active,wasBusy;
        private int lastPlace;
        private float began,readySince=-1;
        private Font font;
        private GUIStyle heading,detail;
        private void Awake(){Instance=this;}
        public static void Show(string caption){if(Instance!=null)Instance.Begin(caption);}
        private void Begin(string caption)
        {
            Caption=caption;active=true;Alpha=1;began=Time.unscaledTime;readySince=-1;TransitionCount++;
            CapVoiceChat.Instance?.StopDeviceTest();
        }
        private void LateUpdate()
        {
            var session=CapRelaySession.Instance;
            var local=NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>();
            bool connected=session!=null && session.Connected;
            bool busy=session!=null && session.Busy;
            int place=connected && local!=null && local.IsSpawned ? local.InTown.Value ? local.InMeetingRoom.Value?3:2 : 1 : 0;
            if(place!=lastPlace)
            {
                lastPlace=place;
                Begin(place==3?"회의실로 이동 중":place==2?"마을로 이동 중":place==1?"대기실로 이동 중":"시작 화면으로 이동 중");
            }
            else if(busy && !wasBusy && !active)Begin("연결을 준비하는 중");
            wasBusy=busy;
            if(!active)return;
            bool ready=!busy && (!connected || (place!=0 && CapWarmTown.Instance!=null && CapWarmTown.Instance.InTown==local.InTown.Value));
            if(!ready){readySince=-1;Alpha=1;return;}
            if(readySince<0)readySince=Time.unscaledTime;
            float revealAt=Mathf.Max(began+MinimumVisibleSeconds,readySince+.15f);
            Alpha=1-Mathf.Clamp01((Time.unscaledTime-revealAt)/FadeSeconds);
            if(Alpha<=0){active=false;Alpha=0;}
        }
        private void OnGUI()
        {
            if(!active)return;
            if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},30);
            if(heading==null){heading=new GUIStyle(GUI.skin.label){font=font,fontSize=30,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};detail=new GUIStyle(heading){fontSize=17,normal={textColor=new Color(.65f,.73f,.83f)}};}
            var matrix=GUI.matrix;var color=GUI.color;var oldDepth=GUI.depth;bool enabled=GUI.enabled;
            try
            {
                GUI.matrix=Matrix4x4.identity;GUI.depth=-20000;GUI.enabled=true;
                GUI.color=new Color(.045f,.065f,.10f,Alpha);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
                float scale=Mathf.Clamp(Screen.height/800f,.5f,2f),middle=Screen.height*.5f;
                heading.fontSize=Mathf.RoundToInt(30*scale);detail.fontSize=Mathf.RoundToInt(17*scale);
                GUI.color=new Color(1,1,1,Alpha);
                GUI.Label(new Rect(0,middle-70*scale,Screen.width,48*scale),Caption,heading);
                GUI.Label(new Rect(0,middle+47*scale,Screen.width,35*scale),"잠시만 기다려 주세요",detail);
                // An indeterminate animation, not a fabricated percentage of loaded assets.
                int lit=Mathf.FloorToInt(Time.unscaledTime*4)%3;
                for(int i=0;i<3;i++)
                {
                    GUI.color=i==lit?new Color(.98f,.75f,.37f,Alpha):new Color(.28f,.35f,.44f,Alpha);
                    GUI.DrawTexture(new Rect(Screen.width/2+(i-1)*26*scale-6*scale,middle+8*scale,12*scale,12*scale),Texture2D.whiteTexture);
                }
                if(Event.current.isMouse || Event.current.isKey || Event.current.type==EventType.ScrollWheel)Event.current.Use();
            }
            finally{GUI.matrix=matrix;GUI.color=color;GUI.depth=oldDepth;GUI.enabled=enabled;}
        }
        private void OnDestroy(){if(Instance==this)Instance=null;if(font!=null)Destroy(font);}
    }
}
