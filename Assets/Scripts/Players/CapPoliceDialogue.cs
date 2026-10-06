using Unity.Netcode;
using UnityEngine;

namespace Cap.Multiplayer
{
    public enum PoliceDialogueResult : byte { Granted, Busy, Unavailable }

    public sealed partial class CapNetworkPlayer
    {
        // A server-owned flag on the existing player avoids a separate spawned NPC prefab.
        // Despawn releases the seat automatically: only spawned players are considered.
        public readonly NetworkVariable<bool> TalkingToPolice=new NetworkVariable<bool>(false);

        public void RequestPoliceDialogue(uint requestId)
        {
            if(IsSpawned && IsOwner)BeginPoliceDialogueRpc(requestId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void BeginPoliceDialogueRpc(uint requestId)
        {
            PoliceDialogueReplyRpc(requestId,TryBeginPoliceDialogue());
        }

        internal PoliceDialogueResult TryBeginPoliceDialogue()
        {
            var npc=CapPoliceNpc.Instance;
            if(!IsServer || !IsSpawned || !InTown.Value || InMeetingRoom.Value || ViewingMap.Value || InDialogue ||
                Time.unscaledTime<movementResumeTime || npc==null || !npc.IsNear(this))
                return PoliceDialogueResult.Unavailable;
            // RPCs execute sequentially on the server; claiming the flag is atomic here.
            foreach(var other in FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None))
                if(other!=this && other.IsSpawned && other.NetworkManager==NetworkManager && other.TalkingToPolice.Value)
                    return PoliceDialogueResult.Busy;
            TalkingToPolice.Value=true;
            serverInput=Vector2.zero;
            Locomotion.Value=(byte)(Locomotion.Value&7);
            lastInputTime=Time.unscaledTime;
            return PoliceDialogueResult.Granted;
        }

        [Rpc(SendTo.Owner)]
        private void PoliceDialogueReplyRpc(uint requestId,PoliceDialogueResult result)
        {
            CapPoliceNpc.Instance?.ReceiveReply(this,requestId,result);
        }

        public void EndPoliceDialogue()
        {
            if(IsSpawned && IsOwner)EndPoliceDialogueRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void EndPoliceDialogueRpc()
        {
            TalkingToPolice.Value=false;
            serverInput=Vector2.zero;
            lastInputTime=Time.unscaledTime;
        }
    }
}
