using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Cap.Multiplayer
{
    public struct CapEvidenceRecord : INetworkSerializable,IEquatable<CapEvidenceRecord>
    {
        public Vector2 Position;
        // 0 = available, 1 = being examined, 2 = collected.
        public byte State;
        public ulong Examiner;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T:IReaderWriter
        {serializer.SerializeValue(ref Position);serializer.SerializeValue(ref State);serializer.SerializeValue(ref Examiner);}
        public bool Equals(CapEvidenceRecord other)=>Position.Equals(other.Position) && State==other.State && Examiner==other.Examiner;
    }

    public sealed class CapEvidenceWorld : NetworkBehaviour
    {
        public static CapEvidenceWorld Instance { get; private set; }
        public NetworkList<CapEvidenceRecord> Evidence;
        public readonly NetworkVariable<uint> Round=new NetworkVariable<uint>(0);
        public CapEvidenceDefinition Definition { get; private set; }
        private readonly List<SpriteRenderer> visuals=new List<SpriteRenderer>();
        private Sprite fallback;
        private bool roundActive,ownsDefinition;
        private void Awake(){Evidence=new NetworkList<CapEvidenceRecord>();}

        public override void OnNetworkSpawn()
        {
            Instance=this;
            Definition=Resources.Load<CapEvidenceDefinition>("Evidence/RandomEvidence");
            if(Definition==null){Definition=ScriptableObject.CreateInstance<CapEvidenceDefinition>();ownsDefinition=true;}
            fallback=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1);
        }

        public static void EnsureRound()
        {
            var manager=NetworkManager.Singleton;
            if(manager==null || !manager.IsServer || CapWarmTown.Instance==null)return;
            if(Instance==null || !Instance.IsSpawned)
            {
                var prefab=Resources.Load<GameObject>("Evidence/EvidenceWorld");
                if(prefab==null){Debug.LogError("[CAP-EVIDENCE] Missing EvidenceWorld prefab.");return;}
                Instantiate(prefab).GetComponent<NetworkObject>().Spawn();
            }
            if(Instance.roundActive)return;
            var world=Instance;world.roundActive=true;world.Round.Value++;
            world.Evidence.Clear();
            var positions=CapEvidencePlacement.Choose(CapWarmTown.Instance,world.Definition.spawnCount,world.Definition.minimumSpacing,world.Definition.WorldSize);
            foreach(var p in positions)world.Evidence.Add(new CapEvidenceRecord{Position=p});
            if(positions.Count<world.Definition.spawnCount)Debug.LogWarning("[CAP-EVIDENCE] Not enough reachable, separated positions; spawned "+positions.Count);
        }

        public bool CanReach(CapNetworkPlayer player,int index)
        {
            if(!IsSpawned || player==null || !player.IsSpawned || player.NetworkManager!=NetworkManager ||
                !player.InTown.Value || player.InMeetingRoom.Value || index<0 || index>=Evidence.Count || Evidence[index].State==2)return false;
            Vector2 p=Evidence[index].Position;
            return Vector2.Distance(player.transform.position,p)<=Mathf.Clamp(Definition.interactionDistance,.5f,3) &&
                CapEvidencePlacement.CanWalkStraight(CapWarmTown.Instance,player.transform.position,p);
        }

        public bool FindNearby(CapNetworkPlayer player,out int index)
        {
            index=-1;float nearest=float.MaxValue;
            for(int i=0;i<Evidence.Count;i++)
            {
                if(!CanReach(player,i))continue;
                float distance=((Vector2)player.transform.position-Evidence[i].Position).sqrMagnitude;
                if(distance<nearest){nearest=distance;index=i;}
            }
            return index>=0;
        }

        public PoliceDialogueResult Reserve(CapNetworkPlayer player,int index,uint round)
        {
            if(!IsServer || round!=Round.Value || !CanReach(player,index))return PoliceDialogueResult.Unavailable;
            var entry=Evidence[index];
            if(entry.State!=0)return PoliceDialogueResult.Busy;
            entry.State=1;entry.Examiner=player.OwnerClientId;Evidence[index]=entry;
            return PoliceDialogueResult.Granted;
        }

        public void Release(CapNetworkPlayer player,bool collect)
        {
            if(!IsServer || !IsSpawned || player==null)return;
            int index=player.InvestigatingEvidence.Value;
            if(index>=0 && index<Evidence.Count)
            {
                var entry=Evidence[index];
                if(entry.State==1 && entry.Examiner==player.OwnerClientId)
                {entry.State=(byte)(collect?2:0);entry.Examiner=0;Evidence[index]=entry;}
            }
            player.InvestigatingEvidence.Value=-1;
        }

        private void Update()
        {
            if(!IsSpawned || Definition==null)return;
            if(IsServer && roundActive)
            {
                bool playing=false;
                foreach(var player in FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None))
                    if(player.IsSpawned && player.NetworkManager==NetworkManager && player.InTown.Value){playing=true;break;}
                if(!playing){roundActive=false;Evidence.Clear();}
            }
            var local=NetworkManager.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>();
            bool visible=local!=null && local.InTown.Value && !local.InMeetingRoom.Value;
            while(visuals.Count<Evidence.Count)
            {
                var go=new GameObject("Random evidence "+visuals.Count);go.transform.SetParent(transform,false);
                var sr=go.AddComponent<SpriteRenderer>();sr.sharedMaterial=CapWarmTown.Instance.ArtMaterial;visuals.Add(sr);
            }
            for(int i=0;i<visuals.Count;i++)
            {
                var sr=visuals[i];sr.enabled=visible && i<Evidence.Count && Evidence[i].State!=2;
                if(i>=Evidence.Count)continue;
                sr.sprite=Definition.worldSprite!=null?Definition.worldSprite:fallback;
                sr.color=Definition.worldSprite!=null?Color.white:new Color(1,.85f,.32f);
                var bounds=sr.sprite.bounds;
                float size=Definition.WorldSize;
                float scale=size/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.y));
                var dimensions=new Vector3(scale*(Definition.worldSprite==null?.7f:1),scale,1);
                sr.transform.localScale=dimensions;
                sr.transform.position=(Vector3)Evidence[i].Position-Vector3.Scale(bounds.center,dimensions);
                sr.sortingOrder=CapWarmTown.Depth(Evidence[i].Position.y);
            }
        }

        public override void OnNetworkDespawn(){if(Instance==this)Instance=null;base.OnNetworkDespawn();}
        public override void OnDestroy()
        {
            if(Instance==this)Instance=null;
            if(fallback!=null)Destroy(fallback);
            if(ownsDefinition && Definition!=null)Destroy(Definition);
            base.OnDestroy();
        }
    }
}
