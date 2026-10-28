# 작업 인수인계
- 날짜: 2026-10-28
- 브랜치: sehui
- 기준 커밋: 281a955 노트북 연결 수월하게 하려고 올림 (스토리 코드의 마지막 커밋은 8a76b94 장면 2, 3 추가)

## 1. 목표
SpaceRoglike(탑다운 우주 로그라이크)의 **스토리 연출 시스템과 튜토리얼 스토리**를 장면 단위로 구현 중이다.
프롤로그 → 튜토리얼 퀘스트 「변방의 황금」(장면 1~6)을 순서대로 만들고, 각 장면은 `Assets/Resources/Story/*.json` 대사 데이터 + 장면별 연출 담당(Director)으로 구성한다.
지금까지 프롤로그와 장면 1~3까지 끝났고, 다음은 장면 4(첫 교전)다.

## 2. 완료한 것
- **개발자 모드** (게임 중 Shift+F, `Assets/Scripts/DevMode/`): 채팅창 명령으로 적(개수·체력·쉴드·속도·공격력 등)과 플레이어(속도·체력·무적·회복)·크레딧 조절, `story play <id>`, `prologue on|off`, `pause`, `reset`. 값은 `DevTuning`이 런타임에만 덮어쓰며 에셋은 건드리지 않는다.
- **스토리 시스템** (`Assets/Scripts/Story/`): JSON 로드(`StoryDatabase`) → 재생(`StoryPlayer`) → 나레이션바(`NarrationBar`, SCI-FI UI Pack Popup012_Cyan 기반, NPC 왼쪽/주인공 오른쪽). 기능: 선택지(`choices`/`next`/`id`), 시퀀스 연결(`then`), 외부 신호 대기(`waitFor` + `StoryPlayer.Signal`), 한 줄 다중 cue(`;`), 건너뛰기(Esc, 선택지 앞에서 멈춤), 배경 이미지(`bg:이름`), 퀘스트(`quest_accept`, `quest_done`), 튜토리얼 안내창(`tutorial_show/hide`), 레이더 연출(`radar_show/hide`).
- **프롤로그** (`PrologueDirector`, `Resources/Directors/PrologueDirector.prefab`): 게임 시작 시 자동 재생. 푸른 워프 터널 → 폭풍(파랑→빨강, 경고창, WarpJumpOut) → 화이트아웃 → 표류 + 설명창 → 순찰함 2척 접근 → 압류(견인 광선) → 보상 함선 → 정거장에 붙어서 시작.
- **장면 1 술집**(`TutorialTavern.json`, 선택지 3분기 + 임무 수락), **장면 2 격납고**(`TutorialHangar.json`, 출항 전 안내창), **장면 3 첫 워프**(`TutorialWarp.json` + `FirstWarpDirector`): 출항, 비콘 클릭 선택, 충전, 워프 터널, 도착, 레이더. 프롤로그 → 장면 1 → 2 → 3이 `then`으로 끊김 없이 이어지고, 이어지는 동안 적 스폰과 조작은 막혀 있다가 마지막에 돌아온다.
- **소행성 지대** (`AsteroidField`): 플레이어 주위에 암석 최대 10개 + 잔해 5개, 카메라를 향한 빌보드.
- **플레이어 속도** 60 / 가속도 30으로 상향(씬 `SampleScene`의 플레이어 인스턴스 값).
- **기기 간 인수인계 체계**: `.claude/commands/handoff.md`(저장), `resume-work.md`(이어받기), 이 파일, `CLAUDE.md`, 장면 4~6 원문 `docs/story/scenes-4-6.md`. 모두 커밋됨.

