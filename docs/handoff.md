# 작업 인수인계
- 날짜: 2026-10-28
- 브랜치: sehui
- 기준 커밋: 412d123 장면 5, 6 임시 구현

## 1. 목표
SpaceRoglike(탑다운 우주 로그라이크)의 **스토리 연출 시스템과 튜토리얼 스토리**를 장면 단위로 구현 중이다.
프롤로그 → 튜토리얼 퀘스트 「변방의 황금」(장면 1~6)이 **처음부터 끝까지 한 번에 이어지게 모두 구현됐다**(장면 4~6은 "임시 구현" 상태).
다음은 사람이 직접 키보드/마우스로 끝까지 플레이해 보고 난이도·배치·문구를 다듬는 단계다.

## 2. 완료한 것
- **스토리 시스템** (`Assets/Scripts/Story/`): JSON(`Resources/Story/*.json`) → `StoryDatabase` → `StoryPlayer` → `NarrationBar`. 선택지, `then` 연결, `waitFor` + `StoryPlayer.Signal`, 다중 cue(`;`), Esc 건너뛰기, `bg:`, `quest_accept/done`, `tutorial_show/hide`(이미지 포함), `radar_show/hide`.
  - **`"control": true` 시퀀스**(조작하며 듣는 대사): 조작이 잠기지 않고, 대사는 시간 또는 `waitFor` 신호로만 넘어간다(클릭/Esc로 안 넘어감, 선택지 불가). `StoryPlayer.IsControlMode`.
- **프롤로그, 장면 1 술집, 장면 2 격납고, 장면 3 첫 워프** — 이전부터 완료. 장면 3 도착 시 임무 목표 1번 완료 처리.
- **장면 4 첫 교전** (`TutorialCombat.json`, `CombatTutorialDirector` + `Resources/Directors/Cue/CombatTutorialDirector.prefab`, `Scripts/Enemy/PirateShip.cs`): 해적 3척 접근·통신 → 전투 튜토리얼 4단계(목표 지정 → 레이저 → 발칸 → 피격) → 1척 격파 시 1척 도주 → 2척째 격파 시 마지막 1척 워프 이탈 → 마무리 대화.
  - 해적 AI: 평소엔 플레이어에서 170~300 떨어진 **소행성 뒤에 숨어** 떠다니고(150 안에 오면 반격), 8~12초마다 한 방향에 **모여서 공격**(6~8초) 후 다시 흩어진다.
  - 플레이어 선체 하한 30%. 닿으면 **비상 프로토콜**: 화면 위쪽 경고 + 쉴드 재가동·무적(피격 연출만).
- **전투 시스템**: 무기 슬롯 1/2키 전환(`PlayerWeaponSelector`), 목표 지정 Tab/클릭(`TargetingSystem`, 함선·광맥 모두), 전투 HUD(`CombatHud` + `Resources/UI/CombatHud.prefab`: 무기 슬롯, 목표 마커, 화물 게이지, 목적지 표시, 경고), 발사 간격 강제, 자기 함선 피격 방지(`HitResolver.Raycast`), 피격 이벤트 `HitResolver.OnHit`.
- **장면 5 채굴** (`TutorialMining.json`, `MiningTutorialDirector` + 프리팹): 목적지 표시 따라 비행 → 광맥 드러남 → 채굴 튜토리얼(`mining.jpg`) → 65% 대사 → 레이더 + 해적 3척 원거리 대기·무전 → 화물칸 가득 → 해적 워프 후퇴.
  - 채굴: `MiningLaser`(광맥 지정 후 사거리 150 안에서 F 길게, 빔 + 광석 파편 연출), `CargoHold`(한도 100 = 의뢰 목표치), `MineableAsteroid`(장식 `AsteroidField`와 별개, 보랏빛 광맥).
- **장면 6 귀환·정산** (`TutorialReturn.json`, 귀환 워프는 `FirstWarpDirector`의 `s6_return`): 워프 → 정거장 도착(소행성 지대 제거) → 격납고 배경 대화 → `credits_add:300;cargo_sell:3` → 목표 3번 완료 → 마지막 줄에서 상점(정비 메뉴) 열림 + `enemies_on`.
- **버그 수정**: 프롤로그가 스토리 전체가 끝날 때까지 함선·터렛을 꺼 두던 문제(레이저 미발사·이동 불가·HUD 숨김의 원인), 카메라 떨림(Rigidbody 보간 + 지수 감쇠 추적), 탄이 반대편 터렛/자기 쉴드에 맞던 문제, 연타 시 발사 간격 무시.
- **소행성 간격**: 큰 암석끼리 가장자리 간격 30 이상(`AsteroidField._minRockGap`), 광맥 소행성과도 겹치지 않음.
- **상점**: 조작이 잠긴 대사 중에는 열리지 않고 끝나면 연다(`StationShop`).
- **문서**: 장면 4~6 원문과 설계 결정 `docs/story/scenes-4-6.md`.

