# 작업 인수인계
- 날짜: 2026-10-30
- 브랜치: sehui
- 기준 커밋: f3f00c8 FORGE3D 풀에서 파괴된 오브젝트 때문에 탄이 남고 적이 발사하지 못하던 문제 수정

## 1. 목표
월드에 생기를 주는 주변 교통(정거장의 아군 경비선·민간 화물선, 정거장·소행성대의 해적 출몰)을 넣었다.
그 과정에서 타이틀에서 시작할 때의 쉴드 연출 버그와, 빌드 테스트(호위 임무 1-3)에서 나온 "탄이 남고 적이 쏘지 못하는" 버그를 고쳤다.
다음은 빌드에서 이 수정들을 확인하고 주변 교통 수치를 다듬은 뒤, 이전 계획(전투 밸런스, 메인 스토리 ②)으로 돌아간다.

## 2. 완료한 것 (모두 커밋됨)
- **타이틀에서 시작 시 쉴드 피격 연출 안 보임**: `Procedural Force-Field/Scripts/ProceduralForceFieldHit.cs`의 피격 시각을 `Time.timeSinceLevelLoad`로. (CLAUDE.md "셰이더 시간" 규칙 추가)
- **주변 교통** `Scripts/Traffic/`
  - `AmbientTraffic` + `Resources/Directors/AmbientTraffic.prefab`: `StarSystem.OnAreaChanged`로 지역에 맞춰 띄우고, 지역을 벗어나면 모두 없앤다. 정거장 지역 = 경비선 3척 순찰, 화물선 수시 등장, 해적 습격(첫 습격 45~90초 뒤). 소행성대 = 비콘 레벨 1~5별 해적 무리(`_beltLevels`).
  - `AllyShip`: 정거장 둘레 순찰, 해적이 교전 거리 안이면 Raid 방식으로 돌며 사격.
  - `NpcFreighter`: `EscortFreighter` 프리팹을 쓰되 임무용 이동은 떼고 움직인다. 정거장 앞 지점을 거쳐 도킹(머티리얼 Fade 모드로 서서히 투명) 또는 반대쪽으로 나아가 `WarpDash` 워프.
  - `ShipMarker`: 함선 아래 LineRenderer 원. 아군 초록, 해적 빨강. `PirateShip.Init`에서 모든 해적(임무·튜토리얼 포함)에 붙는다.
- **아군 무기·보상**: `WeaponOwner.Ally` 추가, `HitResolver`에서 아군 탄은 해적에게만 맞는다. `EnemyKillReward`는 플레이어가 한 번이라도 맞힌 적만 크레딧을 준다.
- **함종 목록** `Missions/EnemyShips.asset`: 아군 경비선 `patrol_corvette`(F3_Corvette Green Variant 모델) 추가, Markers(원 색·굵기·크기) 설정 추가. `EnemyShipCatalog`는 `Build` 공통 + `Spawn`(해적)/`SpawnAlly`(아군). `PirateShip.All`(전체 목록), `IsHostileTarget` 추가.
- **FORGE3D 풀 버그 수정** (5개 파일, `[추가]` 표시)
  - `PoolManager/F3DPool.cs` `ReleaseDead`: 꺼낼 때마다 파괴된 참조 제거 + 개수(`poolLengthCurrent`) 보정. `SpawnAudio` 포함 모든 Spawn에서 호출.
  - `F3DProjectile.cs`: 착탄 연출 전에 `isFXSpawned`를 세워 예외가 나도 탄이 반드시 풀로 돌아간다.
  - `F3DAudioController.cs`(`SourceOf`), `F3DFXController.cs`(`SpawnedObject`, `ComponentOf`): 풀이 가득 차 null이 오면 그 소리/발사만 건너뜀.
  - `F3DTime.cs`: 주인이 파괴된 타이머는 부르지 않고 지움. `F3DFXController.OnDisable`: 반복 발사 타이머 제거.

## 3. 결정 사항
- **풀 버그의 근본 원인은 풀 쪽에서 고친다.** 풀은 `needSort` 꺼짐이라 Despawn 뒤에도 마지막 부모(포탑 소켓) 밑에 남고, 부모 함선이 파괴되면 같이 파괴된다. 소리 오브젝트 하나가 이렇게 죽으면 `SpawnAudio`가 매번 예외 → 적 발사 중단 + 착탄 처리에서 탄이 멈춤. 버린 대안: 풀 DB의 `needSort`를 켜서 풀로 다시 붙이기(에셋 데이터 변경이고 부모를 따라가야 하는 루프음·빔 동작이 바뀔 수 있음), 최대 개수(50)만 늘리기(누수 자체는 그대로).
- **편 판정은 `WeaponOwner.Ally` + `FriendlyShip`.** 아군 NPC 탄을 Player로 두는 안은 버림(튜토리얼·임무가 "플레이어가 맞혔는지"를 Player로 판정해서 섞인다).
- **아군 함종은 별도 카탈로그 없이 `EnemyShips`에 같이 둔다.** 바탕 프리팹(EnEmy)·모델 붙이기·체력 설정이 같아서. 아군은 생성 시 `FriendlyShip`을 붙이고 `EnemyKillReward`를 뗀다.
- **소행성대 해적은 Fight(숨기)가 아니라 Raid(플레이어 노림).** Fight는 Director가 공격 신호를 줘야 덤비는 구조라 주변 교통에 안 맞음.
- **해적은 임무·튜토리얼(`frontier_gold` 퀘스트)·스토리·도킹·워프 중엔 안 나온다.** 임무 시작이나 도킹 시 이미 있던 해적은 워프로 물러난다(`RetreatPirates`).
- **도킹 중 지역 변화는 무시하고 도킹이 끝나면 다시 맞춘다(`SyncArea`).** 도킹하면 함선이 내부 공간(먼 좌표)으로 옮겨져 지역이 "없음"이 되기 때문. 출항 중(아직 도킹 상태) 바뀐 지역을 놓쳐 경비선이 안 생기던 버그를 이렇게 고쳤다.
- **화물선 페이드는 원래 머티리얼(Autodesk Interactive)을 런타임에 Fade 모드로 전환.** 별도 반투명 머티리얼 에셋을 만드는 안은 보류(우선 간단히, 빌드에서 안 되면 그때).
- **격파 보상은 플레이어가 맞힌 적만.** 경비선 혼자 잡은 해적에 돈을 주면 정거장 앞에서 대기만 해도 돈이 들어온다.

