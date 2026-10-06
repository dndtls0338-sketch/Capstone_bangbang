using UnityEngine;

namespace Cap.Multiplayer
{
    [CreateAssetMenu(menuName="CAP/Random Evidence",fileName="RandomEvidence")]
    public sealed class CapEvidenceDefinition : ScriptableObject
    {
        public string displayName="증거";
        [Tooltip("언제든 교체할 수 있는 월드 이미지. 비워 두면 노란 쪽지로 표시합니다.")]
        public Sprite worldSprite;
        public Texture2D portrait;
        [Tooltip("플레이어 키 대비 이미지의 가장 긴 변. 기본값은 1/2입니다.")]
        [Range(.02f,5f)] public float sizeRatio=.5f;
        public float WorldSize=>CapWarmTown.ReferencePlayerHeight*Mathf.Clamp(sizeRatio,.02f,5f);
        [Range(1,64)] public int spawnCount=1;
        [Min(2)] public float minimumSpacing=4;
        [Range(.5f,3)] public float interactionDistance=1.5f;
        [TextArea(2,5)] public string[] lines={"TEST1"};
    }
}
