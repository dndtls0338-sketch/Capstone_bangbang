using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Cap.Multiplayer
{
    // Room-wide text chat. Delivery is relayed by the host through the sender's player object.
    [DefaultExecutionOrder(-50)]
    public sealed class CapChat : MonoBehaviour
    {
        public static CapChat Instance { get; private set; }
        public static bool IsTyping => Instance!=null && Instance.typing;
        public static bool InputBlocked => IsTyping || (Instance!=null && Instance.inputFrame==Time.frameCount);
        public const int MaximumLength=160, MaximumHistory=100;
        private readonly List<string> messages=new List<string>();
        public IReadOnlyList<string> Messages => messages;
        private bool typing,focusInput,wasConnected;
        private int inputFrame=-1,openedFrame=-1,compositionFrame=-1;
        private float nextSend;
        private string draft="",composition="",notice="";
        private Vector2 scroll;
        private Font font;
        private GUIStyle text,field,button;
        private Keyboard imeKeyboard;
        private CapNetworkPlayer Local => NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>();
        private void Awake(){Instance=this;}

        // Keep Unicode (including Korean), remove control characters and limit packet size.
        public static string CleanMessage(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return "";
            var result=new StringBuilder();
            for(int i=0;i<value.Length && result.Length<MaximumLength;i++)
            {
                char c=value[i];
                if(char.IsHighSurrogate(c))
                {
                    if(i+1<value.Length && char.IsLowSurrogate(value[i+1]) && result.Length+2<=MaximumLength){result.Append(c);result.Append(value[++i]);}
                }
                else if(!char.IsLowSurrogate(c) && !char.IsControl(c))result.Append(c);
                else if(c=='\n' || c=='\r' || c=='\t')result.Append(' ');
            }
            return result.ToString().Trim();
        }
        public void Receive(string sender,string message)
        {
            messages.Add(sender+": "+message);
            if(messages.Count>MaximumHistory)messages.RemoveAt(0);
            scroll.y=float.MaxValue;
        }
        public bool Send(string value)
        {
            var player=Local;string clean=CleanMessage(value);
            if(player==null || !player.IsSpawned || clean.Length==0)return false;
            if(Time.unscaledTime<nextSend){notice="잠시 후 다시 전송하세요.";return false;}
            nextSend=Time.unscaledTime+.5f;
            player.SendChat(clean);notice="";return true;
        }
        public void BeginInput()
        {
            if(Local==null || !Local.IsSpawned || CapControls.Blocked)return;
            typing=true;focusInput=true;openedFrame=inputFrame=Time.frameCount;notice="";
            imeKeyboard=Keyboard.current;
            if(imeKeyboard!=null){imeKeyboard.onIMECompositionChange+=OnComposition;imeKeyboard.SetIMEEnabled(true);}
        }
        public void CancelInput()
        {
            if(typing)inputFrame=Time.frameCount;
            typing=false;draft="";composition="";focusInput=false;
            if(imeKeyboard!=null){imeKeyboard.onIMECompositionChange-=OnComposition;imeKeyboard.SetIMEEnabled(false);imeKeyboard=null;}
        }
        private void OnComposition(IMECompositionString value){composition=value.ToString();compositionFrame=Time.frameCount;}
        private void Update()
        {
            bool connected=Local!=null && Local.IsSpawned;
            if(!connected)
            {
                if(wasConnected){CancelInput();messages.Clear();nextSend=0;notice="";}
                wasConnected=false;return;
            }
            wasConnected=true;
            if(typing && (!Application.isFocused || CapOptions.IsOpen || CapLoadingScreen.Blocking || CapPoliceNpc.ModalOpen || CapCaseNews.ModalOpen || CapLobbyUI.ProfileEditing || Keyboard.current!=imeKeyboard)){CancelInput();return;}
            var k=Keyboard.current;
            if(!typing && Application.isFocused && k!=null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) && !CapControls.Blocked)BeginInput();
        }
        private void Styles()
        {
            if(text!=null)return;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},17);
            text=new GUIStyle(GUI.skin.label){font=font,fontSize=17,wordWrap=true,richText=false,normal={textColor=new Color(.91f,.95f,1)}};
            field=new GUIStyle(GUI.skin.textField){font=font,fontSize=18,richText=false,padding=new RectOffset(8,8,6,6)};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=15};
        }
        private void OnGUI()
        {
            if(Local==null || !Local.IsSpawned || CapOptions.IsOpen || CapLoadingScreen.Blocking || CapPoliceNpc.ModalOpen || CapCaseNews.ModalOpen || CapLobbyUI.ProfileEditing)return;
            Styles();var matrix=GUI.matrix;var tint=GUI.color;int depth=GUI.depth;
            try
            {
                GUI.depth=-200;GUI.color=Color.white;
                float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
                float width=typing?440:300,height=typing?272:180;
                float left=Local.InTown.Value?16*scale:Screen.width-(width+20)*scale;
                float bottom=Local.InTown.Value?16:100;
                GUI.matrix=Matrix4x4.TRS(new Vector3(left,Screen.height-(height+bottom)*scale),Quaternion.identity,Vector3.one*scale);
                GUI.color=new Color(.035f,.065f,.105f,typing?.97f:.2f);GUI.DrawTexture(new Rect(0,0,width,height),Texture2D.whiteTexture);GUI.color=new Color(1,1,1,typing?1:.55f);
                GUI.Label(new Rect(12,6,width-24,28),"일반 채팅 · 방 전체",text);
                float contentWidth=width-40,contentHeight=0;
                foreach(string line in messages)contentHeight+=text.CalcHeight(new GUIContent(line),contentWidth)+4;
                var viewport=new Rect(12,36,width-24,height-86);
                scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,contentWidth,Mathf.Max(viewport.height,contentHeight)),false,false);
                float y=0;
                foreach(string line in messages){float h=text.CalcHeight(new GUIContent(line),contentWidth);GUI.Label(new Rect(0,y,contentWidth,h),line,text);y+=h+4;}
                GUI.EndScrollView();
                if(!typing)
                {
                    if(GUI.Button(new Rect(12,height-42,width-24,30),"Enter · 채팅 입력",button))BeginInput();
                    return;
                }
                var evt=Event.current;
                bool enter=evt.type==EventType.KeyDown && (evt.keyCode==KeyCode.Return || evt.keyCode==KeyCode.KeypadEnter);
                if(enter && Time.frameCount!=openedFrame)
                {
                    // The Enter which confirms a Korean IME composition must not send unfinished text.
                    if(composition.Length==0 && compositionFrame<Time.frameCount-1)
                    {
                        if(string.IsNullOrWhiteSpace(draft) || Send(draft))CancelInput();
                    }
                    evt.Use();
                }
                GUI.SetNextControlName("CapRoomChatInput");
                draft=GUI.TextField(new Rect(12,height-44,width-96,32),draft,MaximumLength,field);
                if(focusInput){GUI.FocusControl("CapRoomChatInput");focusInput=false;}
                if(imeKeyboard!=null)imeKeyboard.SetIMECursorPosition(new Vector2(28*scale,Screen.height-28*scale));
                if(GUI.Button(new Rect(width-76,height-44,64,32),"전송",button) && composition.Length==0 && Send(draft))CancelInput();
                GUI.Label(new Rect(12,height-69,width-24,24),notice.Length>0?notice:$"Enter 전송 · Esc 취소 · {draft.Length}/{MaximumLength}",new GUIStyle(text){fontSize=13});
            }
            finally{GUI.matrix=matrix;GUI.color=tint;GUI.depth=depth;}
        }
        private void OnDestroy(){CancelInput();if(Instance==this)Instance=null;if(font!=null)Destroy(font);}
    }
}
