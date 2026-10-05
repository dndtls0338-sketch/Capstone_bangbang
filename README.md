# 대학생 탐정단

Unity **6000.3.23f1** · PC 2D · 4인 Relay 멀티플레이 · Vivox 음성

## 실행

1. Unity Hub에서 `Assets`, `Packages`, `ProjectSettings`가 있는 폴더를 엽니다.
2. 패키지 설치·컴파일 후 `Assets/Scenes/CapRelayTest.unity`를 더블클릭합니다. `CAP > 게임 씬 열기`로도 열 수 있습니다.
3. Play → 방 만들기 또는 참가 코드 입력 → 전원 준비 → 방장 게임 시작 순서로 진행합니다.

마을은 실행 중 코드로 생성하므로 편집 중인 씬에는 보이지 않습니다. 기본 조작은 WASD 이동, E 문, M 지도, B 자전거, V 눌러서 말하기, N 마이크 음소거, F10 옵션입니다. 사건 조사·보고서 기능은 아직 구현하지 않았습니다.

## 폴더

| 위치 | 내용 |
| --- | --- |
| `Assets/Scenes` | 실행 씬 |
| `Assets/Prefabs` | 플레이어 원본 |
| `Assets/Art` | 도형용 흰색 이미지 |
| `Assets/Scripts/Networking` | 방 생성·참가와 게임 초기화 |
| `Assets/Scripts/Players` | 이동·충돌·프로필·준비와 시작 조건 |
| `Assets/Scripts/World` | 마을·카메라·지도·회의실 |
| `Assets/Scripts/UI` | 시작 화면·대기실·옵션·로딩 |
| `Assets/Scripts/Audio` | 음성 연결·거리·장치 테스트 |
| `Assets/Scripts/Input` | 조작키 설정 |
| `Assets/Resources` | 실행 중 불러오는 맵 배치 데이터·셰이더 |
| `Assets/Editor` | 씬 열기·Windows 빌드 메뉴 |
| `Assets/Settings` | 렌더링·입력 설정 |

건물과 도로 배치는 `Assets/Resources/Blockout/layout.json`, 도형 생성은 `Assets/Scripts/World/CapTownGeometry.cs`에서 수정합니다. `CapWarmTown`과 `CapNetworkPlayer`는 기능별로 파일을 나눈 `partial class`입니다. `CapTestBootstrap`은 이름에 Test가 있지만 실제 게임 초기화 코드입니다.

## 빌드와 공유

`CAP > Windows 빌드`로 `Builds/Game/cap.exe`를 만듭니다. 친구에게는 `Builds/Game` 폴더 전체를 전달합니다. 빌드 후 접속 → 전원 준비 → 방장 시작 → 마을·회의실 이동 → 대기실 복귀를 확인합니다.

작업 전 Pull, 작업 후 저장 → Commit → Push를 진행합니다. 다른 사람도 Pull해야 변경을 받습니다. 에셋 이동은 Unity Project 창에서 하고 `.meta`를 유지합니다. `Assets/DefaultNetworkPrefabs.asset`는 Netcode가 사용하는 등록 목록이므로 삭제하지 않습니다. `Library`, `Builds`, `Logs`, `UserSettings`는 자동 생성되므로 Git에 올리지 않습니다.
