using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cap.Multiplayer
{
    public enum CapAction { Up, Down, Left, Right, Map, Bicycle, PushToTalk, Mute, Interact }

    // One source of truth for input, option labels and saved keyboard bindings.
    public static class CapControls
    {
        private static readonly Key[] Defaults = { Key.W, Key.S, Key.A, Key.D, Key.M, Key.B, Key.V, Key.N, Key.E };
        public static readonly string[] Names = { "위로 이동", "아래로 이동", "왼쪽 이동", "오른쪽 이동", "지도 열기 / 닫기", "자전거 타기 / 내리기", "눌러서 말하기", "마이크 음소거", "문 열기 / 상호작용" };
        private static readonly Key[] keys = new Key[9];
        private static bool loaded;
        private static int escapeFrame=-1;
        public static bool EscapeConsumed => escapeFrame==Time.frameCount;
        public static void ConsumeEscape(){escapeFrame=Time.frameCount;}
        public static bool Blocked => CapChat.InputBlocked || CapOptions.IsOpen || CapLobbyUI.ProfileEditing || CapLoadingScreen.Blocking || CapPoliceNpc.ModalOpen || CapCaseNews.ModalOpen;
        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            for (int i=0;i<keys.Length;i++) keys[i]=Defaults[i];
            var candidate=new Key[9];
            for(int i=0;i<keys.Length;i++) candidate[i]=(Key)PlayerPrefs.GetInt("Cap.Key."+(CapAction)i,(int)Defaults[i]);
            if(Array.IndexOf(candidate,candidate[8])<8)
                foreach(Key key in Enum.GetValues(typeof(Key)))
                    if(Allowed(key) && Array.IndexOf(candidate,key)<0){candidate[8]=key;break;}
            for(int i=0;i<keys.Length;i++)
                if(!Allowed(candidate[i]) || Array.IndexOf(candidate,candidate[i])!=i) return;
            Array.Copy(candidate,keys,keys.Length);
        }
        public static Key Get(CapAction action) { Load(); return keys[(int)action]; }
        public static string Label(CapAction action) => Get(action).ToString();
        public static bool Held(CapAction action) => !Blocked && Keyboard.current!=null && Keyboard.current[Get(action)].isPressed;
        public static bool Pressed(CapAction action) => !Blocked && Keyboard.current!=null && Keyboard.current[Get(action)].wasPressedThisFrame;
        public static bool Allowed(Key key) => Enum.IsDefined(typeof(Key),key) && key!=Key.None && key!=Key.Escape && key!=Key.Enter && key!=Key.NumpadEnter && key!=Key.LeftWindows && key!=Key.RightWindows && key!=Key.LeftAlt && key!=Key.RightAlt && key!=Key.PrintScreen;
        public static bool Bind(CapAction action,Key key,out string message)
        {
            Load();
            if(!Allowed(key)){message="이 키는 사용할 수 없습니다. Esc는 옵션 / 취소, Enter는 채팅입니다.";return false;}
            int other=Array.IndexOf(keys,key);
            if(other>=0 && other!=(int)action){message=Names[other]+"에 사용 중인 키입니다.";return false;}
            keys[(int)action]=key;Save();message="변경되었습니다.";return true;
        }
        public static void Reset() { loaded=true;Array.Copy(Defaults,keys,keys.Length);Save(); }
        private static void Save() { for(int i=0;i<keys.Length;i++)PlayerPrefs.SetInt("Cap.Key."+(CapAction)i,(int)keys[i]);PlayerPrefs.Save(); }
        public static Vector2 Movement() => new Vector2((Held(CapAction.Right)?1:0)-(Held(CapAction.Left)?1:0),(Held(CapAction.Up)?1:0)-(Held(CapAction.Down)?1:0));
    }
}
