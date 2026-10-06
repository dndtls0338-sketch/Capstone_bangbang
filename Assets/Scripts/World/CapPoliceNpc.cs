using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Cap.Multiplayer
{
    // Local art and UI. Conversation ownership is validated on the server by CapNetworkPlayer.
    public sealed class CapPoliceNpc : MonoBehaviour
    {
        public static CapPoliceNpc Instance { get; private set; }
        public static bool ModalOpen => Instance!=null && (Instance.pending || Instance.open || Instance.notice!=null);
        [SerializeField] private CapPoliceNpcDefinition definition;
        public CapPoliceNpcDefinition Definition => definition;
        private SpriteRenderer customArt;
        private GameObject fallbackArt;
        private CapNetworkPlayer speaker;
        private bool pending,open,ownsDefinition;
        private uint requestId;
        private string notice;
        private string[] conversation;
        private int page;
        private float pageStarted;
        private bool revealed;
        private Vector2 scroll;
        private Font font;
        private GUIStyle body,nameStyle,hint,button;
        private static readonly Color Navy=new Color(.035f,.14f,.28f);
        private static readonly Color Blue=new Color(.04f,.26f,.46f);
        private static readonly Color Paper=new Color(.98f,.97f,.91f);
        private static readonly Color Skin=new Color(.96f,.76f,.57f);
        // Simple geometric stand-in shared by the world character and the portrait.
        private static readonly Rect[] Parts={
            new Rect(4,19,3,5),new Rect(9,19,3,5),new Rect(3,11,10,9),
            new Rect(1,12,2,7),new Rect(13,12,2,7),new Rect(4,4,8,8),
            new Rect(3,1,10,4),new Rect(2,5,12,2),new Rect(7,2,2,2),
            new Rect(5,8,1,1),new Rect(10,8,1,1),new Rect(6,12,4,3),
            new Rect(7,14,2,5),new Rect(10,14,2,2),new Rect(3,19,10,1)
        };
        private static readonly Color[] Colors={
            Navy,Navy,Blue,Skin,Skin,Skin,Blue,Navy,new Color(1,.78f,.2f),
            Navy,Navy,Paper,Navy,new Color(1,.78f,.2f),Navy
        };

        public void Initialize(Sprite square,Material material)
        {
            Instance=this;
            if(definition==null)definition=Resources.Load<CapPoliceNpcDefinition>("Dialogue/PoliceNpc");
            if(definition==null){definition=ScriptableObject.CreateInstance<CapPoliceNpcDefinition>();ownsDefinition=true;}
            var group=gameObject.AddComponent<SortingGroup>();group.sortingOrder=CapWarmTown.Depth(transform.position.y);
            fallbackArt=new GameObject("Default police character");fallbackArt.transform.SetParent(transform,false);
            for(int i=0;i<Parts.Length;i++)
            {
                var go=new GameObject("Uniform part "+i);go.transform.SetParent(fallbackArt.transform,false);
                var r=Parts[i];go.transform.localPosition=new Vector3((r.center.x-8)/24,(24-r.center.y)/24,0);
                go.transform.localScale=new Vector3(r.width/24,r.height/24,1);
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=square;sr.color=Colors[i];sr.sharedMaterial=material;sr.sortingOrder=i;
            }
            var art=new GameObject("Replaceable character sprite");art.transform.SetParent(transform,false);
            customArt=art.AddComponent<SpriteRenderer>();customArt.sharedMaterial=material;
            RefreshArt();
        }

        private void RefreshArt()
        {
            if(definition==null || customArt==null)return;
            float height=Mathf.Max(.2f,definition.worldHeight);
            customArt.sprite=definition.worldSprite;customArt.enabled=customArt.sprite!=null;
            fallbackArt.SetActive(customArt.sprite==null);fallbackArt.transform.localScale=Vector3.one*height;
            if(customArt.sprite!=null)
            {
                float scale=height/Mathf.Max(.001f,customArt.sprite.bounds.size.y);
                customArt.transform.localScale=new Vector3(scale,scale,1);
                var bounds=customArt.sprite.bounds;
                customArt.transform.localPosition=new Vector3(-bounds.center.x*scale,-bounds.min.y*scale,0);
            }
        }

        public bool IsNear(CapNetworkPlayer player) => definition!=null && player!=null && player.IsSpawned &&
            player.InTown.Value && !player.InMeetingRoom.Value &&
            Vector2.Distance(player.transform.position,transform.position)<=Mathf.Max(1.5f,definition.interactionDistance);

        public bool TryInteract(CapNetworkPlayer player)
        {
            if(!IsNear(player) || player.ViewingMap.Value || CapControls.Blocked)return false;
            speaker=player;pending=true;notice=null;
            player.RequestPoliceDialogue(++requestId);
            return true;
        }

        public void ReceiveReply(CapNetworkPlayer player,uint replyId,PoliceDialogueResult result)
        {
            // Close already sends an ordered release RPC. Ignore old replies after cancel/retry.
            if(replyId!=requestId || !pending || speaker!=player || !isActiveAndEnabled)return;
            pending=false;
            if(result!=PoliceDialogueResult.Granted)
            {
                notice=result==PoliceDialogueResult.Busy?"지금 대화할 수 없습니다":"캐릭터 가까이에서 다시 말을 걸어 주세요.";
                return;
            }
            open=true;page=0;
            conversation=definition.lines!=null && definition.lines.Length>0 ? (string[])definition.lines.Clone() : new[]{"안녕하세요. 무엇을 도와드릴까요?"};
            StartPage();
        }

        // Called before normal player shortcuts, so the opening E press cannot also advance text.
        public bool HandleInput(CapNetworkPlayer player)
        {
            if(!ModalOpen || player!=speaker)return false;
            var keyboard=Keyboard.current;
            if(!Application.isFocused || keyboard==null || CapOptions.IsOpen || CapLoadingScreen.Blocking)return true;
            if(keyboard.escapeKey.wasPressedThisFrame){Close();return true;}
            if(keyboard[CapControls.Get(CapAction.Interact)].wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
            {
                if(notice!=null)Close();
                else if(open)Advance();
            }
            return true;
        }

        private string Line => conversation!=null && page<conversation.Length ? conversation[page]??"" : "";
        private int VisibleCount => revealed?Line.Length:Mathf.Min(Line.Length,Mathf.FloorToInt((Time.unscaledTime-pageStarted)*36));
        private void StartPage(){pageStarted=Time.unscaledTime;revealed=false;scroll=Vector2.zero;}
        private void Advance()
        {
            if(VisibleCount<Line.Length){revealed=true;return;}
            if(page+1>=conversation.Length){Close();return;}
            page++;StartPage();
        }
        private void Close()
        {
            if(speaker!=null && (pending || open || speaker.TalkingToPolice.Value))speaker.EndPoliceDialogue();
            pending=false;open=false;notice=null;speaker=null;conversation=null;
        }

        private void Update()
        {
            RefreshArt();
            if(ModalOpen && (speaker==null || !speaker.IsSpawned || !speaker.InTown.Value || speaker.InMeetingRoom.Value))Close();
        }
        private void OnDisable(){Close();}
        private void OnDestroy()
        {
            if(Instance==this)Instance=null;
            if(font!=null)Destroy(font);
            if(ownsDefinition && definition!=null)Destroy(definition);
        }

        private void Styles()
        {
            if(body!=null)return;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},24);
            body=new GUIStyle(GUI.skin.label){font=font,fontSize=24,wordWrap=true,normal={textColor=Navy},padding=new RectOffset(0,0,0,0)};
            nameStyle=new GUIStyle(body){fontSize=23,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
            hint=new GUIStyle(body){fontSize=15,normal={textColor=Blue},alignment=TextAnchor.MiddleRight};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=18,normal={textColor=Navy},hover={textColor=Blue},active={textColor=Navy}};
        }
        private static void Fill(Rect area,Color color)
        {var previous=GUI.color;GUI.color=color;GUI.DrawTexture(area,Texture2D.whiteTexture);GUI.color=previous;}
        private static Rect Inset(Rect r,float n)=>new Rect(r.x+n,r.y+n,r.width-2*n,r.height-2*n);
        private static void CutPanel(Rect r,Color color,float corner=12)
        {
            corner=Mathf.Min(corner,Mathf.Min(r.width,r.height)/2);
            Fill(new Rect(r.x+corner,r.y,r.width-corner*2,r.height),color);
            Fill(new Rect(r.x,r.y+corner,r.width,r.height-corner*2),color);
            Fill(new Rect(r.x+corner/2,r.y+corner/2,r.width-corner,r.height-corner),color);
        }
        private static void Frame(Rect r)
        {
            CutPanel(r,Navy);CutPanel(Inset(r,3),Paper);CutPanel(Inset(r,6),Blue);CutPanel(Inset(r,10),Paper);
        }

        private void OnGUI()
        {
            if(CapOptions.IsOpen || CapLoadingScreen.Blocking || definition==null)return;
            var local=Unity.Netcode.NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>();
            if(local==null || !local.IsSpawned || !local.InTown.Value || local.InMeetingRoom.Value)return;
            if(CapWarmTown.Instance!=null && CapWarmTown.Instance.Overview)return;
            Styles();var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;
            try
            {
                float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
                float width=Screen.width/scale,height=Screen.height/scale;
                GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));GUI.color=Color.white;GUI.depth=-500;
                if(ModalOpen)
                {
                    if(open)DrawDialogue(width,height);
                    else DrawNotice(width,height);
                    // Stop pointer events from activating gameplay HUD controls underneath.
                    if(Event.current.isMouse || Event.current.type==EventType.ScrollWheel)Event.current.Use();
                    return;
                }
                var camera=Camera.main;
                if(camera!=null)
                {
                    var point=camera.WorldToScreenPoint(transform.position+Vector3.up*(definition.worldHeight+.55f));
                    if(point.z>0 && point.x>0 && point.x<Screen.width && point.y>0 && point.y<Screen.height)
                    {
                        var bubble=new Rect(point.x/scale-25,(Screen.height-point.y)/scale-18,50,32);
                        Frame(bubble);GUI.Label(bubble,"•••",new GUIStyle(body){fontSize=18,alignment=TextAnchor.MiddleCenter});
                    }
                }
                if(IsNear(local))
                {
                    var box=new Rect(width/2-230,height-140,460,48);
                    Frame(box);GUI.Label(Inset(box,10),$"[{CapControls.Label(CapAction.Interact)}] {definition.displayName}에게 말 걸기",new GUIStyle(body){fontSize=18,alignment=TextAnchor.MiddleCenter});
                }
            }
            finally{GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;}
        }

        private void DrawDialogue(float width,float height)
        {
            var panel=new Rect((width-1136)/2,height-252,1136,216);
            Frame(panel);
            var tab=new Rect(panel.x+14,panel.y-38,330,51);
            CutPanel(tab,Navy);CutPanel(Inset(tab,3),Paper);CutPanel(Inset(tab,6),Blue);
            GUI.Label(new Rect(tab.x+22,tab.y+10,284,34),"●  "+definition.displayName,nameStyle);
            var area=new Rect(panel.x+36,panel.y+32,830,123);
            float textHeight=Mathf.Max(area.height,body.CalcHeight(new GUIContent(Line),area.width-22));
            scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,area.width-22,textHeight));
            GUI.Label(new Rect(0,0,area.width-22,textHeight),Line.Substring(0,VisibleCount),body);GUI.EndScrollView();
            DrawPortrait(new Rect(panel.xMax-230,panel.y-12,188,176));
            GUI.Label(new Rect(panel.x+36,panel.yMax-43,160,28),$"{page+1} / {conversation.Length}",hint);
            GUI.Label(new Rect(panel.x+225,panel.yMax-43,550,28),$"{CapControls.Label(CapAction.Interact)} / Enter · 다음     Esc · 대화 종료",hint);
            bool last=page+1>=conversation.Length && VisibleCount==Line.Length;
            if(GUI.Button(new Rect(panel.xMax-223,panel.yMax-46,184,30),last?"대화 마치기":"다음  ▼",button))Advance();
            if(GUI.Button(new Rect(panel.xMax-45,panel.y+16,26,26),"×",button))Close();
        }

        private void DrawPortrait(Rect rect)
        {
            if(definition.portrait!=null)
            {
                GUI.DrawTexture(rect,definition.portrait,ScaleMode.ScaleToFit,true);
                return;
            }
            float scale=Mathf.Min(rect.width/16,rect.height/24);
            for(int i=0;i<Parts.Length;i++)
            {
                var r=Parts[i];
                Fill(new Rect(rect.center.x+(r.x-8)*scale,rect.y+r.y*scale,r.width*scale,r.height*scale),Colors[i]);
            }
        }
        private void DrawNotice(float width,float height)
        {
            Fill(new Rect(0,0,width,height),new Color(0,0,0,.25f));
            var panel=new Rect(width/2-310,height/2-95,620,190);Frame(panel);
            GUI.Label(new Rect(panel.x+24,panel.y+33,572,48),pending?"대화를 요청하는 중…":notice,new GUIStyle(body){alignment=TextAnchor.MiddleCenter});
            if(!pending)GUI.Label(new Rect(panel.x+24,panel.y+86,572,30),notice=="지금 대화할 수 없습니다"?"다른 플레이어가 대화 중입니다. 잠시 후 다시 시도해 주세요.":"E / Enter 또는 확인 버튼으로 닫을 수 있습니다.",new GUIStyle(hint){alignment=TextAnchor.MiddleCenter});
            if(GUI.Button(new Rect(panel.center.x-90,panel.yMax-52,180,32),pending?"취소 · Esc":"확인",button))Close();
        }
    }
}