## 3. 결정 사항
채택:
- **대사 데이터는 JSON**, **UI는 SCI-FI UI Pack 프리팹 기반**, **연출은 cue → 장면별 Director**(건너뛰기 시 최종 상태로 직행), **씬 파일 수정 없이 Resources 프리팹을 런타임 생성** — 이전부터 유지.
- **조작 잠금은 `GameInputLock`으로만.** 연출 담당이 터렛 컴포넌트를 직접 끄던 코드를 제거하고, 잠긴 동안 터렛이 마우스를 따라 돌지 않게 `F3DPlayerTurretController`에서 처리.
- **프롤로그는 control 시퀀스가 시작되면 조작을 돌려준다**(`PrologueDirector.FinishWhenStoryIdle`). 장면 1~6이 `then`으로 이어져 스토리가 "재생 중"으로 유지되기 때문.
- **장면 4 설계**(사용자 선택): 무기 1/2키 전환 + 좌클릭 발사 / 목표 지정은 정보 표시만(조준은 마우스) / 튜토리얼 중 선체 하한 30% / 1척 격파 → 1척 도주 → 1척 워프.
- **해적 AI는 숨었다가 모여서 공격**(사용자 요청): 기존 공전 AI(`EnemyOrbitController`)는 해적에서 끄고 `PirateShip`이 직접 조향. 이유: 플레이어를 중심으로 계속 도는 적은 플레이어가 가만히 있어도 되고 조준하기 어려웠다.
- **해적 피해는 전역 수치(`WeaponStatsTable`)를 건드리지 않고** Director 프리팹의 `PirateShip.Tuning.fireInterval`(4.5초)로 조절. 다른 적도 같은 표를 쓰기 때문.
- **비상 프로토콜 연출 = 쉴드 재가동 + 무적 + 경고**: 체력 고정만으로는 플레이어가 "방어 중"임을 알 수 없어서.
- **장면 5 설계**(사용자 선택): 채굴은 광맥 지정 후 F 길게 누르기 / 해적 증원은 **기본 튜토리얼형**(무전만, 가득 차면 후퇴) / 라스의 좌표는 목적지 표시를 보고 직접 비행 / 장면 6까지 한 번에 구현.
- **귀환 워프는 `FirstWarpDirector`에 추가**: 터널 무대·워프 이펙트·소행성 지대 참조를 이미 갖고 있어서.
- **"정비 메뉴" = 기존 정거장 상점**: 수리 기능이 아직 없음. 정거장 도착 위치가 상점 구역 안(`(0,5,445)`)이라 대사가 끝나면 자동으로 열린다.

버린 대안과 이유:
- **해적 전투 연계형(장면 5에서 해적 1척 추가 교전)** → 원문 문서와 사용자 모두 첫 플레이는 채굴·귀환에 집중하는 쪽을 택함.
- **3번 슬롯 채굴 레이저(좌클릭으로 조준해 채굴)** → 무기와 조작이 섞여 헷갈림. 지정 후 F 키로 분리.
- **지정만 하면 자동 채굴** → 너무 수동적이라 제외.
- **연출로 광맥까지 자동 이동** → 사용자가 직접 비행을 선택.
- **목적지 표시로 목표 마커(광맥 자동 지정) 재사용** → 튜토리얼의 "목표 지정" 단계가 무의미해져서 별도 노란 표시를 만듦.
- **전역 적 발칸 공격력 하향** → 일반 적에도 영향.
- **채굴 빔·파편을 HUD 캔버스 아래에 두기** → 캔버스 배율(약 0.5) 때문에 파편이 작아져서 별도 `MiningLaserFX` 루트로 분리.
- **팁 패널을 체력바 자리에 겹쳐 오른쪽 위 끝으로** → 기존 HUD 체력바(오른쪽 위)를 가려서 그 바로 아래(-24,-84), 82% 크기로.
- (이전 결정 유지) ScriptableObject 대사, ClusterEmitter, VFX Graph 터널, FORGE3D 파편 소행성, 소행성 110개 고정 배치, pulsewave 비콘, `PrologueDirector`→`WarpTunnelStage` 리팩터링 보류, WEBP 이미지.

