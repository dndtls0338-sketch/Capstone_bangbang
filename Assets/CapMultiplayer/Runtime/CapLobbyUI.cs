using Unity.Netcode;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed class CapLobbyUI : MonoBehaviour
    {
        private string code = "";
        private bool localTools;
        private GUIStyle title, normal, small, button, field;
        private Font font;

        private void PrepareStyles()
        {
            if (normal != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 20);
            normal = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true };
            small = new GUIStyle(normal) { fontSize = 14 };
            title = new GUIStyle(normal) { fontSize = 28, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18 };
            field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 22 };
        }

        private void OnGUI()
        {
            if (CapWarmTown.Instance != null && CapWarmTown.Instance.InTown) return;
            var connection = CapRelaySession.Instance;
            if (connection == null) return;
            PrepareStyles();
            float scale = Mathf.Max(.45f, Mathf.Min(Screen.width / 1100f, Screen.height / 720f));
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            GUILayout.BeginArea(new Rect(24, 20, 460, 655), GUI.skin.box);
            GUILayout.Label("CAP  /  탐정단 대기실", title);
            GUILayout.Label("Unity 2D · 방장 + Relay · 1~4명 시작 가능", small);
            GUILayout.Space(14);
            GUILayout.Label(connection.Status, normal, GUILayout.MinHeight(70));
            bool listening = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            GUI.enabled = !connection.Busy;
            if (!connection.Connected && !listening)
            {
                if (!connection.CloudLinked)
                    GUILayout.Label("인터넷 테스트 준비: Unity의 Project Settings > Services에서 Cloud 프로젝트를 연결하세요.", small);
                GUILayout.Space(12);
                if (GUILayout.Button("인터넷 방 만들기", button, GUILayout.Height(48))) connection.CreateRoom();
                GUILayout.Space(16);
                GUILayout.Label("친구의 참가 코드", normal);
                code = GUILayout.TextField(code, 12, field, GUILayout.Height(38));
                if (GUILayout.Button("코드로 인터넷 방 참가", button, GUILayout.Height(48))) connection.JoinRoom(code);
                GUILayout.Space(18);
                localTools = GUILayout.Toggle(localTools, " 개발용 로컬 테스트", small);
                if (localTools)
                {
                    GUILayout.Label("동일 PC 테스트용입니다. 인터넷 연결과 별개입니다.", small);
                    if (GUILayout.Button("로컬 Host", button)) connection.StartLocal(true);
                    if (GUILayout.Button("로컬 Client", button)) connection.StartLocal(false);
                }
            }
            else
            {
                string role = NetworkManager.Singleton.IsHost ? "방장" : "참가자";
                int count = FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Length;
                GUILayout.Label($"{role}  ·  현재 {count} / 4명", normal);
                GUILayout.Label("참가 코드: " + (string.IsNullOrEmpty(connection.JoinCode) ? "로컬 테스트" : connection.JoinCode), title);
                if (!string.IsNullOrEmpty(connection.JoinCode) && GUILayout.Button("참가 코드 복사", button, GUILayout.Height(42)))
                    GUIUtility.systemCopyBuffer = connection.JoinCode;
                GUILayout.Space(12);
                if (NetworkManager.Singleton.IsHost)
                {
                    GUILayout.Label("혼자여도 바로 시작할 수 있어요. 친구는 나중에 참가할 수 있습니다.", small);
                    GUI.enabled = !connection.Busy && CapWarmTown.CanStart;
                    if (GUILayout.Button("게임 시작 · 마을로 이동", button, GUILayout.Height(48))) CapWarmTown.StartForAll(true);
                    GUI.enabled = !connection.Busy;
                }
                else GUILayout.Label("방장이 시작하면 함께 마을로 이동합니다. 4명을 기다릴 필요는 없습니다.", small);
                GUILayout.Label("WASD / 방향키로 이동\n서로 다른 색의 캐릭터가 함께 움직이는지 확인하세요.\n방장이 나가면 연결이 종료됩니다.", normal);
                if (GUILayout.Button("나가기", button, GUILayout.Height(44))) connection.LeaveRoom();
            }
            GUI.enabled = true;
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private void OnDestroy() { if (font != null) Destroy(font); }
    }
}
