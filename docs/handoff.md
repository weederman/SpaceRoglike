# 작업 인수인계
- 날짜: 2026-10-30
- 브랜치: sehui
- 기준 커밋: 6a9bc33 최적?화? 한번 함 (이후 작업은 모두 아직 커밋 안 됨)

## 1. 목표
SpaceRoglike의 튜토리얼 이후 "반복 임무 + 정거장 경제 + 메인 스토리" 루프를 만들고 있다.
반복 임무 12개(호송·해적 소탕·채굴·운송), 경제·무기 10종·장비 5종×4등급, **메인 스토리 ① 첫 번째 함선 해금 퀘스트(라스)와 함선 시스템(스펙터·아이언월)**까지 들어갔다.
다음은 메인 스토리 ② 최종 함선 → 보스 볼테르 → 엔딩(`docs/game-design.md` 4장)이다.

## 2. 완료한 것
- **임무 시스템** (`Scripts/Mission/`): 공통 `MissionData`/`MissionDirector`, `EnemyShipCatalog`. 스페이스 라운지(`MissionBoardWindow`)에서 수락.
  - 호송 1-x, 해적 소탕 2-x, 채굴 3-x, 운송 4-x (`docs/missions.md`). 미션 보수는 기획 "미션 보수" 표의 기본 보수.
  - `MissionData.OnBoard`(false면 라운지 목록에 안 나옴), `CanStart`(수락 조건), `MissionDirector.NearestBeacon/IsAt`(공용 도구), `Succeed(…, successSequence)`(대사 지정).
- **경제·무기·장비** (`docs/game-design.md` 7장): 시작 자금 10,000, 무기 id 체계(`WeaponStatsTable`), 유도탄, 플라스마 포, 장비 5종×4등급, 장비 칸, 광석 구매(시장가×2, 10개씩), 티타늄 광석.
- **함선 시스템 (2026-10-30)**: `Scripts/Ships/ShipCatalog`(`Resources/Ships/ShipCatalog.asset`) + `PlayerShips`.
  - 스펙터(무기 2·장비 4) / 아이언월(무기 4·장비 6, 모델 K4_Green 임시). 선체·속도·가속·보호막 기준값은 함선 데이터. 장비 칸·보호막 기준값은 `EquipmentCatalog`에서 옮겼다.
  - 무기는 함선마다 따로 기억(`Loadouts`), 장비는 공유. 무기 슬롯 3·4는 터렛 복제(`extraTurretMounts`), 무기 HUD 4칸.
  - 격납고 탭 함선 목록에서 갈아타기. cue `ship_grant:id`. 저장 `ship`/`ships`/`shipLoadouts`.
- **첫 번째 함선 퀘스트 (2026-10-30)**: `Scripts/MainQuest/FirstShipQuest`(진행)·`FirstShipQuestData`(`Resources/MainQuest/FirstShipQuest.asset`), 대사 `Story/MainFirstShip.json`.
  - 흐름·수치·임시값은 `docs/game-design.md` 8장. 라운지·미션 탭 맨 위 '라스' 항목에서 단계별 버튼(구매·납품·임무 출발·조립).
  - 임무: `SalvageMissionDirector`(잔해 수색, `Missions/Main/Salvage_1_4.asset`), `InterceptMissionDirector`(수송선 요격, `Intercept_1_5.asset`). 새 함종 `munitions_transport`.
  - 개발자 명령: `mainquest [stage 0~6]`, `ship [grant|switch id]`.
- 정거장 안에서는 퀘스트 목표 창(`QuestTracker`)을 숨긴다(사이드바와 겹침).

## 3. 결정 사항
- **운송**: 받은 정거장 출발, 도착지는 거리 구간에서 무작위, 화물은 화물칸 한도 공유, 진행 중 임무는 저장 안 함, 4-2는 선체 50% 미만 즉시 실패 (사용자 선택).
- **첫 번째 함선 퀘스트는 질문 없이 기록된 정보로 구현** (사용자 지시). 가정한 것은 `docs/game-design.md` 8.4에 번호로 정리해 두었다 → 나중에 사용자에게 묻는다.
- **기본 보호막 없음 유지**: 증폭기를 달아야 보호막이 생기고 기준값은 함선별.
- **무기 가격 1000배 유지, 장비 가격은 기획 단위** (사용자 선택).
- 메인 퀘스트 임무는 반복 임무와 같은 `MissionDirector` 구조 위에 만들고 `OnBoard=false`로 라운지 목록에서 뺐다.

## 4. 현재 상태·주의
- **커밋 안 된 변경이 많다** (아래 커밋 제안). `SampleScene.unity`는 `_startCredits: 0 → 10000` 한 줄만 바뀌었다(의도한 변경).
- **게임 반영 금지 유지**: `docs/game-design.md` 4장(최종 함선·보스·엔딩)은 사용자가 말하기 전에는 구현하지 않는다.
- **알려진 문제**
  - 아이언월 모델이 임시(K4_Green 구축함 모델)이고 아이콘은 스펙터 것을 쓴다. 터렛 위치(`extraTurretMounts`)도 눈대중이다.
  - 수송선 요격·잔해 수색의 전투 밸런스(적 수·체력·제한 시간)는 실제 플레이로 확인하지 않았다. 코드로 격파·수색을 강제해 흐름만 확인했다.
  - 광물 구매가 시장가의 2배라 라스 재료(철 200·티타늄 100·희귀 30) 전부 사면 약 17,000 CR 수준.
  - 빔 무기는 마우스를 뗄 때 사라진다(F3D 원래 동작). 무기 아이콘은 하얀 임시 그림.
  - 퀘스트는 라스 연락 후 '모항'에서만 광물 납품·조립이 된다. 모항이 아닌 정거장에서는 이유가 표시된다.
  - 진행 중 임무(운송·메인 퀘스트 임무 포함)는 저장하지 않는다. 메인 임무 도중 종료하면 임무는 처음부터, 모은 특수 합금은 남는다.
