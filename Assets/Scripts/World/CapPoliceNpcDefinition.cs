using UnityEngine;

namespace Cap.Multiplayer
{
    [CreateAssetMenu(menuName="CAP/Police NPC",fileName="PoliceNpc")]
    public sealed class CapPoliceNpcDefinition : ScriptableObject
    {
        public string displayName="경찰관";
        [Tooltip("월드에서 보이는 캐릭터. 비워 두면 기본 도형 캐릭터를 사용합니다.")]
        public Sprite worldSprite;
        [Tooltip("대화창 오른쪽 초상화 PNG 이미지. 비워 두면 기본 도형 캐릭터를 사용합니다.")]
        public Texture2D portrait;
        [Min(.2f)] public float worldHeight=1.8f;
        [Min(1.5f)] public float interactionDistance=2.6f;
        [TextArea(2,5)] public string[] lines={
            "안녕하세요. 경찰서 앞을 지키고 있는 경찰관입니다.\n무슨 일로 오셨나요?",
            "주변에서 수상한 일이나 도움이 필요한 사람을 보셨다면\n경찰서에 알려 주세요.",
            "조심해서 다녀오세요. 도움이 필요하면 다시 말을 걸어 주세요."
        };
    }
}