## 3. 결정 사항
채택:
- **대사 데이터는 JSON** (Resources/Story). 긴 대사를 코드 없이 편집하고 diff로 보기 쉬움. 여러 파일로 나눠도 자동 병합.
- **UI는 SCI-FI UI Pack 프리팹 기반** (사용자 지시). 나레이션바는 `Resources/UI/NarrationBar.prefab`에서 모양을 바꾼다.
- **연출은 "cue 이름 → Director가 처리"** 구조. Director는 cue를 큐에 쌓아 한 번에 하나씩 실행(빨리 넘겨도 겹치지 않음). 건너뛰기는 중간 연출을 건너뛰고 "끝났을 때와 같은 최종 상태"로 직행한다. 장면별 Director 프리팹은 `Resources/Directors/Cue/`에 두면 자동 생성.
- **씬 파일을 건드리지 않는 방식**: 런타임에 `Resources` 프리팹을 생성(StoryPlayer, 프롤로그 Director 등). 씬 수정은 사용자가 요청했을 때만(플레이어 속도 한 번).
- **정거장에 붙어서 시작**: 정거장은 모델 없이 구역(`StationZone`)+흰 평면뿐이라, 구역 위치 기준 `(0,5,445)`(`_dockOffset`)에 함선을 놓는다. 구역 안이라 상점 패널이 자동으로 열린다(의도된 동작).
- **원래 함선 vs 새 코르벳**: 코르벳 모델이 하나뿐이라 프롤로그 동안은 노란 `F3_Yellow` 머티리얼을 입히고 보상 함선 지급 때 회색으로 되돌린다. (끄려면 `PrologueDirector`의 `_oldShipMaterial` 비우기)
- **비콘 선택은 마우스 클릭만** 인정. Space/Enter는 대사 넘기기와 겹쳐 실수로 선택되므로 제외.
- **한글**: SCI-FI UI Pack 폰트(Play, Aldrich)에 한글이 없어서, 실행 PC의 맑은 고딕 파일을 런타임에 TMP 대체 폰트로 붙인다(`GameFonts`). 출시 전에는 OFL 한글 폰트(Noto Sans KR/Pretendard)로 TMP 폰트 에셋을 만들어 `Resources/Fonts/KoreanFallback`에 두면 코드 수정 없이 우선 적용된다. TMP Settings의 `Modern Hangul Line Breaking`을 켰다(단어 단위 줄바꿈).
- **플레이어 속도 60/30**: 측정 결과 상태 이상은 없었고(이야기 직후와 새 시작이 동일), 체감 문제여서 사용자가 2배를 선택.

버린 대안과 이유:
- **ScriptableObject 대사 데이터** → 긴 대사 편집/병합이 불편해서 JSON 채택.
- **코드로 만든 임시 나레이션바 UI** → 사용자가 Popup012_Cyan 기반을 지정해 프리팹 방식으로 교체.
- **FORGE3D `ClusterEmitter` 그대로 사용** → 어느 씬에서도 쓰이지 않고, 생성한 성운을 정리하는 기능이 없어 우리 쪽 `WarpClusterFlow`(풀링·팔레트 교체·정리)를 만듦.
- **`Effects/Heat/WarpTunnel.vfx`, `WarpTunnelProfile.asset`** → VFX Graph / Post Processing 패키지가 없고 빌트인 파이프라인이라 사용 불가.
- **FORGE3D `debris_rock/debris_junk`를 소행성으로** → 폭발 파편용 입자 클라우드(작은 청록 점들)라 부적합. 2D Space Kit의 탑다운 소행성/잔해 스프라이트 사용. 처음엔 바닥에 눕혔다가 사용자 요청으로 카메라를 향한 빌보드로 변경.
- **소행성 110개 고정 배치** → 화면이 가득 차서, 플레이어 주위 최대 10개를 재사용(멀어지면 앞쪽으로 옮김)하는 방식으로 변경. 대신 같은 자리에 고정된 지형은 아니다.
- **FORGE3D `pulsewave` 프리팹을 비콘으로** → 한 번 퍼지는 충격파라 부적합. 링 3개+코어를 코드로 생성(`WarpBeacon`).
- **사용자가 씬에서 바꿔 둔 카메라 위치를 워프 카메라로** → 런타임에 `CameraController`가 위치를 덮어써서 쓰이지 않음. 워프 전용 바텀업 추적 카메라를 따로 만듦.
- **`PrologueDirector`를 `WarpTunnelStage`로 리팩터링** → 이미 검증된 프롤로그가 깨질 위험이 있어 보류. 터널 코드가 두 벌 있다(다음 단계 참고).
- **WEBP 이미지 그대로 사용** → Unity가 못 읽어서 JPG로 변환해 사용.
- **Esc 건너뛰기가 선택지까지 대신 고르게** → 선택은 대신 해줄 수 없어서 선택지 앞에서 멈추도록 함.

