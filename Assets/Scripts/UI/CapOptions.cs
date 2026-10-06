using System;
using UnityEngine;
using UnityEngine.InputSystem;


namespace Cap.Multiplayer
{
    // Available from both the title menu and live game; opening settings pauses only local input.
    [DefaultExecutionOrder(-100)]
    public sealed class CapOptions : MonoBehaviour
    {
        public static CapOptions Instance { get; private set; }
        public static bool IsOpen => Instance!=null && Instance.open;
        private bool open;
        private int tab, capture=-1;
        private int deviceDropdown=-1; // 0: microphone, 1: speakers
        private Vector2 deviceScroll;
        private float dropdownTop;


        private string message="";
        private Action onClose;
        private Font font;
        private GUIStyle heading, text, small, button, deviceButton;
        public void Open(Action closed=null){if(CapLoadingScreen.Blocking)return;open=true;capture=-1;deviceDropdown=-1;message="";onClose=closed;CapVoiceChat.Instance?.PrepareDevices();}
        public void Close(){CapVoiceChat.Instance?.StopDeviceTest();open=false;capture=-1;deviceDropdown=-1;PlayerPrefs.Save();var callback=onClose;onClose=null;callback?.Invoke();}
        public void ShowAudioTest(){Open();SelectTab(3);}
        public void SelectTab(int index){CapVoiceChat.Instance?.StopDeviceTest();tab=Mathf.Clamp(index,0,3);capture=-1;deviceDropdown=-1;message="";}
        private void Awake(){Instance=this;CapControls.Load();}
        private void Update()
        {
            if(CapLoadingScreen.Blocking)return;
            var keyboard=Keyboard.current;if(!Application.isFocused || keyboard==null)return;
            if(capture>=0)
            {
                if(keyboard.escapeKey.wasPressedThisFrame){CapControls.ConsumeEscape();capture=-1;message="키 변경을 취소했습니다.";return;}
                foreach(var key in keyboard.allKeys)
                    if(key.wasPressedThisFrame){if(CapControls.Bind((CapAction)capture,key.keyCode,out message))capture=-1;break;}
                return;
            }
            if(!keyboard.escapeKey.wasPressedThisFrame)return;
            if(CapChat.IsTyping){CapChat.Instance.CancelInput();CapControls.ConsumeEscape();return;}
            if(open){CapControls.ConsumeEscape();if(deviceDropdown>=0)deviceDropdown=-1;else Close();return;}
            // Dismiss an active dialog or map first; the next Esc opens settings.
            if(CapPoliceNpc.ModalOpen || CapLobbyUI.ProfileEditing || (CapWarmTown.Instance!=null && CapWarmTown.Instance.Overview))return;
            CapControls.ConsumeEscape();Open();
        }
        private void Styles()
        {
            if(text!=null)return;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},20);
            text=new GUIStyle(GUI.skin.label){font=font,fontSize=20,wordWrap=true,normal={textColor=new Color(.90f,.94f,1)}};
            small=new GUIStyle(text){fontSize=16};heading=new GUIStyle(text){fontSize=34,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=19,wordWrap=true};
            deviceButton=new GUIStyle(button){alignment=TextAnchor.MiddleLeft,wordWrap=false,clipping=TextClipping.Clip,padding=new RectOffset(14,32,0,0)};
        }
        private static void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        private void OnGUI()
        {
            if(CapLoadingScreen.Blocking)return;
            Styles();var old=GUI.matrix;int depth=GUI.depth;var color=GUI.color;bool enabled=GUI.enabled;
            try
            {
                GUI.depth=-1000;GUI.color=Color.white;GUI.enabled=true;
                float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
                GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2),Quaternion.identity,Vector3.one*scale);
                if(!open)
                {

                    if(CapRelaySession.Instance==null || !CapRelaySession.Instance.Connected)return;
                    DrawMicrophoneIcon(scale);
                    return;
                }
                Fill(new Rect(-2000,-2000,6000,6000),new Color(.025f,.04f,.065f,.98f));
                Fill(new Rect(190,45,900,710),new Color(.08f,.115f,.16f));
                GUI.Label(new Rect(225,70,800,55),"옵션",heading);
                GUI.Label(new Rect(227,128,800,35),"설정은 자동 저장됩니다. 게임 중에도 Esc로 열 수 있습니다.",small);
                // A dropdown owns the pointer until it closes; clicks cannot pass through it.
                GUI.enabled=deviceDropdown<0;
                for(int i=0;i<4;i++)if(GUI.Button(new Rect(225+i*210,174,200,45),(tab==i?"● ":"")+new[]{"화면 / 소리","조작키 변경","음성 채팅","장치 테스트"}[i],button))SelectTab(i);
                if(tab==0)DrawGeneral();else if(tab==1)DrawControls();else if(tab==2)DrawVoice();else DrawAudioTest();
                GUI.Label(new Rect(225,633,820,43),message,small);
                bool connected=CapRelaySession.Instance!=null && CapRelaySession.Instance.Connected;
                if(GUI.Button(new Rect(225,690,connected?540:830,42),capture>=0?"키 선택 취소":"저장하고 닫기 · Esc",button)){if(capture>=0)capture=-1;else Close();}
                if(connected)
                {
                    GUI.enabled=GUI.enabled && !CapRelaySession.Instance.Busy;
                    if(GUI.Button(new Rect(780,690,275,42),"방 나가기",button))LeaveCurrentRoom();
                    if(Unity.Netcode.NetworkManager.Singleton.IsHost)
                        GUI.Label(new Rect(225,669,825,22),"방장이 나가면 방이 종료됩니다.",new GUIStyle(small){fontSize=14});
                }
                GUI.enabled=true;
                if(deviceDropdown>=0)DrawDeviceDropdown();
            }
            finally{GUI.matrix=old;GUI.depth=depth;GUI.color=color;GUI.enabled=enabled;}
        }
        public void LeaveCurrentRoom()
        {
            if(CapRelaySession.Instance==null || CapRelaySession.Instance.Busy)return;
            CapChat.Instance?.CancelInput();Close();CapRelaySession.Instance.LeaveRoom();
        }
        private void DrawMicrophoneIcon(float scale)
        {
            GUI.matrix=Matrix4x4.identity;
            var area=new Rect(Screen.width-76*scale,Screen.height-76*scale,56*scale,56*scale);
            var voice=CapVoiceChat.Instance;
            bool muted=voice==null || !voice.VoiceEnabled || voice.Muted;
            Color tint=muted?new Color(1,.38f,.38f):voice.Transmitting?new Color(.35f,.95f,.6f):new Color(.7f,.76f,.83f);
            string status=muted?"마이크 음소거":!voice.Ready?"음성 연결 대기":voice.Transmitting?"마이크 송출 중":voice.PushToTalk?"눌러서 말하기 · "+CapControls.Label(CapAction.PushToTalk):"마이크 송출 대기";
            Fill(area,new Color(.04f,.07f,.11f,.95f));
            if(voice!=null && GUI.Button(area,new GUIContent("",status+" · 클릭: 음소거 전환"),GUIStyle.none))voice.SetMuted(!voice.Muted);
            GUI.matrix=Matrix4x4.TRS(new Vector3(area.x,area.y),Quaternion.identity,Vector3.one*scale);
            Fill(new Rect(23,10,10,22),tint);Fill(new Rect(21,12,14,18),tint);
            Fill(new Rect(17,24,3,10),tint);Fill(new Rect(36,24,3,10),tint);
            Fill(new Rect(20,34,16,3),tint);Fill(new Rect(27,37,3,7),tint);Fill(new Rect(21,44,15,3),tint);
            if(muted)
            {
                var matrix=GUI.matrix;GUIUtility.RotateAroundPivot(-45,new Vector2(28,28));
                Fill(new Rect(4,26,48,4),tint);GUI.matrix=matrix;
            }
        }
        private void DrawGeneral()
        {
            GUI.Label(new Rect(225,264,830,38),$"전체 음량   {Mathf.RoundToInt(AudioListener.volume*100)}%",text);
            float volume=GUI.HorizontalSlider(new Rect(228,324,822,30),AudioListener.volume,0,1);
            if(Mathf.Abs(volume-AudioListener.volume)>.001f){AudioListener.volume=volume;PlayerPrefs.SetFloat("Cap.MasterVolume",volume);}
            GUI.Label(new Rect(225,388,350,38),"화면 모드",text);
            GUI.enabled=!Application.isEditor;
            if(GUI.Button(new Rect(570,381,485,48),Screen.fullScreen?"전체 화면":"창 모드",button))
            {bool full=!Screen.fullScreen;Screen.fullScreenMode=full?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;PlayerPrefs.SetInt("Cap.Fullscreen",full?1:0);}
            GUI.enabled=true;
            GUI.Label(new Rect(225,467,825,100),"전체 음량은 게임 소리와 음성에 모두 적용됩니다.\n화면 모드는 빌드한 게임에서 변경할 수 있습니다.\nEsc: 옵션 / 열린 창 닫기 · Enter: 일반 채팅 (고정 키)",small);
        }
        private void DrawControls()
        {
            for(int i=0;i<CapControls.Names.Length;i++)
            {
                int col=i/5,row=i%5;float x=225+col*425,y=242+row*60;
                GUI.Label(new Rect(x,y+9,240,40),CapControls.Names[i],text);
                if(GUI.Button(new Rect(x+245,y,150,50),capture==i?"키를 누르세요":CapControls.Label((CapAction)i),button)){capture=i;message="원하는 키를 누르세요. 이미 사용 중인 키는 지정할 수 없습니다. Esc: 취소";}
            }
            if(GUI.Button(new Rect(225,560,830,44),"기본 조작키로 초기화",button)){capture=-1;CapControls.Reset();message="WASD / M / B / V / N / E로 초기화했습니다.";}
        }
        private void DrawVoice()
        {
            var voice=CapVoiceChat.Instance;if(voice==null)return;
            if(GUI.Button(new Rect(225,239,403,43),voice.VoiceEnabled?"음성 채팅: 켜짐":"음성 채팅: 꺼짐",button))voice.SetEnabled(!voice.VoiceEnabled);
            if(GUI.Button(new Rect(649,239,406,43),voice.Muted?"마이크: 음소거":"마이크: 사용",button))voice.SetMuted(!voice.Muted);
            DrawVoiceMode(new Rect(225,293,194,43),"항상 켜짐",!voice.PushToTalk,false,voice);
            DrawVoiceMode(new Rect(429,293,199,43),"눌러서 말하기",voice.PushToTalk,true,voice);
            if(GUI.Button(new Rect(649,293,406,43),"음성 서버 다시 연결",button))voice.Retry();
            GUI.Label(new Rect(225,350,680,34),$"친구 목소리 음량   {Mathf.RoundToInt(voice.VoiceVolume*100)}%"+(voice.VoiceVolume>1?" · 증폭 중":""),text);
            if(GUI.Button(new Rect(917,348,138,34),"100% 복원",button))voice.SetVolume(1);
            float volume=GUI.HorizontalSlider(new Rect(228,396,822,25),voice.VoiceVolume,0,CapVoiceRange.MaximumVoiceVolume);
            if(Mathf.Abs(volume-voice.VoiceVolume)>.001f)voice.SetVolume(volume);
            DrawDeviceSelector(voice,true,432);
            DrawDeviceSelector(voice,false,488);
            string modeHint=voice.PushToTalk?$"선택: 눌러서 말하기 — {CapControls.Label(CapAction.PushToTalk)} 키를 누르는 동안만 송출합니다.":"선택: 항상 켜짐 — 키를 누르지 않아도 마이크를 송출합니다.";
            GUI.Label(new Rect(225,550,825,78),modeHint+"\n마이크 음소거 / 설정 중 / 게임 창을 벗어난 동안에는 송출하지 않습니다.\n마을: 화면 안에서 거리별 음량 · 대기실: 전체 대화",small);
            message=voice.Status;
        }
        private void DrawDeviceSelector(CapVoiceChat voice,bool input,float y)
        {
            bool enabled=GUI.enabled;GUI.enabled=enabled && voice.CanSelectDevices;
            string name=input?voice.InputName:voice.OutputName;
            if(GUI.Button(new Rect(225,y,830,46),new GUIContent((input?"마이크: ":"스피커: ")+name,name),deviceButton))
            {deviceDropdown=input?0:1;deviceScroll=Vector2.zero;dropdownTop=y+50;}
            GUI.Label(new Rect(1018,y+10,28,30),deviceDropdown==(input?0:1)?"▲":"▼",small);
            GUI.enabled=enabled;
        }
        private void DrawAudioTest()
        {
            var voice=CapVoiceChat.Instance;if(voice==null)return;
            DrawDeviceSelector(voice,true,242);
            bool enabled=GUI.enabled;GUI.enabled=enabled && voice.CanTestDevices;
            if(GUI.Button(new Rect(225,301,260,43),voice.TestingMicrophone?"■ 마이크 테스트 중지":"▶ 마이크 테스트 시작",button))voice.ToggleMicrophoneTest();
            GUI.enabled=enabled;
            GUI.Label(new Rect(505,308,550,35),voice.TestingMicrophone?"마이크에 말하면 아래 막대가 움직여요.":"버튼을 눌러 입력 소리를 확인하세요.",small);
            GUI.Label(new Rect(225,353,825,30),$"마이크 입력 크기  {Mathf.RoundToInt(voice.MicrophoneLevel*100)}%",text);
            var meter=new Rect(225,391,830,14);
            Fill(meter,new Color(.27f,.31f,.38f));
            Fill(new Rect(meter.x,meter.y,meter.width*voice.MicrophoneLevel,meter.height),voice.MicrophoneLevel>.9f?new Color(1,.65f,.25f):new Color(.25f,.85f,.64f));
            GUI.Label(new Rect(225,416,825,40),"입력 크기를 확인하는 막대입니다. 친구에게 전달됐다는 표시는 아닙니다.",small);
            DrawDeviceSelector(voice,false,465);
            GUI.enabled=enabled && voice.CanTestDevices;
            if(GUI.Button(new Rect(225,527,260,43),voice.TestingSpeaker?"■ 테스트 소리 중지":"▶ 스피커 소리 테스트",button))voice.ToggleSpeakerTest();
            GUI.enabled=enabled;
            GUI.Label(new Rect(505,531,550,43),"선택한 출력 장치로 짧은 소리를 재생합니다.",small);
            GUI.Label(new Rect(225,589,825,43),"내 PC에서만 테스트하며 친구에게 전송되지 않습니다.\n설정을 닫거나 장치를 변경하면 테스트가 종료됩니다.",small);
            message=voice.DeviceTestStatus;
        }
        private void DrawVoiceMode(Rect rect,string name,bool selected,bool pushToTalk,CapVoiceChat voice)
        {
            var old=GUI.backgroundColor;
            if(selected)GUI.backgroundColor=new Color(.45f,.8f,1f);
            if(GUI.Button(rect,(selected?"● ":"○ ")+name,button))voice.SetPushToTalk(pushToTalk);
            GUI.backgroundColor=old;
        }
        private void DrawDeviceDropdown()
        {
            var voice=CapVoiceChat.Instance;
            if(voice==null || !voice.CanSelectDevices){deviceDropdown=-1;return;}
            bool input=deviceDropdown==0;
            var devices=voice.GetDevices(input);
            float height=Mathf.Min(148,Mathf.Max(1,devices.Count)*38+8);
            var panel=new Rect(225,Mathf.Min(dropdownTop,682-height),830,height);
            var evt=Event.current;
            if(evt.type==EventType.MouseDown && !panel.Contains(evt.mousePosition))
            {deviceDropdown=-1;evt.Use();return;}
            Fill(panel,new Color(.30f,.40f,.51f));
            Fill(new Rect(panel.x+1,panel.y+1,panel.width-2,panel.height-2),new Color(.075f,.105f,.15f));
            if(devices.Count==0){GUI.Label(new Rect(panel.x+14,panel.y+8,panel.width-28,32),"사용 가능한 장치가 없습니다.",small);return;}
            deviceScroll=GUI.BeginScrollView(new Rect(panel.x+4,panel.y+4,panel.width-8,panel.height-8),deviceScroll,new Rect(0,0,panel.width-30,devices.Count*38),false,false);
            string selected=null;
            for(int i=0;i<devices.Count;i++)
            {
                var device=devices[i];
                if(GUI.Button(new Rect(0,i*38,panel.width-32,36),new GUIContent((device.Selected?"✓  ":"    ")+device.Name,device.Name),deviceButton))selected=device.Id;
            }
            GUI.EndScrollView();
            if(selected!=null){deviceDropdown=-1;voice.SelectDevice(input,selected);}
            // Consume scrolls and clicks in the empty part of the list as well.
            if(panel.Contains(evt.mousePosition) && (evt.type==EventType.MouseDown || evt.type==EventType.MouseUp || evt.type==EventType.ScrollWheel))evt.Use();
        }
        private void OnDestroy(){if(Instance==this)Instance=null;if(font!=null)Destroy(font);}
    }
}
