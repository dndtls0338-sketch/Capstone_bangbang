using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cap.Multiplayer
{
    [DefaultExecutionOrder(-75)]
    public sealed class CapCaseNews : MonoBehaviour
    {
        public static CapCaseNews Instance { get; private set; }
        public CapCaseNewsDefinition Definition { get; private set; }
        private static CapNetworkPlayer Local=>NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>();
        public static bool ModalOpen=>Instance!=null && Local!=null && Local.IsSpawned && Local.InTown.Value && Local.ReadingCaseNews.Value;
        private bool ownsDefinition;
        private Font font;
        private GUIStyle masthead,title,lead,body,small,button;
        private Vector2 scroll;
        private CapNetworkPlayer displayedPlayer;
        private uint displayedRevision;
        private static readonly Color Ink=new Color(.12f,.12f,.12f,1);

        private void Awake()
        {
            Instance=this;Definition=Resources.Load<CapCaseNewsDefinition>("News/CaseNews");
            if(Definition==null){Definition=ScriptableObject.CreateInstance<CapCaseNewsDefinition>();ownsDefinition=true;}
        }
        private void Update()
        {
            if(!ModalOpen){displayedPlayer=null;return;}
            var player=Local;
            if(displayedPlayer!=player || displayedRevision!=player.CaseNewsRevision.Value)
            {displayedPlayer=player;displayedRevision=player.CaseNewsRevision.Value;scroll=Vector2.zero;}
            if(CapWarmTown.Instance!=null && CapWarmTown.Instance.Overview)CapWarmTown.Instance.SetMapOpen(false);
            if(CapPoliceNpc.ModalOpen)CapPoliceNpc.Instance.CancelForCaseNews();
        }

        public bool HandleInput(CapNetworkPlayer player)
        {
            if(!ModalOpen || player!=Local)return false;
            var keyboard=Keyboard.current;
            if(!Application.isFocused || keyboard==null || CapLoadingScreen.Blocking || CapOptions.IsOpen)return true;
            if(keyboard.escapeKey.wasPressedThisFrame && !CapControls.EscapeConsumed)
            {CapControls.ConsumeEscape();Close();}
            else if(keyboard[CapControls.Get(CapAction.Interact)].wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)Close();
            return true;
        }
        public void Close()
        {
            if(ModalOpen)Local.CloseCaseNews(Local.CaseNewsRevision.Value);
        }

        private void Styles()
        {
            if(body!=null)return;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},28);
            body=new GUIStyle(GUI.skin.label){font=font,fontSize=22,wordWrap=true,richText=false,padding=new RectOffset(),normal={textColor=Ink}};
            masthead=new GUIStyle(body){fontSize=38,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            title=new GUIStyle(body){fontSize=34,fontStyle=FontStyle.Bold};
            lead=new GUIStyle(body){fontSize=20,fontStyle=FontStyle.Bold};
            small=new GUIStyle(body){fontSize=15};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=20,richText=false};
        }
        private static void Fill(Rect rect,Color color)
        {var previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous;}

        private void OnGUI()
        {
            var player=Local;
            if(player==null || !player.IsSpawned || !player.InTown.Value || CapOptions.IsOpen || CapLoadingScreen.Blocking)return;
            Styles();var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;
            try
            {
                float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
                float width=Screen.width/scale,height=Screen.height/scale;
                GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));GUI.color=Color.white;GUI.depth=-15000;
                if(!ModalOpen)
                {
                    if(CapControls.Blocked || CapCaseNewspaper.Instance==null || !CapCaseNewspaper.Instance.IsNear(player))return;
                    var hint=new Rect(width/2-230,height-114,460,48);
                    Fill(hint,Ink);Fill(new Rect(hint.x+2,hint.y+2,hint.width-4,hint.height-4),Definition.paperColor);
                    GUI.Label(hint,$"[{CapControls.Label(CapAction.Interact)}] 책상 위 사건 뉴스 읽기",new GUIStyle(lead){alignment=TextAnchor.MiddleCenter});
                    return;
                }
                DrawPaper(width,height,player);
                if(Event.current.isMouse || Event.current.isKey || Event.current.type==EventType.ScrollWheel)Event.current.Use();
            }
            finally{GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;}
        }

        private void DrawPaper(float width,float height,CapNetworkPlayer player)
        {
            var article=Definition.ForRound(player.CaseRound.Value);
            Fill(new Rect(0,0,width,height),new Color(0,0,0,.7f));
            var paper=new Rect((width-1000)/2,(height-700)/2,1000,700);
            Fill(new Rect(paper.x+9,paper.y+10,paper.width,paper.height),new Color(0,0,0,.3f));
            Fill(paper,Ink);Fill(new Rect(paper.x+3,paper.y+3,paper.width-6,paper.height-6),Definition.paperColor);
            float x=paper.x+40;
            GUI.Label(new Rect(x,paper.y+24,920,56),Definition.newspaperName,masthead);
            Fill(new Rect(x,paper.y+90,920,3),Ink);Fill(new Rect(x,paper.y+97,920,1),Ink);
            GUI.Label(new Rect(x,paper.y+108,920,25),$"사건 브리핑  /  제 {player.CaseRound.Value} 라운드",small);
            Fill(new Rect(x,paper.y+146,920,1),Ink);

            // Keep long round-specific headlines and articles inside a scrollable column.
            var area=new Rect(x,paper.y+172,650,424);
            float textWidth=area.width-25;
            float titleHeight=title.CalcHeight(new GUIContent(article.headline??""),textWidth);
            float leadHeight=lead.CalcHeight(new GUIContent(article.summary??""),textWidth);
            float bodyHeight=body.CalcHeight(new GUIContent(article.body??""),textWidth);
            scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,textWidth,Mathf.Max(area.height,titleHeight+leadHeight+bodyHeight+45)));
            GUI.Label(new Rect(0,0,textWidth,titleHeight),article.headline,title);
            GUI.Label(new Rect(0,titleHeight+15,textWidth,leadHeight),article.summary,lead);
            GUI.Label(new Rect(0,titleHeight+leadHeight+35,textWidth,bodyHeight),article.body,body);
            GUI.EndScrollView();

            Fill(new Rect(x+677,paper.y+172,1,424),new Color(.4f,.4f,.4f));
            GUI.Label(new Rect(x+702,paper.y+179,210,38),"탐정단 소식",lead);
            Fill(new Rect(x+702,paper.y+228,210,90),new Color(.62f,.62f,.62f));
            GUI.Label(new Rect(x+714,paper.y+248,186,46),"사건 기록",new GUIStyle(lead){alignment=TextAnchor.MiddleCenter});
            GUI.Label(new Rect(x+702,paper.y+347,210,176),"기사를 다시 읽고 싶다면\n회의실 책상 위 신문을\n확인해 주세요.",small);
            Fill(new Rect(x,paper.y+621,920,2),Ink);
            GUI.Label(new Rect(x,paper.y+646,650,32),$"{CapControls.Label(CapAction.Interact)} / Enter / Esc · 신문 닫기",small);
            if(GUI.Button(new Rect(paper.xMax-232,paper.y+641,192,40),"신문 접기",button))Close();
        }
        private void OnDestroy()
        {if(Instance==this)Instance=null;if(font!=null)Destroy(font);if(ownsDefinition && Definition!=null)Destroy(Definition);}
    }
}
