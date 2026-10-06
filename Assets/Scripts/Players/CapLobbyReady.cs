using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed partial class CapNetworkPlayer
    {
        public readonly NetworkVariable<bool> ReadyToStart=new NetworkVariable<bool>(false);
        public bool CanStartFromLobby
        {
            get
            {
                if(!IsSpawned || !IsServer || !IsOwner || InTown.Value)return false;
                var players=FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Where(p=>p.IsSpawned && p.NetworkManager==NetworkManager).ToArray();
                return players.Length>0 && players.Length==NetworkManager.ConnectedClientsIds.Count && players.All(p=>!p.InTown.Value&&p.ReadyToStart.Value);
            }
        }
        public void StartReadyGame()
        {
            // Only the host can start, and every connected player must still be ready.
            if(CapControls.Blocked || !CanStartFromLobby)return;
            CapWarmTown.StartForAll(true);
        }
        public void SetReady(bool ready)
        {
            if(IsSpawned && IsOwner && !InTown.Value && !CapControls.Blocked)SetReadyRpc(ready);
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void SetReadyRpc(bool ready)
        {
            if(InTown.Value || InMeetingRoom.Value)return;
            ReadyToStart.Value=ready;
        }
    }
}
