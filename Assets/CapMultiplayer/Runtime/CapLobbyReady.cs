using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed partial class CapNetworkPlayer
    {
        public readonly NetworkVariable<bool> ReadyToStart=new NetworkVariable<bool>(false);
        // Only the host player's deadline is used; synchronized server time keeps the countdown aligned.
        public readonly NetworkVariable<double> StartAt=new NetworkVariable<double>(-1);
        private string readyRoster="";
        public bool CanStartFromLobby
        {
            get
            {
                if(!IsSpawned || !IsServer || !IsOwner || InTown.Value || StartAt.Value>=0)return false;
                var players=FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Where(p=>p.IsSpawned && p.NetworkManager==NetworkManager).ToArray();
                return players.Length>0 && players.Length==NetworkManager.ConnectedClientsIds.Count && players.All(p=>!p.InTown.Value&&p.ReadyToStart.Value);
            }
        }
        public void StartReadyCountdown()
        {
            // The start button runs on the host only. Recheck readiness instead of trusting the UI.
            if(CapControls.Blocked || !CanStartFromLobby)return;
            readyRoster=string.Join(",",NetworkManager.ConnectedClientsIds.OrderBy(id=>id));
            StartAt.Value=NetworkManager.ServerTime.Time+3;
        }
        public static CapNetworkPlayer LobbyHost=>FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).FirstOrDefault(p=>p.IsSpawned && p.OwnerClientId==Unity.Netcode.NetworkManager.ServerClientId);
        public void SetReady(bool ready)
        {
            if(IsSpawned && IsOwner && !InTown.Value && !CapControls.Blocked)SetReadyRpc(ready);
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void SetReadyRpc(bool ready)
        {
            if(InTown.Value || InMeetingRoom.Value)return;
            ReadyToStart.Value=ready;
            // Cancel immediately, even if another ready message arrives in the same frame.
            if(!ready && LobbyHost!=null)LobbyHost.StartAt.Value=-1;
        }
        private void TickLobbyReady()
        {
            if(!IsServer || OwnerClientId!=Unity.Netcode.NetworkManager.ServerClientId)return;
            if(InTown.Value){if(StartAt.Value>=0)StartAt.Value=-1;return;}
            var players=FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Where(p=>p.IsSpawned && p.NetworkManager==NetworkManager).ToArray();
            string roster=string.Join(",",NetworkManager.ConnectedClientsIds.OrderBy(id=>id));
            if(roster!=readyRoster)
            {
                // A departure must not unexpectedly launch the remaining players while someone reconnects.
                if(StartAt.Value>=0)foreach(var player in players)player.ReadyToStart.Value=false;
                readyRoster=roster;StartAt.Value=-1;return;
            }
            bool all=players.Length>0 && players.Length==NetworkManager.ConnectedClientsIds.Count && players.All(p=>!p.InTown.Value&&p.ReadyToStart.Value);
            if(!all){if(StartAt.Value>=0)StartAt.Value=-1;return;}
            if(StartAt.Value<0)return; // Being ready alone does not start the game.
            if(NetworkManager.ServerTime.Time>=StartAt.Value){StartAt.Value=-1;CapWarmTown.StartForAll(true);}
        }
    }
}
