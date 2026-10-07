using System;
using UnityEngine;

namespace Cap.Multiplayer
{
    [Serializable]
    public sealed class CapCaseNewsArticle
    {
        [Min(1)] public int roundNumber=1;
        public string headline="사건 뉴스 테스트";
        [TextArea(1,3)] public string summary="본 기사는 테스트용으로 작성되었습니다.";
        [TextArea(6,24)] public string body="현재 사건 뉴스 화면을 테스트하고 있습니다.\n\n실제 사건 내용은 추후 라운드별로 추가될 예정입니다.\n\n탐정단 여러분은 기사를 읽고 조사를 준비해 주세요.";
    }

    [CreateAssetMenu(menuName="CAP/Case News",fileName="CaseNews")]
    public sealed class CapCaseNewsDefinition : ScriptableObject
    {
        public string newspaperName="탐정 사건일보";
        public Color paperColor=new Color(.8f,.8f,.8f,1);
        [Tooltip("회의실 책상 위 신문 이미지. 비우면 뉴스 창과 같은 회색 신문을 표시합니다.")]
        public Sprite worldSprite;
        [Range(.5f,2)] public float worldWidth=1.6f;
        [Range(1.2f,2.2f)] public float interactionDistance=1.8f;
        [Tooltip("Round Number에 해당하는 기사를 표시합니다. 라운드 번호는 중복 없이 지정하세요.")]
        public CapCaseNewsArticle[] rounds={new CapCaseNewsArticle()};
        [Tooltip("아직 기사를 등록하지 않은 라운드에 표시할 테스트 기사입니다.")]
        public CapCaseNewsArticle fallback=new CapCaseNewsArticle();

        public CapCaseNewsArticle ForRound(int round)
        {
            if(rounds!=null)foreach(var article in rounds)
                if(article!=null && article.roundNumber==round)return article;
            return fallback??new CapCaseNewsArticle();
        }
    }
}
