using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed class CapRelaySession : MonoBehaviour
    {
        public static CapRelaySession Instance { get; private set; }
        public bool Busy { get; private set; }
        public string Status { get; private set; } = "방을 만들거나 참가 코드를 입력하세요.";
        public string JoinCode => session?.Code ?? "";
        public string VoiceRoom => session==null ? "" : "cap_"+session.Id;
        public bool Connected => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
        public bool CloudLinked => !string.IsNullOrWhiteSpace(Application.cloudProjectId) &&
            Application.cloudProjectId != "00000000-0000-0000-0000-000000000000";
        private ISession session;
        private bool wasConnected;

        private void Awake()
        {
            Instance = this;
            Application.runInBackground = true;
        }

        private Task authenticationTask;
        // Device options and room connection share one initialization/sign-in operation.
        public Task EnsureServicesAsync()
        {
            if(authenticationTask==null || authenticationTask.IsCompleted)authenticationTask=AuthenticateAsync();
            return authenticationTask;
        }
        private async Task AuthenticateAsync()
        {
            if (!CloudLinked)
                throw new InvalidOperationException("Unity Cloud 연결이 필요합니다. Edit > Project Settings > Services에서 프로젝트를 연결하세요.");
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // Separate authentication profiles let Editor and multiple desktop builds coexist.
                var options = new InitializationOptions().SetProfile(
                    "cap_" + System.Diagnostics.Process.GetCurrentProcess().Id);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public async void CreateRoom()
        {
            if (Busy || Connected || session != null) return;
            Busy = true;
            CapLoadingScreen.Show("방을 만드는 중");
            Status = "Relay 방을 만드는 중...";
            try
            {
                await EnsureServicesAsync();
                session = await MultiplayerService.Instance.CreateSessionAsync(
                    new SessionOptions { Name = "cap", MaxPlayers = 4, IsPrivate = true }.WithRelayNetwork());
                Status = "방이 생성되었습니다. 참가 코드를 친구에게 전달하세요.";
            }
            catch (Exception e) { await RecoverAsync(e); }
            finally { Busy = false; }
        }

        public async void JoinRoom(string code)
        {
            if (Busy || Connected || session != null) return;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) { Status = "참가 코드를 입력하세요."; return; }
            Busy = true;
            CapLoadingScreen.Show("방에 참가하는 중");
            Status = "방에 연결하는 중...";
            try
            {
                await EnsureServicesAsync();
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                Status = "접속했습니다. WASD 또는 방향키로 이동하세요.";
            }
            catch (Exception e) { await RecoverAsync(e); }
            finally { Busy = false; }
        }

        private async Task RecoverAsync(Exception error)
        {
            Debug.LogWarning("[CAP] Connection failed: " + error);
            string reason=NetworkManager.Singleton!=null ? NetworkManager.Singleton.DisconnectReason : "";
            await CleanupAsync();
            Status = "연결 실패: " + (string.IsNullOrWhiteSpace(reason) ? error.Message : reason);
        }

        private async Task CleanupAsync()
        {
            var old = session;
            session = null;
            wasConnected = false;
            try
            {
                if (old != null)
                {
                    if (old.IsHost) await old.AsHost().DeleteAsync();
                    else await old.LeaveAsync();
                }
            }
            catch (Exception e) { Debug.LogWarning("[CAP] Session cleanup: " + e.Message); }
            finally
            {
                var manager = NetworkManager.Singleton;
                if (manager != null && manager.IsListening) manager.Shutdown();
            }
        }

        public async void LeaveRoom()
        {
            if (Busy) return;
            Busy = true;
            CapLoadingScreen.Show("시작 화면으로 이동 중");
            Status = "연결을 종료하는 중...";
            try { await CleanupAsync(); Status = "연결을 종료했습니다."; }
            finally { Busy = false; }
        }

        private async void Update()
        {
            if (Connected) wasConnected = true;
            if (!wasConnected || Connected || Busy) return;
            Busy = true;
            try
            {
                await CleanupAsync();
                Status = "";
            }
            finally { Busy = false; }
        }

        // Explicitly separate local smoke tests from the real Relay buttons.
        public void StartLocal(bool host)
        {
            if (Busy || Connected || session != null || NetworkManager.Singleton.IsListening) return;
            var manager = NetworkManager.Singleton;
            manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 7777);
            bool started = host ? manager.StartHost() : manager.StartClient();
            Status = started ? "로컬 테스트 연결 중 (인터넷 Relay 연결 아님)" : "로컬 연결 시작 실패";
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
