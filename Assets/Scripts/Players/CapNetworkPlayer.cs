using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cap.Multiplayer
{
    // Server-authoritative movement with shared town footprint collision.
    [DefaultExecutionOrder(100)]
    public sealed partial class CapNetworkPlayer : NetworkBehaviour
    {
        [SerializeField] private float speed = 3.5f;
        public static Vector2? TestInput; // Development test input; null during normal play.
        private bool mapRequested;
        // Client IDs keep increasing on reconnect. Display slots are separate, host-assigned seats.
        public readonly NetworkVariable<int> PlayerSlot = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> ColorIndex = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<FixedString64Bytes> Nickname = new NetworkVariable<FixedString64Bytes>(default);
        public string PlayerLabel => PlayerSlot.Value < 0 ? "연결 중" : Nickname.Value.Length==0 ? "P"+(PlayerSlot.Value+1) : Nickname.Value+" · P"+(PlayerSlot.Value+1);
        public readonly NetworkVariable<FixedString64Bytes> VoicePlayerId=new NetworkVariable<FixedString64Bytes>(default,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> ViewingMap=new NetworkVariable<bool>(false);
        private Vector2 serverInput;
        private float lastInputTime;
        private float nextSend;
        private Vector2 sentInput;
        private float nextBikeToggle;
        private Vector3 previousStep, currentStep;
        public Vector3 VisualCenter => visual != null ? visual.bounds.center : transform.position;
        public Vector3 RenderPosition => visual != null ? visual.transform.position : transform.position;
        public readonly NetworkVariable<bool> Riding = new NetworkVariable<bool>(false);
        private float nextLog;
        private bool smokeMove;
        private bool smokeTest;
        public readonly NetworkVariable<bool> InMeetingRoom = new NetworkVariable<bool>(false);
        private float nextDoorTime;
        private float movementResumeTime;
        public bool SameSpace(CapNetworkPlayer other) => other!=null && InTown.Value==other.InTown.Value && InMeetingRoom.Value==other.InMeetingRoom.Value;
        public readonly NetworkVariable<bool> InTown = new NetworkVariable<bool>(false);
        // Low 3 bits: E,NE,N,NW,W,SW,S,SE. Bit 3: actual movement.
        public readonly NetworkVariable<byte> Locomotion = new NetworkVariable<byte>(6);
        private SpriteRenderer visual;

        public void SetTown(bool value)
        {
            if (!IsServer || !IsSpawned || PlayerSlot.Value<0) return;
            Vector3 preferred=value ? CapWarmTown.MeetingCenter+new Vector3(-3+PlayerSlot.Value*2,-4,0) : new Vector3(-3+PlayerSlot.Value*2,-1,0);
            if(!TryFreePosition(preferred,value,value,out var p))return;
            movementResumeTime=Time.unscaledTime+CapLoadingScreen.MinimumVisibleSeconds+CapLoadingScreen.FadeSeconds;
            InTown.Value=value;InMeetingRoom.Value=value;ViewingMap.Value=false;TalkingToPolice.Value=false;
            ReadyToStart.Value=false;
            serverInput=Vector2.zero;Riding.Value=false;Locomotion.Value=6;
            GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(p,transform.rotation,transform.localScale);
            previousStep=currentStep=p;
        }
        public override void OnNetworkSpawn()
        {
            if(IsServer)
            {
                int taken=0;
                foreach(var other in FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None))
                    if(other!=this && other.IsSpawned && other.NetworkManager==NetworkManager && other.PlayerSlot.Value>=0)
                        taken |= 1 << other.PlayerSlot.Value;
                int available=-1;
                for(int i=0;i<4;i++)if((taken & (1<<i))==0){available=i;break;}
                if(available<0)
                {
                    Debug.LogWarning("[CAP] No player seat available; connection rejected.");
                    NetworkManager.DisconnectClient(OwnerClientId);
                    return;
                }
                PlayerSlot.Value=available;
                for(int i=0;i<CapPlayerProfile.Colors.Length;i++)if(CanUseColor(i)){ColorIndex.Value=i;break;}
                var lobbySpawn=new Vector3(-3f+available*2f,-1f,0f);
                if(TryFreePosition(lobbySpawn,false,false,out var freeSpawn))lobbySpawn=freeSpawn;
                transform.position=lobbySpawn;
            }
            PlayerSlot.OnValueChanged+=OnSlotChanged;
            ColorIndex.OnValueChanged+=OnSlotChanged;
            ApplySlotVisual();
            previousStep=currentStep=transform.position;
            if(IsOwner && !CapPlayerProfile.Verification && (CapPlayerProfile.SavedName.Length>0 || CapPlayerProfile.SavedColor>=0))
                RequestProfileRpc(new FixedString64Bytes(CapPlayerProfile.SavedName),CapPlayerProfile.SavedColor,true);
            Debug.Log($"[CAP] Player spawned owner={OwnerClientId} slot={PlayerSlot.Value} local={IsOwner} server={IsServer}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args = System.Environment.GetCommandLineArgs();
            smokeMove = System.Array.IndexOf(args, "--cap-test-move") >= 0;
            smokeTest = System.Array.IndexOf(args, "--cap-smoke") >= 0;
#endif
        }

        public override void OnNetworkDespawn()
        {
            PlayerSlot.OnValueChanged-=OnSlotChanged;
            ColorIndex.OnValueChanged-=OnSlotChanged;
            // The seat becomes free because the server only counts spawned players.
            if(visual!=null){Destroy(visual.gameObject);visual=null;}
            if(nameFont!=null)Destroy(nameFont);nameFont=null;nameStyle=null;
            base.OnNetworkDespawn();
        }
        private void OnSlotChanged(int previous,int current){ApplySlotVisual();}
        private void ApplySlotVisual()
        {
            int slot=PlayerSlot.Value;
            var rootRenderer=GetComponent<SpriteRenderer>();
            if(slot<0 || slot>=4 || ColorIndex.Value<0){rootRenderer.enabled=false;return;}
            var color=CapPlayerProfile.GetColor(ColorIndex.Value);
            rootRenderer.color=color;rootRenderer.enabled=visual==null;
            var art = CapWarmTown.Instance != null ? CapWarmTown.Instance.PlayerSprite(slot) : null;
            if (art != null)
            {
                rootRenderer.enabled = false;
                if(visual==null)
                {
                    var child = new GameObject("Player art");
                    child.transform.SetParent(transform, false);
                    visual = child.AddComponent<SpriteRenderer>();
                }
                visual.sprite = art;
                visual.sharedMaterial=CapWarmTown.Instance.ArtMaterial;
                // Counter the original prefab's non-uniform rectangle scale.
                float target = CapWarmTown.ReferencePlayerHeight / art.bounds.size.y;
                visual.transform.localScale = new Vector3(target * .67f / transform.localScale.x, target / transform.localScale.y, 1);
                visual.color = color;
            }
        }

        public bool CanUseColor(int color)
        {
            if(color<0 || color>=CapPlayerProfile.Colors.Length)return false;
            foreach(var other in FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None))
                if(other!=this && other.IsSpawned && other.NetworkManager==NetworkManager && other.ColorIndex.Value==color)return false;
            return true;
        }
        public void ApplyProfile(string name,int color)
        {
            if(!IsOwner || !IsSpawned)return;
            CapPlayerProfile.Status="프로필 적용 중…";
            RequestProfileRpc(new FixedString64Bytes(CapPlayerProfile.CleanName(name)),color,false);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestProfileRpc(FixedString64Bytes name,int color,bool joining)
        {
            // The host arbitrates simultaneous requests so two players cannot claim one color.
            if(!CanUseColor(color))
            {
                if(!joining){ProfileResultRpc(false,Nickname.Value,ColorIndex.Value);return;}
                Nickname.Value=new FixedString64Bytes(CapPlayerProfile.CleanName(name.ToString()));
                return; // Saved color already occupied: keep the automatic free color.
            }
            Nickname.Value=new FixedString64Bytes(CapPlayerProfile.CleanName(name.ToString()));
            ColorIndex.Value=color;
            if(!joining)ProfileResultRpc(true,Nickname.Value,color);
        }
        [Rpc(SendTo.Owner)]
        private void ProfileResultRpc(bool success,FixedString64Bytes name,int color)
        {
            if(success){if(!CapPlayerProfile.Verification)CapPlayerProfile.Save(name.ToString(),color);CapPlayerProfile.Status="이름과 색상을 저장했습니다.";}
            else CapPlayerProfile.Status="다른 플레이어가 선택한 색상입니다. 다른 색상을 골라 주세요.";
        }

        private float nextChatTime;
        public void SendChat(string text)
        {
            if(IsOwner && IsSpawned)
            {
                string clean=CapChat.CleanMessage(text);
                if(clean.Length>0)SendChatRpc(new FixedString512Bytes(clean));
            }
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SendChatRpc(FixedString512Bytes text)
        {
            if(Time.unscaledTime<nextChatTime)return;
            string clean=CapChat.CleanMessage(text.ToString());
            if(clean.Length==0)return;
            nextChatTime=Time.unscaledTime+.45f;
            // Sender identity comes from server state, never from client-provided display text.
            ReceiveChatRpc(new FixedString128Bytes(PlayerLabel),new FixedString512Bytes(clean));
        }
        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void ReceiveChatRpc(FixedString128Bytes sender,FixedString512Bytes text)
        {
            CapChat.Instance?.Receive(sender.ToString(),text.ToString());
        }

        private Font nameFont;
        private GUIStyle nameStyle;
        private void OnGUI()
        {
            if(!IsSpawned || !IsOwner || CapOptions.IsOpen || CapLoadingScreen.Blocking || CapPoliceNpc.ModalOpen || (CapWarmTown.Instance!=null && CapWarmTown.Instance.Overview))return;
            var camera=Camera.main;if(camera==null)return;
            if(nameStyle==null)
            {
                nameFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},18);
                nameStyle=new GUIStyle(GUI.skin.label){font=nameFont,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,richText=false,normal={textColor=Color.white}};
            }
            var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;
            try
            {
                GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.depth=100;
                float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);nameStyle.fontSize=Mathf.Max(10,Mathf.RoundToInt(17*scale));
                var players=FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None);
                foreach(var player in players)if(player!=this)DrawName(player,camera,scale);
                DrawName(this,camera,scale); // Own nameplate also stays on top when stacked.
            }
            finally{GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;}
        }
        private void DrawName(CapNetworkPlayer player,Camera camera,float scale)
        {
            if(!player.IsSpawned || player.NetworkManager!=NetworkManager || !SameSpace(player))return;
            var renderer=player.visual!=null?player.visual:player.GetComponent<SpriteRenderer>();
            if(renderer==null || !renderer.enabled)return;
            var anchor=camera.WorldToScreenPoint(new Vector3(renderer.bounds.center.x,renderer.bounds.max.y,renderer.bounds.center.z));
            if(anchor.z<=0 || anchor.x<0 || anchor.x>Screen.width || anchor.y<0 || anchor.y>Screen.height)return;
            string name=player.PlayerLabel+(player==this?" · 나":"");
            float width=nameStyle.CalcSize(new GUIContent(name)).x+16*scale;
            var area=new Rect(anchor.x-width/2,Screen.height-anchor.y-30*scale,width,25*scale);
            GUI.color=new Color(.03f,.06f,.1f,.85f);GUI.DrawTexture(area,Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(area,name,nameStyle);
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if(!InTown.Value)mapRequested=false;
            if (IsOwner)
            {
                Vector2 input = Vector2.zero;
                var k = Keyboard.current;
                bool dialogueInput=CapPoliceNpc.Instance!=null && CapPoliceNpc.Instance.HandleInput(this);
                if (!dialogueInput && Application.isFocused && k != null)
                {
                    if(CapControls.Pressed(CapAction.Bicycle)) ToggleBicycle();
                    if(CapControls.Pressed(CapAction.Interact) &&
                        !(CapPoliceNpc.Instance!=null && CapPoliceNpc.Instance.TryInteract(this))) InteractDoor();
                    input=CapControls.Movement();
                }
                if (smokeMove) input = new Vector2(Mathf.Sin(Time.unscaledTime), Mathf.Cos(Time.unscaledTime));
                if (TestInput.HasValue) input=TestInput.Value;
                if(CapControls.Blocked || TalkingToPolice.Value || mapRequested || ViewingMap.Value || (CapWarmTown.Instance!=null && CapWarmTown.Instance.Overview))input=Vector2.zero;
                input=Vector2.ClampMagnitude(input,1);
                if(input!=sentInput || Time.unscaledTime>=nextSend)
                {
                    sentInput=input; nextSend=Time.unscaledTime+1f/30f;
                    SubmitInputRpc(input);
                }
            }
            if (smokeTest && Time.unscaledTime >= nextLog)
            {
                nextLog = Time.unscaledTime + 2;
                Debug.Log($"[CAP-SMOKE] localClient={NetworkManager.LocalClientId} owner={OwnerClientId} pos={transform.position}");
            }
        }

        // The owner requests map mode; the host enforces the movement lock.
        public void SetMapViewing(bool open) {
            if(!IsSpawned || !IsOwner)return;
            mapRequested=open && InTown.Value && !InMeetingRoom.Value;sentInput=Vector2.zero;nextSend=0;
            SetMapViewingRpc(mapRequested);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SetMapViewingRpc(bool open) {
            ViewingMap.Value=open && InTown.Value && !InMeetingRoom.Value && !TalkingToPolice.Value;
            serverInput=Vector2.zero;Locomotion.Value=(byte)(Locomotion.Value&7);
            lastInputTime=Time.unscaledTime;
        }

        public void InteractDoor()
        {
            if(IsSpawned && IsOwner && !CapControls.Blocked && !mapRequested && !ViewingMap.Value) InteractDoorRpc();
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void InteractDoorRpc()
        {
            var town=CapWarmTown.Instance;
            if(Time.unscaledTime<movementResumeTime || !InTown.Value || ViewingMap.Value || TalkingToPolice.Value || town==null || Time.unscaledTime<nextDoorTime || !town.NearMeetingDoor(this))return;
            nextDoorTime=Time.unscaledTime+.6f;
            bool entering=!InMeetingRoom.Value;
            Vector3 preferred=entering ? CapWarmTown.MeetingSpawn : town.StudentDoor+Vector3.down*.6f;
            if(!TryFreePosition(preferred,true,entering,out var destination))return;
            movementResumeTime=Time.unscaledTime+CapLoadingScreen.MinimumVisibleSeconds+CapLoadingScreen.FadeSeconds;
            InMeetingRoom.Value=entering;ViewingMap.Value=false;Riding.Value=false;
            serverInput=Vector2.zero;Locomotion.Value=6;lastInputTime=Time.unscaledTime;

            GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(destination,transform.rotation,transform.localScale);
            previousStep=currentStep=destination;
        }

        public void ToggleBicycle()
        {
            if(IsSpawned && IsOwner && !CapControls.Blocked && InTown.Value && !mapRequested && !ViewingMap.Value) ToggleBicycleRpc();
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ToggleBicycleRpc()
        {
            if(Time.unscaledTime<movementResumeTime || !InTown.Value || InMeetingRoom.Value || ViewingMap.Value || TalkingToPolice.Value || Time.unscaledTime<nextBikeToggle) return;
            nextBikeToggle=Time.unscaledTime+.3f;
            Riding.Value=!Riding.Value;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitInputRpc(Vector2 input)
        {
            if (float.IsNaN(input.x) || float.IsNaN(input.y) || float.IsInfinity(input.x) || float.IsInfinity(input.y)) return;
            serverInput = ViewingMap.Value || TalkingToPolice.Value || Time.unscaledTime<movementResumeTime ? Vector2.zero : Vector2.ClampMagnitude(input, 1);
            lastInputTime = Time.unscaledTime;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            previousStep=currentStep;
            // NetworkTransform teleports (including development tools) must not interpolate across the map.
            if((transform.position-currentStep).sqrMagnitude>.01f) previousStep=transform.position;
            if (ViewingMap.Value || TalkingToPolice.Value || Time.unscaledTime<movementResumeTime || Time.unscaledTime - lastInputTime > .25f) serverInput = Vector2.zero;
            float movementSpeed=InTown.Value ? (Riding.Value?9:5) : speed;
            var p=MoveInEnvironment(transform.position,serverInput*movementSpeed*Time.fixedDeltaTime);
            UpdateLocomotion(p-transform.position);
            transform.position = p;
            currentStep=p;
        }

        private void UpdateLocomotion(Vector2 movement)
        {
            if(movement.sqrMagnitude<.0000001f) { Locomotion.Value=(byte)(Locomotion.Value&7); return; }
            int direction=(Mathf.RoundToInt(Mathf.Atan2(movement.y,movement.x)/(Mathf.PI/4))+8)%8;
            Locomotion.Value=(byte)(direction|8);
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            var sr = visual != null ? visual : GetComponent<SpriteRenderer>();
            if(visual!=null)
            {
                // Clients already receive interpolated NetworkTransform poses. Only the server needs render interpolation.
                visual.transform.position=IsServer ? Vector3.Lerp(previousStep,currentStep,Mathf.Clamp01((Time.time-Time.fixedTime)/Time.fixedDeltaTime)) : transform.position;
            }
            var local=NetworkManager.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>();
            sr.enabled=local!=null && SameSpace(local);
            // Local rendering only: your own avatar appears above teammates.
            sr.sortingOrder = IsOwner ? 10000 : InTown.Value ? CapWarmTown.Depth(RenderPosition.y) : 10;
        }
    }
}
