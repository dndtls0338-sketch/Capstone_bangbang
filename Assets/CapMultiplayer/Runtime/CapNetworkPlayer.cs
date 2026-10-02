using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cap.Multiplayer
{
    // Server-authoritative movement with shared town footprint collision.
    [DefaultExecutionOrder(100)]
    public sealed class CapNetworkPlayer : NetworkBehaviour
    {
        [SerializeField] private float speed = 3.5f;
        public static Vector2? TestInput; // Development test input; null during normal play.
        private bool mapRequested;
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
        public readonly NetworkVariable<bool> InTown = new NetworkVariable<bool>(false);
        // Low 3 bits: E,NE,N,NW,W,SW,S,SE. Bit 3: actual movement.
        public readonly NetworkVariable<byte> Locomotion = new NetworkVariable<byte>(6);
        private SpriteRenderer visual;

        public void SetTown(bool value)
        {
            if (!IsServer || !IsSpawned) return;
            InTown.Value = value;
            ViewingMap.Value=false;
            serverInput = Vector2.zero;
            Riding.Value=false;
            Locomotion.Value=6;
            int slot = (int)(OwnerClientId % 4);
            Vector3 p = value ? CapWarmTown.Spawn(slot) : new Vector3(-3 + slot * 2, -1, 0);
            GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(p, transform.rotation, transform.localScale);
            previousStep=currentStep=p;
        }

        public override void OnNetworkSpawn()
        {
            int slot = (int)(OwnerClientId % 4);
            Color[] colors = { new Color(1f,.65f,.28f), new Color(.3f,.7f,1f),
                new Color(.45f,.9f,.6f), new Color(.95f,.45f,.7f) };
            GetComponent<SpriteRenderer>().color = colors[slot];
            if (IsServer) transform.position = new Vector3(-3f + slot * 2f, -1f, 0f);
            previousStep=currentStep=transform.position;
            var art = CapWarmTown.Instance != null ? CapWarmTown.Instance.PlayerSprite(slot) : null;
            if (art != null)
            {
                GetComponent<SpriteRenderer>().enabled = false;
                var child = new GameObject("Player art");
                child.transform.SetParent(transform, false);
                visual = child.AddComponent<SpriteRenderer>(); visual.sprite = art;
                visual.sharedMaterial=CapWarmTown.Instance.ArtMaterial;
                // Counter the original prefab's non-uniform rectangle scale.
                float target = CapWarmTown.ReferencePlayerHeight / art.bounds.size.y;
                child.transform.localScale = new Vector3(target * .67f / transform.localScale.x, target / transform.localScale.y, 1);
                visual.color = colors[slot];
            }
            if (IsServer)
                foreach (var other in FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None))
                    if (other != this && other.IsSpawned && other.InTown.Value) { SetTown(true); break; }
            Debug.Log($"[CAP] Player spawned owner={OwnerClientId} local={IsOwner} server={IsServer}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args = System.Environment.GetCommandLineArgs();
            smokeMove = System.Array.IndexOf(args, "--cap-test-move") >= 0;
            smokeTest = System.Array.IndexOf(args, "--cap-smoke") >= 0;
#endif
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if(!InTown.Value)mapRequested=false;
            if (IsOwner)
            {
                Vector2 input = Vector2.zero;
                var k = Keyboard.current;
                if (Application.isFocused && k != null)
                {
                    if(k.bKey.wasPressedThisFrame) ToggleBicycle();
                    input.x = ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1 : 0)
                            - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1 : 0);
                    input.y = ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1 : 0)
                            - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1 : 0);
                }
                if (smokeMove) input = new Vector2(Mathf.Sin(Time.unscaledTime), Mathf.Cos(Time.unscaledTime));
                if (TestInput.HasValue) input=TestInput.Value;
                if(mapRequested || ViewingMap.Value || (CapWarmTown.Instance!=null && CapWarmTown.Instance.Overview))input=Vector2.zero;
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
            mapRequested=open && InTown.Value;sentInput=Vector2.zero;nextSend=0;
            SetMapViewingRpc(mapRequested);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SetMapViewingRpc(bool open) {
            ViewingMap.Value=open && InTown.Value;
            serverInput=Vector2.zero;Locomotion.Value=(byte)(Locomotion.Value&7);
            lastInputTime=Time.unscaledTime;
        }

        public void ToggleBicycle()
        {
            if(IsSpawned && IsOwner && InTown.Value && !mapRequested && !ViewingMap.Value) ToggleBicycleRpc();
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ToggleBicycleRpc()
        {
            if(!InTown.Value || ViewingMap.Value || Time.unscaledTime<nextBikeToggle) return;
            nextBikeToggle=Time.unscaledTime+.3f;
            Riding.Value=!Riding.Value;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitInputRpc(Vector2 input)
        {
            if (float.IsNaN(input.x) || float.IsNaN(input.y) || float.IsInfinity(input.x) || float.IsInfinity(input.y)) return;
            serverInput = ViewingMap.Value ? Vector2.zero : Vector2.ClampMagnitude(input, 1);
            lastInputTime = Time.unscaledTime;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            previousStep=currentStep;
            // NetworkTransform teleports (including development tools) must not interpolate across the map.
            if((transform.position-currentStep).sqrMagnitude>.01f) previousStep=transform.position;
            if (ViewingMap.Value || Time.unscaledTime - lastInputTime > .25f) serverInput = Vector2.zero;
            if (InTown.Value && CapWarmTown.Instance != null)
            {
                var next=CapWarmTown.Instance.Move(transform.position, serverInput * (Riding.Value?9:5) * Time.fixedDeltaTime);
                UpdateLocomotion(next-transform.position);
                transform.position = next;
                currentStep=next;
                return;
            }
            var p = transform.position + (Vector3)(serverInput * speed * Time.fixedDeltaTime);
            p.x = Mathf.Clamp(p.x, -7.2f, 7.2f);
            p.y = Mathf.Clamp(p.y, -3.8f, 2.4f);
            p.z = 0;
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
            sr.sortingOrder = InTown.Value ? CapWarmTown.Depth(RenderPosition.y) : 10;
        }
    }
}
