using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cap.Multiplayer
{
    // Start -> create/join -> lobby. Existing Relay connection code stays in CapRelaySession.
    public sealed class CapLobbyUI : MonoBehaviour
    {
        private enum Page { Home, Create, Join, Options }
        private Page page;
        private string code="",notice="";
        private bool wasConnected,joinAttempted;
        private float volume=1;
        private bool fullscreen;
        private Font font;
        private GUIStyle title,heading,body,muted,button,primary,field,center,small;
        private readonly List<Texture2D> textures=new List<Texture2D>();
        private static readonly Color Background=new Color(.055f,.075f,.105f);
        private static readonly Color Panel=new Color(.085f,.115f,.16f);
        private static readonly Color Accent=new Color(.96f,.73f,.36f);
        private const string VolumeKey="Cap.MasterVolume",FullscreenKey="Cap.Fullscreen";
        public string CurrentScreen => CapRelaySession.Instance!=null && CapRelaySession.Instance.Connected ? "Lobby" : page.ToString();
        public string Notice => notice;

        private void Awake()
        {
            volume=Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey,1));AudioListener.volume=volume;
            fullscreen=PlayerPrefs.GetInt(FullscreenKey,Screen.fullScreen?1:0)!=0;
            if(!Application.isEditor && PlayerPrefs.HasKey(FullscreenKey))
                Screen.fullScreenMode=fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
        }
        private void Update()
        {
            var connection=CapRelaySession.Instance;if(connection==null)return;
            if(connection.Connected)wasConnected=true;
            else if(wasConnected && !connection.Busy)
            {wasConnected=false;page=Page.Home;notice=connection.Status;}
            var keyboard=Keyboard.current;
            if(!Application.isFocused || keyboard==null || connection.Busy || connection.Connected)return;
            if(keyboard.escapeKey.wasPressedThisFrame && page!=Page.Home)BackToHome();
            else if(page==Page.Join && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))JoinFromMenu(code);
        }

        public void OpenJoin(){page=Page.Join;notice="";code="";joinAttempted=false;}
        public void OpenOptions(){page=Page.Options;notice="";}
        public void BackToHome(){SaveOptions();page=Page.Home;notice="";}
        public void HostFromMenu()
        {
            var session=CapRelaySession.Instance;if(session==null||session.Busy)return;
            page=Page.Create;notice="";session.CreateRoom();
        }
        public void JoinFromMenu(string enteredCode)
        {
            code=(enteredCode??"").Trim().ToUpperInvariant();
            if(code.Length==0){notice="친구에게 받은 참가 코드를 입력해 주세요.";return;}
            notice="";joinAttempted=true;CapRelaySession.Instance?.JoinRoom(code);
        }
        public void SetVolume(float value){volume=Mathf.Clamp01(value);AudioListener.volume=volume;}
        public void SaveOptions()
        {
            PlayerPrefs.SetFloat(VolumeKey,volume);
            if(!Application.isEditor)PlayerPrefs.SetInt(FullscreenKey,fullscreen?1:0);
            PlayerPrefs.Save();
        }
        public void ExitGame()
        {
            SaveOptions();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }

        private Texture2D Swatch(Color color)
        {
            var texture=new Texture2D(1,1){name="Menu color",hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixel(0,0,color);texture.Apply();textures.Add(texture);return texture;
        }
        private void PrepareStyles()
        {
            if(body!=null)return;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},24);
            body=new GUIStyle(GUI.skin.label){font=font,fontSize=20,wordWrap=true,normal={textColor=new Color(.9f,.93f,.96f)}};
            muted=new GUIStyle(body){fontSize=17,normal={textColor=new Color(.60f,.69f,.77f)}};
            small=new GUIStyle(muted){fontSize=14};
            heading=new GUIStyle(body){fontSize=32,fontStyle=FontStyle.Bold};
            title=new GUIStyle(heading){fontSize=72};
            center=new GUIStyle(body){alignment=TextAnchor.MiddleCenter};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=21,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(24,16,0,0),border=new RectOffset()};
            button.normal.background=Swatch(new Color(.15f,.20f,.27f));button.normal.textColor=Color.white;
            button.hover.background=Swatch(new Color(.21f,.29f,.38f));button.hover.textColor=Color.white;
            button.active.background=Swatch(new Color(.10f,.16f,.23f));button.active.textColor=Color.white;
            button.focused.background=button.hover.background;button.focused.textColor=Color.white;
            primary=new GUIStyle(button);primary.normal.background=Swatch(Accent);primary.normal.textColor=Background;
            primary.hover.background=Swatch(new Color(1,.83f,.51f));primary.hover.textColor=Background;
            primary.active.background=Swatch(new Color(.84f,.58f,.23f));primary.active.textColor=Background;
            primary.focused.background=primary.hover.background;primary.focused.textColor=Background;
            field=new GUIStyle(GUI.skin.textField){font=font,fontSize=30,padding=new RectOffset(18,18,12,12),alignment=TextAnchor.MiddleLeft};
        }
        private static void Fill(Rect rect,Color color)
        {var previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous;}

        private void OnGUI()
        {
            if(CapWarmTown.Instance!=null&&CapWarmTown.Instance.InTown)return;
            var local=NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if(local!=null&&local.GetComponent<CapNetworkPlayer>().InTown.Value)return;
            var connection=CapRelaySession.Instance;if(connection==null)return;
            PrepareStyles();
            var oldMatrix=GUI.matrix;var oldColor=GUI.color;bool oldEnabled=GUI.enabled;
            try
            {
                GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;
                bool connected=connection.Connected;
                if(!connected)Fill(new Rect(0,0,Screen.width,Screen.height),Background);
                float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
                GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2,0),Quaternion.identity,Vector3.one*scale);
                if(connected){DrawLobby(connection);return;}
                DrawBackdrop();
                switch(page)
                {
                    case Page.Home:DrawHome();break;
                    case Page.Create:DrawCreate(connection);break;
                    case Page.Join:DrawJoin(connection);break;
                    case Page.Options:DrawOptions();break;
                }
            }
            finally{GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.enabled=oldEnabled;}
        }
        private void DrawBackdrop()
        {
            Fill(new Rect(0,660,1280,140),new Color(.065f,.095f,.13f));
            for(int i=0;i<11;i++)
            {
                float height=70+(i%3)*28;
                Fill(new Rect(i*126-20,660-height,96,height),new Color(.08f,.12f,.17f));
                for(int row=0;row<2;row++)Fill(new Rect(i*126+2,674-height+row*24,12,12),new Color(.18f,.22f,.23f));
            }
            Fill(new Rect(64,68,32,4),Accent);
            GUI.Label(new Rect(108,55,500,32),"대학생 탐정단",muted);
            GUI.Label(new Rect(64,748,700,30),"탐색하고, 비교하고, 진실에 가까워지세요.",small);
        }
        private void DrawHome()
        {
            GUI.Label(new Rect(100,205,630,190),"대학생\n탐정단",title);
            Fill(new Rect(106,427,50,4),Accent);
            GUI.Label(new Rect(106,459,610,45),"뉴스 너머의 진실을 찾아서",heading);
            GUI.Label(new Rect(108,521,550,70),"함께 조사하고, 각자의 결론을 내리세요.",muted);
            Fill(new Rect(786,173,398,455),Panel);
            GUI.Label(new Rect(820,197,325,38),"조사를 시작할까요?",body);
            if(GUI.Button(new Rect(820,253,330,64),"방 만들기",primary))HostFromMenu();
            if(GUI.Button(new Rect(820,333,330,64),"방 참가하기",button))OpenJoin();
            if(GUI.Button(new Rect(820,413,330,64),"옵션",button))OpenOptions();
            if(GUI.Button(new Rect(820,493,330,64),"게임 종료",button))ExitGame();
            GUI.Label(new Rect(822,579,330,30),"친구와 함께 · 최대 4명",small);
            if(!string.IsNullOrEmpty(notice))GUI.Label(new Rect(820,645,340,83),notice,small);
        }
        private void Card(string name,string subtitle)
        {
            Fill(new Rect(350,154,580,532),Panel);
            Fill(new Rect(350,154,580,4),Accent);
            GUI.Label(new Rect(390,188,500,55),name,heading);
            GUI.Label(new Rect(392,250,495,70),subtitle,muted);
        }
        private void DrawCreate(CapRelaySession connection)
        {
            Card("방 만들기",connection.Busy?"친구들과 만날 공간을 준비하고 있어요.":"방을 만들면 참가 코드를 친구에게 보내 주세요.");
            GUI.Label(new Rect(392,343,490,170),connection.Busy?"방을 만드는 중…":connection.Status,body);
            GUI.enabled=!connection.Busy;
            if(GUI.Button(new Rect(390,536,500,58),connection.Busy?"연결 중…":"다시 시도",primary))HostFromMenu();
            if(GUI.Button(new Rect(390,608,500,48),"뒤로",button))BackToHome();
        }
        private void DrawJoin(CapRelaySession connection)
        {
            Card("방 참가하기","친구에게 받은 참가 코드를 입력하세요.");
            GUI.enabled=!connection.Busy;
            GUI.SetNextControlName("JoinCode");
            code=GUI.TextField(new Rect(390,330,500,64),code,12,field).ToUpperInvariant();
            GUI.Label(new Rect(392,415,495,101),!string.IsNullOrEmpty(notice)?notice:connection.Busy?"방에 연결하는 중…":joinAttempted?connection.Status:"코드를 입력하고 참가하기를 눌러 주세요.",muted);
            if(GUI.Button(new Rect(390,536,500,58),connection.Busy?"연결 중…":"참가하기",primary))JoinFromMenu(code);
            if(GUI.Button(new Rect(390,608,500,48),"뒤로",button))BackToHome();
        }
        private void DrawOptions()
        {
            Card("옵션","돌아가면 변경한 설정이 저장됩니다.");
            GUI.Label(new Rect(390,325,370,34),"전체 음량",body);
            GUI.Label(new Rect(792,325,100,34),Mathf.RoundToInt(volume*100)+"%",center);
            float next=GUI.HorizontalSlider(new Rect(392,381,493,28),volume,0,1);
            if(Mathf.Abs(next-volume)>.001f)SetVolume(next);
            GUI.Label(new Rect(390,438,265,34),"화면 모드",body);
            GUI.enabled=!Application.isEditor;
            if(GUI.Button(new Rect(665,428,225,52),fullscreen?"전체 화면":"창 모드",button))
            {fullscreen=!fullscreen;Screen.fullScreenMode=fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;}
            GUI.enabled=true;
            if(Application.isEditor)GUI.Label(new Rect(392,493,490,40),"화면 모드는 빌드한 게임에서 변경할 수 있습니다.",small);
            GUI.Label(new Rect(392,542,490,50),"WASD / 방향키 이동 · M 지도 · Esc 지도 닫기",small);
            if(GUI.Button(new Rect(390,608,500,48),"저장하고 돌아가기",primary))BackToHome();
        }
        private void DrawLobby(CapRelaySession connection)
        {
            Fill(new Rect(36,40,490,702),Panel);
            GUI.Label(new Rect(68,68,420,52),"탐정단 대기실",heading);
            bool host=NetworkManager.Singleton.IsHost;
            var players=FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None);
            GUI.Label(new Rect(70,125,420,34),$"{(host?"방장":"참가자")} · {players.Length} / 4명",muted);
            string roomCode=string.IsNullOrEmpty(connection.JoinCode)?"로컬 테스트":connection.JoinCode;
            GUI.Label(new Rect(70,179,420,35),"참가 코드",small);
            GUI.Label(new Rect(70,212,410,48),roomCode,heading);
            if(!string.IsNullOrEmpty(connection.JoinCode)&&GUI.Button(new Rect(70,278,420,46),"참가 코드 복사",button))GUIUtility.systemCopyBuffer=connection.JoinCode;
            for(int i=0;i<4;i++)
            {
                float y=345+i*47;Fill(new Rect(70,y,420,39),new Color(.12f,.16f,.21f));
                if(i<players.Length)
                {var p=players[i];Fill(new Rect(83,y+10,18,18),CapWarmTown.PlayerColor(p.OwnerClientId));GUI.Label(new Rect(116,y+4,350,32),"P"+(p.OwnerClientId%4+1)+(p.IsOwner?" · 나":""),body);}
                else GUI.Label(new Rect(116,y+4,350,32),"참가 대기 중",muted);
            }
            GUI.enabled=!connection.Busy&&host&&CapWarmTown.CanStart;
            if(GUI.Button(new Rect(70,553,420,58),host?"게임 시작":"방장의 시작을 기다리는 중",primary))CapWarmTown.StartForAll(true);
            GUI.enabled=!connection.Busy;
            if(GUI.Button(new Rect(70,627,420,48),"방 나가기",button))connection.LeaveRoom();
            GUI.Label(new Rect(72,697,418,30),"혼자 시작하거나 친구가 들어오기를 기다릴 수 있어요.",small);
        }
        private void OnDestroy(){if(font!=null)Destroy(font);foreach(var texture in textures)if(texture!=null)Destroy(texture);}
    }
}
