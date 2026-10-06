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
        public void SetOverviewForVerification(bool value) { SetMapOpen(value); }
        public readonly List<Rect> Obstacles = new List<Rect>();
        private readonly List<GameObject> lobbyObjects = new List<GameObject>();
        private GameObject root;
        private Sprite white;
        public Material ArtMaterial { get; private set; }
        private CapLobbyUI[] lobbyUIs;
        private Camera view;
        private Vector3 lobbyCamera;
        private float lobbyZoom;
        private float cameraZoom = ReferenceCameraHalfHeight;
        private Font font;
        private GUIStyle label;

        public static Vector3 Spawn(int slot) => MapWorld(1120 + slot * 22, 366);
        public static int Depth(float footY) => Mathf.RoundToInt(-footY * 100);

        private void Awake()
        {
            Instance = this;
            QualitySettings.vSyncCount=1;
            Application.targetFrameRate=60;
            view = Camera.main;
            lobbyUIs=FindObjectsByType<CapLobbyUI>(FindObjectsSortMode.None);
            if(view!=null){view.rect=new Rect(0,0,1,1);view.clearFlags=CameraClearFlags.SolidColor;}
            if(view != null) { lobbyCamera=view.transform.position; lobbyZoom=view.orthographicSize; }
            foreach(var sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if(sr.GetComponent<NetworkObject>() == null) lobbyObjects.Add(sr.gameObject);
            white=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
            ArtMaterial=new Material(Resources.Load<Shader>("WarmTown/CapPixelSprite"));
            root=new GameObject("Shape Town - geometry prototype");
            BuildShapeTown();
            BuildMeetingRoom();
            root.SetActive(false);
            Debug.Log($"[CAP-TOWN] Built {root.transform.childCount} modular objects; {Obstacles.Count} footprints.");
        }

        public Sprite PlayerSprite(int slot) => white;
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
                InTown=desired; root.SetActive(desired); Overview=false;ResetMinimap();
                foreach(var ui in lobbyUIs) if(ui!=null) ui.enabled=!desired;
                foreach(var go in lobbyObjects) if(go != null) go.SetActive(!desired);
                if(view != null) { view.rect=new Rect(0,0,1,1); view.transform.position=desired ? Spawn(player.PlayerSlot.Value)+new Vector3(0,2,-10):lobbyCamera; view.orthographicSize=desired?cameraZoom:lobbyZoom; }
                Debug.Log("[CAP-TOWN] Local phase="+(desired?"town":"lobby"));
            }
            bool inside=desired && player.InMeetingRoom.Value;
            root.SetActive(desired && !inside);meetingRoot.SetActive(inside);
            if(inside && Overview)SetMapOpen(false);
            if(!InTown || view==null || player==null) return;
            var keyboard=Keyboard.current;
            if(Application.isFocused && keyboard!=null && !CapControls.Blocked) {
                if(CapControls.Pressed(CapAction.Map))SetMapOpen(!Overview);
                else if(Overview && keyboard.escapeKey.wasPressedThisFrame)SetMapOpen(false);
            }
            // Fixed reference framing; scroll wheel cannot silently change asset scale.
        }

        private void LateUpdate()
        {
            if(!InTown || view==null) return;
            var obj=NetworkManager.Singleton.LocalClient?.PlayerObject;
            if(obj==null) return;
            // Fill the Game view at any aspect ratio; map UI never changes this camera.
            view.rect=new Rect(0,0,1,1);
            view.orthographicSize=ReferenceCameraHalfHeight;
            var player=obj.GetComponent<CapNetworkPlayer>();
            var cameraPosition=player.VisualCenter+new Vector3(0,0,-10);
            if(!player.InMeetingRoom.Value)
            {
                // Keep the entire viewport inside the town, including after a window resize.
                float aspect=Mathf.Max(view.aspect,.0001f);
                float halfHeight=Mathf.Min(ReferenceCameraHalfHeight,Bounds.height/2,Bounds.width/(2*aspect));
                float halfWidth=halfHeight*aspect;
                view.orthographicSize=halfHeight;
                float travelX=Mathf.Max(0,Bounds.width/2-halfWidth);
                float travelY=Mathf.Max(0,Bounds.height/2-halfHeight);
                float centerX=OffsetX+Bounds.center.x;
                cameraPosition.x=Mathf.Clamp(cameraPosition.x,centerX-travelX,centerX+travelX);
                cameraPosition.y=Mathf.Clamp(cameraPosition.y,Bounds.center.y-travelY,Bounds.center.y+travelY);
            }
            view.transform.position=cameraPosition;
            UpdateMinimap(player);
        }

        private SpriteRenderer Make(string name)
        {
            var go=new GameObject(name); go.transform.SetParent(root.transform,false);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sharedMaterial=ArtMaterial; return sr;
        }
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
            if(!InTown || CapOptions.IsOpen || CapLoadingScreen.Blocking) return;

            if(label==null) { font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},18); label=new GUIStyle(GUI.skin.label){font=font,fontSize=18}; }
            if(Overview){DrawMapOverlay();return;}
            float s=Mathf.Clamp(Screen.width/1400f,.6f,1.4f); var old=GUI.matrix; GUI.matrix=Matrix4x4.Scale(Vector3.one*s);
            GUI.Box(new Rect(16,16,440,64),GUIContent.none);
            GUI.Label(new Rect(30,22,420,28),(IsMeetingRoom ? "학생회관 / 탐정 동아리 회의실" : "대학생 탐정단 / 도형 테스트 마을"),label);
            GUI.Label(new Rect(30,49,420,28),$"{CapControls.Label(CapAction.Up)}/{CapControls.Label(CapAction.Left)}/{CapControls.Label(CapAction.Down)}/{CapControls.Label(CapAction.Right)} 이동 · {CapControls.Label(CapAction.Interact)} 대화 / 문"+(IsMeetingRoom?"":$" · {CapControls.Label(CapAction.Map)} 지도"),new GUIStyle(label){fontSize=14});
            var local=NetworkManager.Singleton.LocalClient?.PlayerObject;
            if(local!=null)
            {
                var rider=local.GetComponent<CapNetworkPlayer>();
                if(!IsMeetingRoom && GUI.Button(new Rect(16,116,180,36),CapControls.Label(CapAction.Bicycle)+(rider.Riding.Value?" · 자전거 내리기":" · 자전거 타기"))) rider.ToggleBicycle();
            }
            if(local!=null && local.GetComponent<CapNetworkPlayer>().Riding.Value)
                GUI.Label(new Rect(30,82,350,28),$"자전거 주행 중 · 속도 ×1.8 · {CapControls.Label(CapAction.Bicycle)} 내리기",label);
            GUI.matrix=old;
            DrawMinimap();
            DrawMeetingDoorHint();
        }

        private void OnDestroy()
        {
            if(Instance==this) Instance=null;
            if(root!=null) Destroy(root);
            if(meetingRoot!=null) Destroy(meetingRoot);
            if(white!=null) Destroy(white);
            if(ArtMaterial!=null) Destroy(ArtMaterial);
            if(font!=null) Destroy(font);
            if(mapImage!=null) Destroy(mapImage);
            if(mapDot!=null) Destroy(mapDot);
            if(minimapImage!=null) Destroy(minimapImage);
            if(blockoutFont!=null) Destroy(blockoutFont);
        }
    }
}
