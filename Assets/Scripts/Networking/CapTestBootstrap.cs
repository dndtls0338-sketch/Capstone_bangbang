using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed class CapTestBootstrap : MonoBehaviour
    {
        private void Awake() { if(CapLoadingScreen.Instance==null)gameObject.AddComponent<CapLoadingScreen>(); if(CapCaseNews.Instance==null)gameObject.AddComponent<CapCaseNews>(); if (CapWarmTown.Instance == null) gameObject.AddComponent<CapWarmTown>(); if(CapOptions.Instance==null)gameObject.AddComponent<CapOptions>(); if(CapChat.Instance==null)gameObject.AddComponent<CapChat>(); if(CapVoiceChat.Instance==null)gameObject.AddComponent<CapVoiceChat>(); }
        private IEnumerator Start()
        {
            var manager = NetworkManager.Singleton;
            // Profile/seat NetworkVariables require the same wire format on every player.
            manager.NetworkConfig.ProtocolVersion = 9;
            manager.ConnectionApprovalCallback = (request, response) =>
            {
                bool space = manager.ConnectedClientsIds.Count < 4;
                bool started=false;
                foreach(var player in FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None))
                    if(player.IsSpawned && player.NetworkManager==manager && player.InTown.Value){started=true;break;}
                response.Approved = space && !started;
                response.CreatePlayerObject = response.Approved;
                response.Reason = started ? "이미 시작한 게임에는 참가할 수 없습니다. 대기실로 돌아온 뒤 다시 참가하세요." : space ? "" : "방이 가득 찼습니다 (최대 4명).";
                response.Pending = false;
            };
            manager.OnClientConnectedCallback += id => Debug.Log($"[CAP] Client connected: {id}");
            manager.OnClientDisconnectCallback += id => Debug.Log($"[CAP] Client disconnected: {id}");
            yield return null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "--cap-local-host") >= 0) CapRelaySession.Instance.StartLocal(true);
            if (System.Array.IndexOf(args, "--cap-local-client") >= 0) CapRelaySession.Instance.StartLocal(false);
            if (System.Array.IndexOf(args, "--cap-town-test") >= 0)
            {
                yield return new WaitForSecondsRealtime(5);
                CapWarmTown.StartForAll(true);
            }
            if (System.Array.IndexOf(args, "--cap-smoke") >= 0)
            {
                yield return new WaitForSecondsRealtime(16);
                CapRelaySession.Instance.LeaveRoom();
                yield return new WaitForSecondsRealtime(2);
                Application.Quit();
            }
#endif
        }
    }
}
