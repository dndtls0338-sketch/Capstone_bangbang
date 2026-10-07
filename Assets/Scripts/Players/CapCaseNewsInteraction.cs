using Unity.Netcode;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed partial class CapNetworkPlayer
    {
        public readonly NetworkVariable<int> CaseRound=new NetworkVariable<int>(1);
        public readonly NetworkVariable<uint> CaseNewsRevision=new NetworkVariable<uint>(0);
        public readonly NetworkVariable<bool> ReadingCaseNews=new NetworkVariable<bool>(false);

        // Called by the server at game start and by future round progression.
        public void ShowCaseNewsForRound(int round)
        {
            if(!IsSpawned || !IsServer || !InTown.Value)return;
            CapEvidenceWorld.Instance?.Release(this,false);
            TalkingToPolice.Value=false;ViewingMap.Value=false;
            CaseRound.Value=Mathf.Max(1,round);CaseNewsRevision.Value++;
            ReadingCaseNews.Value=true;StopForCaseNews();
        }

        public bool TryReadNewspaper()
        {
            if(!IsSpawned || !IsOwner || CapControls.Blocked || InDialogue || mapRequested || ViewingMap.Value ||
                CapCaseNewspaper.Instance==null || !CapCaseNewspaper.Instance.IsNear(this))return false;
            ReadNewspaperRpc();return true;
        }

        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void ReadNewspaperRpc()
        {
            if(InDialogue || ViewingMap.Value || Time.unscaledTime<movementResumeTime ||
                CapCaseNewspaper.Instance==null || !CapCaseNewspaper.Instance.IsNear(this))return;
            // Every reader has their own flag: the newspaper is not an exclusive NPC.
            CaseNewsRevision.Value++;ReadingCaseNews.Value=true;StopForCaseNews();
        }

        public void CloseCaseNews(uint revision)
        {if(IsSpawned && IsOwner)CloseCaseNewsRpc(revision);}

        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void CloseCaseNewsRpc(uint revision)
        {
            if(revision!=CaseNewsRevision.Value)return;
            ReadingCaseNews.Value=false;StopForCaseNews();
        }

        private void StopForCaseNews()
        {serverInput=Vector2.zero;Locomotion.Value=(byte)(Locomotion.Value&7);lastInputTime=Time.unscaledTime;}
    }
}
