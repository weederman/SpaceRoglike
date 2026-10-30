# 작업 인수인계
- 날짜: 2026-10-30
- 브랜치: sehui
- 기준 커밋: 778d28c 타이틀 화면, 사용자 이름 입력 기능 구현 (그 뒤 작업은 커밋 안 됨, 아래 4장)

## 1. 목표
게임의 바깥 흐름(타이틀 → 새 게임/이어 하기 → 게임 → 일시 정지·설정 → 타이틀)을 갖추는 중이다.
타이틀·이름 입력은 커밋했고, 이어서 설정(그래픽·오디오) 시스템과 설정 화면, 게임 중 Esc 일시 정지 메뉴, 에디터에서도 보이는 한글 폰트를 만들었다(미커밋).
그 뒤로는 handoff 이전 계획(전투 밸런스 확인, 메인 스토리 ②)으로 돌아간다.

## 2. 완료한 것
- **씬 분리**(사용자 작업): `Scenes/TitleScene.unity`(타이틀), `Scenes/MainScene.unity`(예전 SampleScene). 빌드 순서 Title 0, Main 1.
- **타이틀·시작 방식**(커밋됨): `Scripts/Title/GameLaunch`(NewGame/Continue/Direct, ReturnToTitle), `TitleMenu`(새 게임 확인 창·이어 하기·설정·종료).
- **이름 입력**(커밋됨): `UI/NameEntryView` + `Resources/UI/NameEntry.prefab`, `Story/PlayerProfile`(hero 화자 이름, 대사의 `{player}` 치환, 저장 `playerName`). 프롤로그가 검은 화면에서 이름을 받은 뒤 시작.
- **저장·재시작 버그 수정**(커밋됨): 출항할 때도 저장(`SaveSystem.HandleUndocked`), 패배 후 '다시 시작'은 마지막 저장에서 이어 함(`ContinueOnNextLoad`), 씬 재로드 때 무기 슬롯·화물칸 초기화(`PlayerWeaponSelector`, `CargoHold`). 프롤로그 시작을 `sceneLoaded`로(타이틀 거쳐도 시작).
- **설정 시스템**(미커밋): `Scripts/Settings/GameSettings`(PlayerPrefs, 음량 4종·해상도·전체 화면), `SettingsConfig` + `Resources/Settings/SettingsConfig.asset`(믹서·채널·해상도 목록), 오디오 믹서 `Resources/Audio/GameMixer.mixer`(Music/SFX/UI 그룹, 노출 파라미터 MusicVolume/SfxVolume/UiVolume).
  - 소리 연결: GameAudio(효과음·UI·음악 소스, SoundTable `channel`), MusicDirector, 엔진(`ShipMovementBase`), 적 등장음(`PirateShip`), 프리팹 Output(EnEmy, Player, FORGE3D `Misc/AudioSource.prefab`).
- **설정 화면**(미커밋): `Settings/SettingsWindow` + `Resources/UI/SettingsWindow.prefab`(정거장 창과 같은 구성, 그래픽 탭: 해상도 ◀▶·전체 화면 스위치 / 오디오 탭: 슬라이더 4개). 타이틀 '설정' 버튼 연결.
- **일시 정지 메뉴**(미커밋): `UI/PauseMenu` + `Resources/UI/PauseMenu.prefab`. MainScene에서 Esc → 계속하기/설정/타이틀로(확인 창). 열려 있으면 timeScale 0·조작 잠금. `GameAudio.StopAll` 추가.
- **한글 폰트**(미커밋): `Assets/Fonts/NotoSansKR/`(ttf + OFL.txt), TMP 폰트 `Resources/Fonts/KoreanFallback.asset`(2048 아틀라스, 914자 선적재, Dynamic), `TextMesh Pro/Resources/TMP Settings.asset`의 전체 대체 폰트로 등록. `GameFonts`가 이것을 먼저 씀.
- `CLAUDE.md`: 씬 이름, `Title`·`Settings` 폴더, `{player}`, 소리 라우팅 규칙, 한글 폰트 규칙(7번) 반영.

## 3. 결정 사항
- **음량은 오디오 믹서 그룹 + 전체는 AudioListener.volume.** 전체를 믹서 Master로 걸면 그룹에 연결 안 된 소리가 빠지므로 리스너로 건다. 버린 대안: 소스마다 코드로 음량 곱하기(FORGE3D 풀 소리 등 놓치는 곳이 생김).
- **믹서는 에디터 내부 API(리플렉션)로 생성**했고 그룹 보기(View)도 직접 만들었다. 믹서 SetFloat는 Start 전에는 무시되므로 `GameSettings.Applier`가 첫 씬 Start에서 적용한다.
- **음량 종류: 전체·음악·효과음·인터페이스**(사용자 결정). 대사 넘김·퀘스트 수락만 인터페이스, 나머지는 효과음.
- **해상도: 16:9(1280×720, 1600×900, 1920×1080 Full HD) / 16:10(1280×800, 1680×1050, 1920×1200) + 전체 화면/창 모드**(사용자 결정). 모니터(`Screen.currentResolution`)보다 큰 것은 숨김. 전체 화면은 FullScreenWindow. 처음 실행은 현재 화면 유지. 버린 대안: 세 개만 고정 전체 화면.
- **설정 값은 바꾸는 즉시 적용·저장**(확인 버튼 없음). 해상도 드롭다운은 UI 팩 Dropdown/Switch 라벨이 레거시 Text라 한글 폰트가 안 붙어 ◀▶ + TMP 라벨로 대체.
- **일시 정지 Esc 충돌 방지는 '지난 프레임 상태'로 판단**(같은 프레임에 지도·창이 Esc로 닫히며 메뉴까지 열리는 것 방지). 스토리·지도·정거장 창·설정 창·조작 잠금·워프·timeScale 0이면 열지 않음. 정거장 안이라도 창이 없으면 열림.
- **한글 폰트는 Noto Sans KR(OFL) 번들 + TMP 전체 대체 폰트**(사용자 결정). 버린 대안: 맑은 고딕 번들(라이선스상 배포 불가), UI 팩 폰트 에셋에 fallback 추가(팩 수정 금지 규칙). 4096 아틀라스는 33MB라 2048(44pt)로 축소.
- **KoreanFallback 줄 높이를 줄여 둠**(ascent 1.0 / descent -0.25 / line 1.25, Noto 원래 1.16/-0.29/1.45). 원래 값이면 Ellipsis 텍스트가 좁은 칸에서 통째로 사라질 수 있음.
- (이전) 새 게임 시 저장은 이름을 확정할 때 지움(이름 입력 중 종료하면 저장 유지). 이름 2~10자. 출항 저장과 도킹 저장 둘 다 유지.

