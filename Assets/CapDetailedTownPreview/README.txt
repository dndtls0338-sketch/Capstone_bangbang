상세 에셋 배율 확인용 씬

열기: Unity 메뉴 CAP > 상세 에셋 > 확인용 씬 열기
또는 Scenes/DetailedTownPreview.unity 더블클릭 후 Play.

상단 Shops / Campus / Hospital / Park 버튼으로 위치를 바꿉니다.
WASD / 방향키는 배율 확인용 이동이며 충돌 처리는 없습니다.
Overview는 전체 배치를 축소해서 보는 검수용입니다.
플레이어 화면 높이는 기준 1534x1025에서 90px입니다.

Assembled/DetailedTown.prefab: 각각 선택/이동 가능한 건물과 소품 배치.
Assembled/DetailedTownAssets.asset: 분리된 Sprite 서브 에셋 60개.
Data: 분리 좌표와 부지/도로 치수.
sprites: 재생성한 투명 PNG 원본.

도로와 보도는 치수 확인용 단색이며 최종 바닥 아트가 아닙니다.
실제 멀티플레이: CAP > 새 마을 대기실 열기 > Play > 방 만들기 > 게임 시작.
게임 시작 시 Resources/DetailedTown/DetailedTown.prefab을 불러옵니다.
런타임 연결 코드는 CapMultiplayer/Runtime/CapDetailedTown.cs입니다.
실제 멀티플레이 맵에는 건물/시설 충돌 144개가 연결되어 있습니다.
이 파일 상단의 별도 확인용 씬은 배율 검사용이며 멀티플레이를 실행하지 않습니다.
NPC 상호작용과 사건 진행은 이번 맵 연결 작업에 포함하지 않았습니다.
