using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed class CapCaseNewspaper : MonoBehaviour
    {
        public static CapCaseNewspaper Instance { get; private set; }
        private CapCaseNewsDefinition definition;
        private SpriteRenderer customArt,paper;
        private GameObject fallback;
        private bool ownsDefinition;

        public void Initialize(Sprite square,Material material)
        {
            Instance=this;
            definition=CapCaseNews.Instance!=null?CapCaseNews.Instance.Definition:Resources.Load<CapCaseNewsDefinition>("News/CaseNews");
            if(definition==null){definition=ScriptableObject.CreateInstance<CapCaseNewsDefinition>();ownsDefinition=true;}
            fallback=new GameObject("Default gray newspaper");fallback.transform.SetParent(transform,false);
            paper=Part("Paper",Vector2.zero,Vector2.one,definition.paperColor,0,square,material);
            var ink=new Color(.25f,.25f,.25f,1);
            Part("Masthead",new Vector2(0,.28f),new Vector2(.76f,.08f),ink,1,square,material);
            Part("Fold",new Vector2(0,-.08f),new Vector2(.018f,.48f),ink,1,square,material);
            for(int column=0;column<2;column++)for(int row=0;row<4;row++)
                Part("Printed line",new Vector2(column==0?-.23f:.23f,.1f-row*.12f),new Vector2(.32f,.027f),ink,1,square,material);
            var art=new GameObject("Replaceable newspaper sprite");art.transform.SetParent(transform,false);
            customArt=art.AddComponent<SpriteRenderer>();customArt.sharedMaterial=material;customArt.sortingOrder=-1980;
            RefreshArt();
        }

        private SpriteRenderer Part(string name,Vector2 position,Vector2 size,Color color,int order,Sprite square,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(fallback.transform,false);
            go.transform.localPosition=position;go.transform.localScale=new Vector3(size.x,size.y,1);
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=square;sr.color=color;sr.sharedMaterial=material;sr.sortingOrder=-1980+order;
            return sr;
        }

        public bool IsNear(CapNetworkPlayer player)=>definition!=null && player!=null && player.IsSpawned &&
            player.InTown.Value && player.InMeetingRoom.Value &&
            !CapWarmTown.MeetingBlocked(player.transform.position-CapWarmTown.MeetingCenter) &&
            Vector2.Distance(player.transform.position,transform.position)<=Mathf.Clamp(definition.interactionDistance,1.2f,2.2f);

        private void Update(){RefreshArt();}
        private void RefreshArt()
        {
            if(definition==null || customArt==null)return;
            float width=Mathf.Clamp(definition.worldWidth,.5f,2);
            paper.color=definition.paperColor;fallback.transform.localScale=new Vector3(width,width*.625f,1);
            customArt.sprite=definition.worldSprite;customArt.enabled=customArt.sprite!=null;fallback.SetActive(!customArt.enabled);
            if(customArt.enabled)
            {
                var bounds=customArt.sprite.bounds;
                // Fit inside the desk's newspaper area while retaining the image's aspect ratio.
                float scale=Mathf.Min(width/Mathf.Max(.001f,bounds.size.x),width*.625f/Mathf.Max(.001f,bounds.size.y));
                customArt.transform.localScale=new Vector3(scale,scale,1);
                customArt.transform.localPosition=-bounds.center*scale;
            }
        }
        private void OnDestroy()
        {if(Instance==this)Instance=null;if(ownsDefinition && definition!=null)Destroy(definition);}
    }
}
