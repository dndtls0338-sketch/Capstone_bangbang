using Unity.Netcode;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed partial class CapNetworkPlayer
    {
        public readonly NetworkVariable<int> InvestigatingEvidence=new NetworkVariable<int>(-1);
        public bool InDialogue=>TalkingToPolice.Value || InvestigatingEvidence.Value>=0;
        private uint evidenceRequestId;

        public bool TryInteractEvidence()
        {
            var world=CapEvidenceWorld.Instance;
            if(!IsSpawned || !IsOwner || CapControls.Blocked || mapRequested || ViewingMap.Value || world==null || CapPoliceNpc.Instance==null)return false;
            return world.FindNearby(this,out int index) && CapPoliceNpc.Instance.TryInteractEvidence(this,index);
        }

        public void RequestEvidenceDialogue(int index,uint round,uint requestId)
        {if(IsSpawned && IsOwner)BeginEvidenceDialogueRpc(index,round,requestId);}

        [Rpc(SendTo.Server, InvokePermission=RpcInvokePermission.Owner)]
        private void BeginEvidenceDialogueRpc(int index,uint round,uint requestId)
        {
            var result=TryBeginEvidenceDialogue(index,round,requestId);
            EvidenceDialogueReplyRpc(requestId,result);
        }

        internal PoliceDialogueResult TryBeginEvidenceDialogue(int index,uint round,uint requestId)
        {
            if(!IsServer || !IsSpawned || InDialogue || ViewingMap.Value || Time.unscaledTime<movementResumeTime || CapEvidenceWorld.Instance==null)
                return PoliceDialogueResult.Unavailable;
            var result=CapEvidenceWorld.Instance.Reserve(this,index,round);
            if(result==PoliceDialogueResult.Granted)
            {
                InvestigatingEvidence.Value=index;evidenceRequestId=requestId;
                serverInput=Vector2.zero;Locomotion.Value=(byte)(Locomotion.Value&7);lastInputTime=Time.unscaledTime;
            }
            return result;
        }

        [Rpc(SendTo.Owner, InvokePermission=RpcInvokePermission.Server)]
        private void EvidenceDialogueReplyRpc(uint requestId,PoliceDialogueResult result)
        {CapPoliceNpc.Instance?.ReceiveEvidenceReply(this,requestId,result);}

        public void EndEvidenceDialogue(uint requestId,bool collect)
        {if(IsSpawned && IsOwner)EndEvidenceDialogueRpc(requestId,collect);}

        [Rpc(SendTo.Server, InvokePermission=RpcInvokePermission.Owner)]
        private void EndEvidenceDialogueRpc(uint requestId,bool collect)
        {
            if(requestId!=evidenceRequestId || InvestigatingEvidence.Value<0)return;
            CapEvidenceWorld.Instance?.Release(this,collect);
            InvestigatingEvidence.Value=-1;serverInput=Vector2.zero;lastInputTime=Time.unscaledTime;
        }
    }
}
