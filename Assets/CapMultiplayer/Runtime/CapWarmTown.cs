using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cap.Multiplayer
{
    // Local visual environment; only phase and player transforms travel over NGO.
    [DefaultExecutionOrder(300)]
    public sealed partial class CapWarmTown : MonoBehaviour
    {
        public static CapWarmTown Instance { get; private set; }
        public const float OffsetX = 100;
        public static readonly Rect Bounds = new Rect(-768/MapPPU, -512/MapPPU, 1536/MapPPU, 1024/MapPPU);
        public bool InTown { get; private set; }
        public bool Overview { get; private set; }
        public void SetOverviewForVerification(bool value) { Overview=value; }
        public readonly List<Rect> Obstacles = new List<Rect>();
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly List<GameObject> lobbyObjects = new List<GameObject>();
        private GameObject root;
        private Sprite white;
        public Material ArtMaterial { get; private set; }
        private readonly List<Sprite> groundSprites = new List<Sprite>();
        private Camera view;
        private Vector3 lobbyCamera;
        private float lobbyZoom;
        private float cameraZoom = ReferenceCameraHalfHeight;
        private Font font;
        private GUIStyle label;
        [Serializable] public class Region { public string key, sheet; public int x,y,w,h; }
        [Serializable] public class Catalog { public Region[] items; }

        public static Vector3 Spawn(int slot) => MapWorld(1120 + slot * 14, 366);
        public static int Depth(float footY) => Mathf.RoundToInt(-footY * 100);
        public static bool CanStart => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost && NetworkManager.Singleton.LocalClient?.PlayerObject != null;

        private void Awake()
        {
            Instance = this;
            QualitySettings.vSyncCount=1;
            Application.targetFrameRate=60;
            view = Camera.main;
            if(view != null) { lobbyCamera=view.transform.position; lobbyZoom=view.orthographicSize; }
            foreach(var sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if(sr.GetComponent<NetworkObject>() == null) lobbyObjects.Add(sr.gameObject);
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("WarmTown/catalog").text);
            foreach(var item in catalog.items)
            {
                var texture=Resources.Load<Texture2D>("WarmTown/"+item.sheet);
                if(texture == null) throw new InvalidOperationException("Missing town sheet: "+item.sheet);
                texture.filterMode=FilterMode.Point;
                var sprite=Sprite.Create(texture,new Rect(item.x,item.y,item.w,item.h),new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);
                sprite.name=item.key;
                sprites.Add(item.key,sprite);
            }
            white=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
            ArtMaterial=new Material(Resources.Load<Shader>("WarmTown/CapPixelSprite"));
            root=new GameObject("Warm Town — modular environment");
            Build();
            root.SetActive(false);
            Debug.Log($"[CAP-TOWN] Built {root.transform.childCount} modular objects; {Obstacles.Count} footprints.");
        }

        public Sprite PlayerSprite(int slot) => sprites.TryGetValue("07-players-"+slot, out var s) ? s : null;
        public static void StartForAll(bool town)
        {
            var nm=NetworkManager.Singleton;
            if(nm == null || !nm.IsServer) return;
            foreach(var player in FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None))
                if(player.IsSpawned) player.SetTown(town);
        }

        private void Update()
        {
            var nm=NetworkManager.Singleton;
            var local=nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            var player=local != null ? local.GetComponent<CapNetworkPlayer>() : null;
            bool desired=player != null && player.IsSpawned && player.InTown.Value;
            if(desired != InTown)
            {
                InTown=desired; root.SetActive(desired); Overview=false;
                foreach(var go in lobbyObjects) if(go != null) go.SetActive(!desired);
                if(view != null) { if(!desired)view.rect=new Rect(0,0,1,1); view.transform.position=desired ? Spawn((int)(player.OwnerClientId%4))+new Vector3(0,2,-10):lobbyCamera; view.orthographicSize=desired?cameraZoom:lobbyZoom; }
                Debug.Log("[CAP-TOWN] Local phase="+(desired?"town":"lobby"));
            }
            if(!InTown || view==null || player==null) return;
            if(Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) Overview=!Overview;
            // Fixed reference framing; scroll wheel cannot silently change asset scale.
        }

        private void LateUpdate()
        {
            if(!InTown || view==null) return;
            var obj=NetworkManager.Singleton.LocalClient?.PlayerObject;
            if(obj==null) return;
            UpdateBlockoutLabels();
            float targetAspect=1534f/1025f;
            float windowAspect=(float)Screen.width/Screen.height;
            view.rect=windowAspect>targetAspect ? new Rect((1-targetAspect/windowAspect)/2,0,targetAspect/windowAspect,1) : new Rect(0,(1-windowAspect/targetAspect)/2,1,windowAspect/targetAspect);
            view.orthographicSize=Overview?Mathf.Max(Bounds.height/2,Bounds.width/(2*targetAspect)):ReferenceCameraHalfHeight;
            var player=obj.GetComponent<CapNetworkPlayer>();
            view.transform.position=Overview?new Vector3(OffsetX,0,-10):player.VisualCenter+new Vector3(0,0,-10);

        }

        private SpriteRenderer Make(string name)
        {
            var go=new GameObject(name); go.transform.SetParent(root.transform,false);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sharedMaterial=ArtMaterial; return sr;
        }
        private void Fill(string name,string key,float x,float y,float w,float h,int order)
        {
            var sr=Make(name); var source=sprites[key];
            float tileWidth=key=="pavement"?.85f:2;
            var tile=Sprite.Create(source.texture,source.rect,new Vector2(.5f,.5f),source.rect.width/tileWidth,0,SpriteMeshType.FullRect);
            groundSprites.Add(tile);
            sr.sprite=tile; sr.drawMode=SpriteDrawMode.Tiled; sr.size=new Vector2(w,h); sr.sortingOrder=order;
            sr.transform.position=new Vector3(OffsetX+x,y,0);
        }
        private void Flat(string name,float x,float y,float w,float h,Color color,int order)
        {
            var sr=Make(name); sr.sprite=white; sr.color=color; sr.sortingOrder=order;
            sr.transform.position=new Vector3(OffsetX+x,y,0); sr.transform.localScale=new Vector3(w,h,1);
        }
        private void Solid(SpriteRenderer sr,float x,float y,float w,float h)
        {
            var rect=new Rect(x-w/2,y,w,h); Obstacles.Add(rect);
            var box=sr.gameObject.AddComponent<BoxCollider2D>();
            var scale=sr.transform.localScale;
            box.size=new Vector2(w/scale.x,h/scale.y);
            box.offset=new Vector2(0,(y-sr.transform.position.y+h/2)/scale.y);
        }
        private void Object(string key,string name,float x,float foot,float width,float colliderWidth=0,float colliderHeight=0)
        {
            var sr=Make(name); sr.sprite=sprites[key];
            float scale=width/sr.sprite.bounds.size.x;
            sr.transform.localScale=Vector3.one*scale; sr.transform.position=new Vector3(OffsetX+x,foot,0);
            sr.sortingOrder=Depth(foot+.15f);
            if(colliderWidth>0) Solid(sr,x,foot+.1f,colliderWidth,colliderHeight);
        }
        private void Building(string key,string name,float x,float y,float width)
        {
            Object(key,name,x,y,width,width*.90f,sprites[key].rect.height/sprites[key].rect.width*width*.65f);
        }
        private void Tree(float x,float y,int style=0) => Object("03-greenery-"+style,"Tree",x,y,style==1?1.6f:3.2f,.7f,.55f);
        private void Bench(float x,float y) => Object("05-props-0","Bench",x,y,2.1f,1.9f,.55f);
        private void Lamp(float x,float y) => Object("04-infrastructure-1","Street light",x,y,1.2f,.32f,.35f);

        public bool Blocked(Vector2 local,float radius=.23f)
        {
            if(local.x<Bounds.xMin+radius || local.x>Bounds.xMax-radius || local.y<Bounds.yMin+radius || local.y>Bounds.yMax-radius) return true;
            foreach(var r in Obstacles)
                if(local.x>r.xMin-radius && local.x<r.xMax+radius && local.y>r.yMin-radius && local.y<r.yMax+radius) return true;
            return false;
        }
        public Vector3 Move(Vector3 world,Vector2 delta)
        {
            var p=new Vector2(world.x-OffsetX,world.y);
            int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.12f)); delta/=steps;
            for(int i=0;i<steps;i++)
            {
                var next=p+new Vector2(delta.x,0); if(!Blocked(next)) p=next;
                next=p+new Vector2(0,delta.y); if(!Blocked(next)) p=next;
            }
            return new Vector3(p.x+OffsetX,p.y,0);
        }

        private void OnGUI()
        {
            if(!InTown) return;
            if(!Overview) return;
            if(label==null) { font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},18); label=new GUIStyle(GUI.skin.label){font=font,fontSize=18}; }
            float s=Mathf.Clamp(Screen.width/1400f,.6f,1.4f); var old=GUI.matrix; GUI.matrix=Matrix4x4.Scale(Vector3.one*s);
            GUI.Box(new Rect(16,16,440,64),GUIContent.none);
            GUI.Label(new Rect(30,22,420,28),"대학생 탐정단  /  캠퍼스 마을",label);
            GUI.Label(new Rect(30,49,420,28),"WASD 이동 · B 자전거 · 배율 고정 · M 지도",new GUIStyle(label){fontSize=14});
            var local=NetworkManager.Singleton.LocalClient?.PlayerObject;
            if(local!=null)
            {
                var rider=local.GetComponent<CapNetworkPlayer>();
                if(GUI.Button(new Rect(16,116,180,36),rider.Riding.Value?"B · 자전거 내리기":"B · 자전거 타기")) rider.ToggleBicycle();
            }
            if(local!=null && local.GetComponent<CapNetworkPlayer>().Riding.Value)
                GUI.Label(new Rect(30,82,350,28),"자전거 주행 중 · 속도 ×1.8 · B 내리기",label);
            if(NetworkManager.Singleton.IsHost && GUI.Button(new Rect(Screen.width/s-160,20,140,36),"대기실로 돌아가기")) StartForAll(false);
            GUI.matrix=old;
        }

        private void OnDestroy()
        {
            if(Instance==this) Instance=null;
            if(root!=null) Destroy(root);
            foreach(var sprite in sprites.Values) if(sprite!=null) Destroy(sprite);
            if(white!=null) Destroy(white);
            foreach(var s in groundSprites) if(s!=null) Destroy(s);
            if(ArtMaterial!=null) Destroy(ArtMaterial);
            if(font!=null) Destroy(font);
            if(blockoutFont!=null) Destroy(blockoutFont);
        }
    }
}