- **임시값**: 무기·함선·잔해·수송선 수치, 보호막 기준값, 광물 구매가 배율 (`docs/game-design.md` 7·8장).
- **테스트 함정**
  - 테스트 전 `save.json`(`%USERPROFILE%/AppData/LocalLow/DefaultCompany/SpaceRoglike/`)을 백업하고 끝나면 되돌린다(도킹하면 자동 저장).
  - 저장에서 시작하려면 에디터 메뉴 "SpaceRoglike/저장/에디터에서 이어 하기"(EditorPrefs `SpaceRoglike.ContinueInEditor`)를 켠다. 끝나면 끈다.
  - Play 직후 저장 불러오기가 몇 프레임 뒤에 끝난다(`SaveSystem.IsLoading`). 그 전에 임무를 시작하면 엉킨다.
  - 라스의 첫 연락은 자금이 100만을 넘으면 자동 재생된다 → 테스트 세이브의 자금이 크면 Play 직후 대사가 나온다.
  - `PlayerPrefs` `dev.skipPrologue`는 0으로 되돌린다. `Assets/Screenshots`는 지운다.
  - 사용자가 Play 중일 수 있다. Play를 멈추기 전에 묻는다.
  - Unity MCP `execute_code`는 `AssetDatabase.DeleteAsset`을 막는다. 파일 삭제는 셸로 파일과 `.meta`를 함께 지운다. `git rm`은 쓰지 않는다.
  - Bash heredoc에 한글·따옴표·`\n`이 섞인 파이썬을 넣으면 깨진다 → 스크래치 파일로 써서 실행하고, C# 문자열의 `\n`이 실제 개행으로 바뀌지 않았는지 확인한다.

## 5. 다음 단계
1. **커밋** (사용자가 한다). 제안:
   - `무기 id 도입, 무기 10종·유도탄·플라스마 포` — `Assets/FORGE3D/Sci-Fi Effects/Code/F3D*.cs`, `Scripts/Turret/*`, `Scripts/Economy/{PlayerHangar,StationShop,WeaponCatalog}.cs`, `Scripts/UI/CombatHud.cs`, `Assets/Items/*`, `Resources/Shop/WeaponCatalog.asset`, `Resources/WeaponStatsTable.asset`, `Prefabs/Turrets/Turret1.prefab`, `Resources/UI/CombatHud.prefab`
   - `장비 5종 4등급과 장비 칸, 광물 구매` — `Scripts/Economy/{EquipmentCatalog,PlayerEquipment,CargoHold,OreCatalog}.cs`, `Scripts/Controllers/MiningLaser.cs`, `Resources/Shop/{EquipmentCatalog,OreCatalog}.asset`
   - `경제 반영: 시작 자금·튜토리얼 보상·미션 보수` — `Scenes/SampleScene.unity`, `Resources/Story/Tutorial{Hangar,Return}.json`, `Resources/Missions/{Escort,Sweep,Mining}/*.asset`
   - `운송 임무 4-1~4-3` — `Scripts/Mission/Transport*`, `Scripts/Map/StarSystem.cs`, `Resources/Missions/Transport*`, `Resources/Story/{MissionTransport,Speakers}.json`
   - `함선 시스템과 첫 번째 함선 퀘스트` — `Scripts/Ships/*`, `Scripts/MainQuest/*`, `Scripts/Mission/{Salvage*,Intercept*}`, `Scripts/Controllers/PlayerShipAppearance.cs`, `Scripts/Save/SaveSystem.cs`, `Scripts/UI/{HangarWindow,CargoWindow,MissionBoardWindow}.cs`, `Scripts/Story/QuestTracker.cs`, `Scripts/DevMode/*`, `Resources/{Ships,MainQuest,Missions/Main}/*`, `Resources/Story/MainFirstShip.json`, `Resources/UI/StationInterior.prefab`, `Resources/Missions/EnemyShips.asset`
   - `문서` — `docs/*`, `CLAUDE.md`
2. **사용자에게 질문** (`docs/game-design.md` 8.4): 첫 번째 함선이 아이언월인지, 임시 재료 이름("희귀 결정"=희귀 광석), 납품 위치(모항 한정), 거래소 구매 방식, 새로 쓴 대사.
3. **실제 플레이로 확인**: 100만 CR을 모아 라스 연락 → 구매 → 재료 → 두 임무 → 조립. 끝 기준: 임무 난이도(적 수·제한 시간)가 적당하고 아이언월 4슬롯이 불편하지 않은지.
4. **(사용자가 요청하면) 최종 함선 퀘스트**: 3,000,000 CR + 완료 임무 50회 → 완료 임무 수 저장·카운트가 먼저 필요하다. 브레이크포인트 함선(6/8)을 `ShipCatalog`에 추가.
5. **(필요하면) 아이언월 전용 모델·아이콘, 진행 중 임무 저장**.

## 추가 메모
- 기획 문서 원본(그림 포함)은 이 기기의 `Downloads/425a3c47-…_ExportBlock-…/`에만 있다. 텍스트는 `docs/design-source.md`에 복사해 두었다.