## 4. 현재 상태·주의
- 모든 작업이 커밋됨(2026-10-28 기준). 슬래시 명령(`/handoff`, `/resume-work`)은 **Claude Code를 새로 시작한 세션부터** 인식된다. 명령을 만든 세션에서는 등록되지 않았다. `git status`에 남는 것은 `Assets/SCI-FI UI Pack Pro` 수정뿐인데 사용자의 임포트 작업이므로 건드리지 않는다.
- **장면 3은 사용자 테스트 중.** 비콘 클릭 경로는 사용자가 직접 한 번 확인했다. 그 외 자동 테스트는 코드로 함수를 호출하는 방식이라 실제 키보드/마우스 입력은 사람이 확인해야 한다.
- 장면 3이 끝나면 함선은 소행성 지대(`(2600, 5, 2400)`)에서 조작 가능해지는데, 기존 `EnemySpawner`가 정거장 근처 `(20,5,180)`에 적 1기를 스폰해 3,500 거리를 날아온다. 장면 4가 이 자리를 대신할 예정.
- 프롤로그는 **실행할 때마다** 자동 재생된다(저장 시스템이 없음). 건너뛰기: 개발자 모드 `prologue off`(PlayerPrefs `dev.skipPrologue`, 이 기기 에디터에만 저장).
- 임시 문구(수정 필요): 압류 경고창 본문, 보상 함선 지급 안내창, 워프 모듈 고장 설명창, 폭풍 경고창 본문, 임무 목표 3줄(원문 축약) — 프리팹/JSON에서 직접 수정.
- 술집 와이드샷 배경(`tavern.jpg`)에 "ARCCORP BAR" 간판이 있어 Star Citizen 콘셉트 아트로 보인다 → **출시 전 교체하거나 라이선스 확인**.
- **이 기기에만 있는 것**:
  - Claude 메모리(`C:\Users\SAEHEE\.claude\projects\...\memory`)에 있는 스토리/설계 메모는 저장소에 없다.
  - 장면 4~6 대사 원문은 `docs/story/scenes-4-6.md`에 저장했다(이미지 링크 줄 제외). **참고용 첨부 이미지는 저장소에 없다** — 필요하면 사용자에게 다시 받는다.
  - 개발자 모드의 `prologue off` 설정(PlayerPrefs).
- 함정:
  - Unity MCP `execute_code`는 같은 코드를 **여러 번 실행**하는 경우가 있다 → 테스트 코드는 멱등하게(이미 있으면 건너뛰기) 쓸 것.
  - JSON을 고친 뒤에는 **`refresh_unity` 후 Play**해야 새 내용이 읽힌다.
  - Play 모드 테스트 중 씬 변경 표시가 남을 수 있다 → 저장하지 말 것(씬 파일 누수 방지). 테스트 후 `git status`로 씬 확인.
  - `StoryPlayer`는 `DontDestroyOnLoad`다. 시퀀스가 끝나기 전에는 `GameInputLock` 때문에 조작이 막힌다.
  - 적 최고 속도(12)에 비해 플레이어가 60이다. 전투가 쉬워지면 적 속도/가속도를 올려야 한다.

## 5. 다음 단계
1. **장면 3 마무리**: `TutorialWarp.json`의 도착 줄(`s3_warp_arrive` 이후)에 `quest_done:frontier_gold:go_beacon`을 추가해 도착 시 임무 목표 1번이 취소선으로 바뀌게 한다. 끝난 기준: Play 모드에서 도착하면 왼쪽 위 목표 창의 첫 줄이 취소선.
2. **장면 4 준비**: 대사 원문은 `docs/story/scenes-4-6.md`를 기준으로 한다. 그 전에 `EnemySpawner`가 장면 3 직후 불필요하게 스폰하지 않도록 장면 4 시작까지 게이트할지 정한다.
3. **장면 4 구현** (`TutorialCombat.json` 새로 + `tutorial_warp`의 `then` 연결): 해적선 3척을 레이더 신호 위치(`RadarPing`의 각도)와 맞춰 접근시키고, 해적 선장 통신 화자(`Speakers.json` 추가), 4단계 전투 안내(목표 지정 → 레이저 → 발칸 → 기동)를 `tutorial_show`와 `waitFor` 신호로 구성. **목표 지정(타깃팅) 시스템은 아직 없어서** 새로 만들어야 한다. 끝난 기준: 해적 2척 격파, 1척 워프 이탈 후 오르비스 대사까지 재생되고 조작이 돌아옴.
4. **장면 5(채굴)·6(귀환/정산)** (원문: `docs/story/scenes-4-6.md`): 채굴 대상 소행성은 장식용 `AsteroidField`와 **별도 오브젝트**로 만든다. 장면 5의 해적 증원은 원문의 두 안(후퇴형/전투 연계형) 중 사용자와 정한 쪽으로.
5. **정리 작업**: `PrologueDirector`의 터널·카메라 코드를 `WarpTunnelStage`로 합치기(중복 제거, 프롤로그 전체 재생으로 회귀 확인), 프롤로그를 "처음 한 번만" 재생하도록 저장 플래그 도입, 임시 문구 확정, 한글 폰트 번들, 술집 배경 이미지 교체/라이선스 확인.
