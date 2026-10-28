# SpaceRoglike

탑다운 시점의 우주 로그라이크 (PC/Steam 목표). 3D로 렌더링하되 게임플레이는 2D 평면(x, z)에서 진행한다.

## 기술 스택
- Unity 2022.3.47f1, Built-in 렌더 파이프라인, Linear 색 공간
- 입력: 레거시 `Input` (새 Input System 사용 안 함)
- UI: uGUI + TextMeshPro 3.0.7 (asmdef 없음, 스크립트는 전부 기본 어셈블리)
- 에셋: FORGE3D Sci-Fi Effects(포탑·투사체·워프 이펙트), SCI-FI UI Pack Pro(게임 UI), 2D Space Kit(소행성/잔해 스프라이트), JMO WarFX
- 에디터 자동화: Unity MCP (`execute_code`, `manage_editor`, `refresh_unity`, `read_console` 등)

## 폴더 구조
- `Assets/Scripts/` — `Controllers`(함선·카메라·입력 잠금), `Turret`, `Enemy`, `Health`, `Economy`, `Environment`, `Story`, `UI`, `DevMode`
- `Assets/Resources/` — 런타임에 코드로 불러오는 것만: `Story/*.json`(대사), `UI/`(스토리 UI 프리팹), `Directors/`(연출 담당), `WeaponStatsTable.asset`
- `Assets/Prefabs/` — `Effects`, `Projectiles`, `Ships`, `Turrets`, `UI`
- `Assets/Scenes/SampleScene.unity` — 메인 씬
- `docs/handoff.md` — 진행 중 작업 인수인계

## 아키텍처 원칙
- **데이터는 한 곳에서**: 무기 수치는 `WeaponStatsTable`, 대사는 `Resources/Story/*.json`. 코드에 수치/대사를 박지 않는다.
- **개발자 모드(`DevTuning`)는 런타임 값만 덮어쓴다.** 에셋과 프리팹은 절대 수정하지 않는다.
- **피해 처리**: `IDamageable` + `HitResolver`, 체력/쉴드는 `HullHealth`/`ShieldHealth`의 이벤트로 알린다.
- **스토리 파이프라인**: JSON → `StoryDatabase` → `StoryPlayer` → `NarrationBar`. 연출은 대사의 `cue`로 호출하고 장면별 Director가 처리한다.
  - Director는 cue를 큐에 쌓아 한 번에 하나씩 실행한다. **건너뛰기(Esc) 시에는 중간 연출을 생략하고 "끝난 뒤의 최종 상태"로 바로 이동**해야 한다.
  - 장면 Director 프리팹은 `Resources/Directors/Cue/`에 두면 `StoryPlayer`가 자동 생성한다.
  - 시퀀스는 `then`으로 이어 붙인다(이어지는 동안 `IsPlaying`이 유지되어 조작 잠금이 풀리지 않는다).
- **조작 잠금은 `GameInputLock`(소유자 기반)으로만** 건다. 직접 `enabled = false` 등으로 막지 않는다.
- 스토리 UI는 시간 정지 중에도 동작해야 하므로 **unscaled time**을 쓴다.
- 텔레포트/큰 이동 뒤에는 `CameraController.SnapToTarget()`을 호출한다. (씬에 둔 카메라 위치는 런타임에 `CameraController`가 덮어쓴다)

## 반드시 지킬 규칙
1. **커밋은 사용자가 한다.** `git commit`/`push`는 요청받기 전에는 하지 않는다. 커밋 목록과 메시지를 제안만 한다.
2. **끝났다고 말하기 전에 Play 모드에서 직접 확인**한다 (화면 확인 + 콘솔 에러 확인). 컴파일 성공만으로 완료라고 하지 않는다.
3. **UI는 SCI-FI UI Pack Pro 프리팹을 기반으로** 만든다. 임시 코드 UI를 새로 만들지 않는다. `Assets/SCI-FI UI Pack Pro` 안의 사용자 변경은 임포트 작업이므로 건드리지 않는다.
4. **씬/프리팹 누수 방지**: 작업 후 `git status`로 의도하지 않은 `SampleScene.unity` 변경이 없는지 확인한다. 테스트 중 생긴 씬 변경은 저장하지 않는다. `Assets/Screenshots`가 생겼으면 지운다. 씬 수정은 사용자가 요청했을 때만 한다.
5. **JSON 대사를 고친 뒤에는 `refresh_unity` 후 Play** 한다 (안 하면 이전 데이터로 실행된다).
6. **Unity MCP `execute_code`는 같은 코드가 여러 번 실행될 수 있다** → 생성/설정 코드는 멱등하게(이미 있으면 건너뜀) 작성한다.
7. **한글 폰트**: SCI-FI UI Pack 폰트에는 한글이 없다. 한글을 쓰는 TMP는 `GameFonts`의 대체 폰트를 거친다. 새 한글 UI를 만들면 한글이 정상 표시되는지 확인한다.
8. **UI 문자열에 `&gt;` 같은 HTML 엔티티를 쓰지 않는다** (TMP 리치텍스트에서 그대로 출력됨). `>`를 그대로 쓴다.
9. **풀링/키네마틱**: Kinematic Rigidbody에는 `velocity`를 대입하지 않는다 (`isKinematic` 확인).
10. 사용자와 대화는 한국어로 한다.

진행 중 작업은 docs/handoff.md 참고 (/resume-work)