## 4. 현재 상태·주의
- **모든 변경이 커밋됨**(412d123). `git status` 깨끗함.
- **사람이 직접 끝까지 플레이한 적이 없다.** 자동 테스트는 신호·피해·채굴량을 코드로 넣어 진행했다. 실제 F 채굴, Tab/클릭 지정, 1/2 전환, 해적 숨기/모여서 공격의 체감은 미확인.
- **해적 난이도 조정은 사용자가 보류**("나중에"). 가만히 있으면 한 번 모여서 공격할 때 선체가 약 350 깎여 두 번째 공격쯤 비상 프로토콜이 발동한다.
- 알려진 문제·임시:
  - 광맥 쪽 접근 방향에 따라 오른쪽 위 튜토리얼 안내창이 광맥 소행성을 가릴 수 있다.
  - 장면 4 해적 격파 시 기존 적 프리팹의 처치 보상이 들어가 장면 6 크레딧이 정산액(600)보다 많다.
  - 정산 금액(300 + 광물 단가 3)은 임시값. 마무리 대사의 "장갑 손상률 18%"는 고정 문구.
  - 무기 아이콘은 UI 팩의 소총 아이콘(임시), 광맥 소행성·파편은 2D Space Kit 그림을 보라색으로 물들인 임시 연출.
  - 퀘스트 목표 창은 3개 모두 완료돼도 화면에 남는다(닫는 cue 없음).
  - 장면 6 끝에서 `enemies_on`으로 일반 적 스폰이 켜져, 상점이 열린 정거장 근처에 적이 나온다(원래 게임 루프와 같음).
- 출시 전 교체 필요(라이선스): `Resources/Story/Tutorials/*.jpg` 5장(상용 게임 화면), 술집 배경 `tavern.jpg`(Star Citizen 콘셉트 아트로 보임).
- **이 기기에만 있는 것**: 개발자 모드 `prologue off` 설정(PlayerPrefs, 에디터 로컬). Claude 메모리 폴더 경로가 기기마다 다르다(`C:\Users\<사용자>\.claude\projects\...`).
- 함정:
  - Unity가 백그라운드면 컴파일이 멈춘다 → 컴파일 요청 전에 Unity 창을 앞으로(PowerShell `WScript.Shell.AppActivate`). Play 전에 새 코드가 들어갔는지 확인.
  - **control 시퀀스 중에 `StoryPlayer.Skip()`을 코드로 부르면 그 시퀀스가 통째로 빨리 감긴다**(Esc 키는 막혀 있지만 API는 막지 않음). 테스트 시 control 구간에서는 Skip 대신 `Signal`을 쓸 것.
  - `execute_code`는 같은 코드가 여러 번 실행될 수 있다 → 멱등하게. JSON 수정 후엔 `refresh_unity` 후 Play.
  - 전투 HUD 레이아웃(1920x1080 기준): 왼쪽 위 크레딧·퀘스트 창, 그 아래 화물 게이지(y +150), 왼쪽 가운데 무기 슬롯, 아래 대사창, 오른쪽 위 체력바(씬 HUD)와 그 아래 튜토리얼 안내창. 새 UI를 넣을 때 겹침 확인.
  - `F3DFXController` 등 FORGE3D 코드에 `[추가]` 주석으로 표시한 수정이 있다(에셋 업데이트 시 덮어쓰이지 않게 주의).

## 5. 다음 단계
1. **장면 1~6 수동 플레이 확인**: 사용자가 Play에서 처음부터 끝까지 실제 입력으로 진행하며 문제를 적는다. 끝난 기준: 레이저/발칸 명중, Tab 지정, F 채굴, 해적 격파·도주·워프, 귀환 정산까지 막힘 없이 도달하고 콘솔 에러 0.
2. **해적 난이도 조정**(사용자가 요청할 때): `Resources/Directors/Cue/CombatTutorialDirector.prefab`의 `_pirateTuning`(fireInterval, attackDistance), `_lurkSeconds`/`_attackSeconds`만 조절. 끝난 기준: 사용자가 정한 목표(예: 가만히 있으면 첫 공격 후 선체 70% 이상) 측정 확인.
3. **튜토리얼 안내창과 광맥 겹침 해소**(1번에서 거슬리면): `MiningTutorialDirector.prefab`의 `_oreAngle`/`_reachDistance` 또는 `Resources/UI/TutorialTips.prefab` 크기 조정. 끝난 기준: 광맥에 접근해 채굴하는 동안 광맥이 안내창에 가리지 않음.
4. **프롤로그 "처음 한 번만" 재생**: `PrologueDirector`에 저장 플래그(PlayerPrefs 등) 도입, 개발자 모드 `prologue on`으로 다시 볼 수 있게. 끝난 기준: 두 번째 실행부터 정거장에서 바로 시작.
5. **정거장 수리 기능**("일단 장갑부터 수리하자"): 상점에 선체 수리 항목 추가(`StationShop`, SCI-FI UI Pack 기반). 끝난 기준: 크레딧을 내고 선체가 최대로 회복.
6. **`PrologueDirector`의 터널·카메라 코드를 `WarpTunnelStage`로 통합**: 끝난 기준: 프롤로그 전체 재생과 장면 3·6 워프가 이전과 같게 동작.
7. **출시 전 교체**: 튜토리얼 이미지 5장·술집 배경·무기 아이콘·광맥 그림, 한글 OFL 폰트 TMP 에셋을 `Resources/Fonts/KoreanFallback`에.