## 4. 현재 상태·주의
- 커밋 안 된 변경 없음.
- **빌드 미확인**: FORGE3D 풀 수정(에디터 재현과 1-3 임무로만 확인), 화물선 Fade(빌드에서 셰이더 변형이 빠지면 투명해지지 않을 수 있음).
- **원래 빌드 버그의 첫 계기는 확정 못 함**(로그 없음). 유력: 빔(고출력 레이저) 루프음이 포탑에 붙은 채 함선 교체·격파로 같이 파괴. 다음 빌드 테스트에서 문제가 있으면 테스트 PC의 `%USERPROFILE%\AppData\LocalLow\DefaultCompany\SpaceRoglike\Player.log`를 받는다.
- **임무 시작 시 주변 해적 퇴각은 Play로 확인 안 함**(도킹 시 퇴각은 확인).
- **정거장 습격이 셈**: 가만히 있던 플레이어가 경전투함 3척에 격파됨. 경비선은 교전 거리(1200) 안의 1척만 싸웠다.
- 원(ShipMarker) 크기는 선체 판정 기준이라 큰 화물선은 원도 크다.
- 테스트로 저장 파일(`save.json`)은 새 게임 테스트로 덮어써졌다(테스트 데이터). `dev.skipPrologue`는 0으로 되돌려 둠.
- **함정**
  - 셰이더에 시각을 넘길 땐 `Time.timeSinceLevelLoad`(CLAUDE.md 규칙).
  - FORGE3D 풀 오브젝트는 Despawn 뒤에도 마지막 부모 밑에 남는다. 부모를 파괴하면 풀 오브젝트도 사라진다(이제 `ReleaseDead`가 정리하지만, 풀에서 꺼낸 것을 직접 Destroy하지 말 것).
  - `Procedural Force-Field`의 cs 파일 중 일부는 UTF-8이 아닌 옛 인코딩이라 Edit 도구가 거부한다 → 셸로 수정하고 주석은 영어로.
  - 테스트 코드로 함선을 옮길 때는 `Rigidbody.position`도 같이 바꿔야 한다(키네마틱 화물선은 transform만 바꾸면 원래 경로로 돌아감).
  - `execute_code`는 두 번 실행될 수 있다(스폰 테스트가 3배로 생긴 적 있음).
  - MainScene 직접 Play는 개발 설정을 따른다. 해적 테스트는 `dev.skipPrologue`=1로 해야 튜토리얼이 막지 않는다(끝나면 0으로).

## 5. 다음 단계
1. **빌드에서 확인**: Windows 빌드로 (a) 호위 임무 1-3을 끝까지 진행해 탄이 남거나 적이 발사를 멈추지 않는지, (b) 고출력 레이저 장착 후 함선 교체 → 전투가 정상인지, (c) 정거장에서 화물선이 투명해지며 사라지는지. 끝 기준: 세 가지 모두 정상이고 `Player.log`에 예외 없음. (c)가 안 되면 `NpcFreighter.BeginFade`에서 반투명 머티리얼 에셋을 `Resources`에 두고 바꿔 끼우는 방식으로 변경.
2. **주변 교통 수치 조정**: `Resources/Directors/AmbientTraffic.prefab`의 정거장 습격(`_stationRaid` 척수·간격), 경비선 교전 거리(`_allyEngageRange`), `Missions/EnemyShips.asset`의 Markers `radiusRatio`를 사용자와 직접 플레이하며 조정. 끝 기준: 사용자가 적당하다고 판단.
3. **임무 시작 시 퇴각 확인**: 정거장 습격 중에 임무를 시작해 주변 해적이 워프로 물러나는지 Play로 확인(`AmbientTraffic.RetreatPirates`). 끝 기준: 임무 적만 남음, 콘솔 에러 없음.
4. **전투 밸런스 확인**(이전 handoff에서 이어짐): 잔해 수색(`Missions/Main/Salvage_1_4.asset`)·수송선 요격(`Intercept_1_5.asset`)을 스펙터/아이언월로 직접 싸워 적 수·체력·제한 시간 조정(임무 에셋·Director 프리팹 값으로, `WeaponStatsTable` 금지). 끝 기준: 사용자가 적당하다고 판단.
5. **희귀 부품 결과 화면 확인**: 중급 임무 클리어 시 결과 창에 "희귀 부품" 줄이 뜨는지, 화물칸이 가득 차도 받는지.
6. **(사용자가 요청하면) 메인 스토리 ② 최종 함선**: 완료 임무 수 저장·카운트 → 브레이크포인트(무기 6/장비 8)를 `ShipCatalog`에 추가(`docs/game-design.md` 4장).