## 4. 현재 상태·주의
- **커밋 안 된 변경**: 설정 시스템·설정 화면·일시 정지 메뉴·한글 폰트 전체(약 30개 파일, 위 2장의 "미커밋"). 커밋은 사용자가 한다.
- **확인 못 한 것**: 실제 해상도 변경(에디터 Game 창은 안 바뀜, 빌드에서 확인 필요), 실제 Esc 키 입력(코드로 Open/Close만 호출해 확인), 음량 슬라이더 소리를 귀로 듣는 것.
- **이 기기에만 있는 것**: 테스트 도킹으로 생긴 `save.json`(2026-10-30 18:12, `%USERPROFILE%/AppData/LocalLow/DefaultCompany/SpaceRoglike/`)이 남아 있다(삭제 권한 막힘). 테스트 데이터라 지워도 된다. `PlayerPrefs` `dev.skipPrologue`=1, 설정 PlayerPrefs(`settings.*`)는 테스트 후 지움.
- **함정**
  - 새 AudioSource는 `GameSettings.Route` 또는 프리팹 Output 그룹을 지정하지 않으면 음량 설정(전체 제외)을 따르지 않는다.
  - `KoreanFallback.asset`은 Dynamic이라 새 한글이 처음 나올 때마다 파일이 바뀐다(커밋 diff에 나옴). 아틀라스 여유 약 450자.
  - TMP 3.0.7은 에디터에서 다중 아틀라스 추가 시 오류(`SetupNewAtlasTexture` NullReference) → 다중 아틀라스 끔.
  - 메인 씬을 바로 Play하면 Direct 모드(개발 설정 따름: `dev.skipPrologue`, 메뉴 '에디터에서 이어 하기'). 새 게임·이어 하기 흐름은 TitleScene에서 Play해야 확인된다.
  - `RuntimeInitializeOnLoadMethod(AfterSceneLoad)`는 처음 씬(TitleScene)에서만 한 번 돈다. 메인 씬에서 매번 할 일은 `sceneLoaded`로 건다.
  - FORGE3D 프리팹을 스크립트로 읽기만 해도 Unity가 직렬화 버전을 올려 다시 쓸 수 있다(예: Explosion_006). 의도 안 한 변경은 `git checkout`으로 되돌린다(Unity가 파일을 잡고 있으면 `AssetDatabase.ReleaseCachedFileHandles()` 후).
  - Unity MCP `execute_code`는 두 번 실행되거나 타임아웃 후에도 실행된다. 상태를 바꾸는 코드는 멱등하게.
- (이전 handoff에서 이어지는 미확인) 잔해 수색·수송선 요격 전투 밸런스, 희귀 부품 결과 창 표시.

## 5. 다음 단계
1. **빌드로 설정 확인**: Windows 빌드에서 설정 창으로 해상도 6종·전체 화면/창 모드를 바꿔 실제 창 크기가 바뀌는지, 재실행 시 유지되는지 확인(`GameSettings.ApplyScreen`). 끝 기준: 각 해상도로 바뀌고 다시 켜도 같은 값.
2. **Esc 일시 정지 직접 확인**: MainScene(TitleScene에서 이어 하기)에서 자유 비행·정거장 안(창 없음)·지도 열림·스토리 재생 중에 Esc를 눌러 열림/안 열림이 2장 규칙대로인지 확인(`PauseMenu.IsEscapeBusy`). 끝 기준: 지도·창을 Esc로 닫을 때 메뉴가 같이 열리지 않음.
3. **커밋 후 전투 밸런스 확인**(이전 handoff 1번): 잔해 수색(`Missions/Main/Salvage_1_4.asset`)·수송선 요격(`Intercept_1_5.asset`)을 스펙터/아이언월로 직접 싸워 적 수·체력·제한 시간 조정. 조정은 임무 에셋·Director 프리팹 값으로(`WeaponStatsTable` 건드리지 않음). 끝 기준: 사용자가 적당하다고 판단.
4. **희귀 부품 결과 화면 확인**(이전 2번): 중급 임무 클리어 시 결과 창에 "희귀 부품" 줄이 뜨는지, 화물칸이 가득 차도 받는지.
5. **(사용자가 요청하면) 메인 스토리 ② 최종 함선**: 완료 임무 수 저장·카운트 → 브레이크포인트(무기 6/장비 8)를 `ShipCatalog`에 추가(`docs/game-design.md` 4장).
