using System.Collections;
using System.Collections.Generic;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 호송 임무 실행 담당 (공통 부분은 MissionDirector).
///  1) 정거장에 도킹해 있으면 먼저 출항한다 → 의뢰인 수락 대사
///  2) 플레이어 옆에 화물선이 나타나 워프 준비를 시작한다 (타이머, 화물선에 목적지 표시)
///  3) 임무 데이터의 시각마다 적 함대가 멀리서 나타나 화물선이나 플레이어를 습격한다 (PirateShip.Raid)
///  4) 타이머가 끝나면 화물선이 워프해 떠나고 적은 물러난다 → 성공 대사 → 보수 지급 (화물선 체력·보너스 반영)
///     화물선이 파괴되면 실패.
/// 진행 중에는 정거장 도킹과 지도 워프가 막힌다.
/// 프리팹: Assets/Resources/Missions/EscortMissionDirector.prefab (화물선 프리팹과 배치 수치)
/// </summary>
public class EscortMissionDirector : MissionDirector
{
    [Header("호송")]
    [SerializeField] private EscortFreighter _freighterPrefab;
    [Tooltip("화물선이 나타나는 곳: 플레이어 기준 (오른쪽, 앞쪽) 거리")]
    [SerializeField] private Vector2 _freighterOffset = new Vector2(260f, 120f);
    [Tooltip("적이 나타나는 거리 (화물선 기준). 화면 밖")]
    [SerializeField, Min(100f)] private float _enemySpawnDistance = 1100f;
    [Tooltip("한 번에 나타나는 적끼리 벌어지는 각도")]
    [SerializeField, Range(0f, 60f)] private float _enemySpread = 14f;

    private EscortMissionData _data;
    private EscortFreighter _freighter;
    private float _elapsed;
    private int _nextWave;

    // 보너스: 지정 함종을 모두, 그 무기가 화물선에 닿기 전에 격파
    private F3DFXType _bonusWeapon;
    private bool _bonusPossible;
    private bool _bonusLost;
    private int _bonusShipsSpawned;
    private int _bonusShipsKilled;

    // 호송은 처음부터 끝까지 화물선 곁을 떠날 수 없다
    protected override bool TravelBlocked => true;

    protected override bool Setup(MissionData data)
    {
        _data = data as EscortMissionData;
        return _data != null;
    }

    private void OnEnable()
    {
        HitResolver.OnHit += HandleHit;
    }

    private void OnDisable()
    {
        HitResolver.OnHit -= HandleHit;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (_freighter != null)
            Destroy(_freighter.gameObject);
    }

    // ── 진행 ───────────────────────────────────────────

    protected override IEnumerator Run()
    {
        yield return LeaveStation();
        yield return PlayStory(_data.acceptSequence);

        if (Player == null)
        {
            Destroy(gameObject);
            yield break;
        }

        SpawnFreighter();
        PrepareBonus();

        Hud.Show(_data.title, _data.objective);
        CombatHud.ShowWaypoint(_freighter.transform, "화물선");

        // 워프 준비: 시간이 다 될 때까지 화물선을 지킨다
        _elapsed = 0f;
        _nextWave = 0;
        while (_elapsed < _data.warpPrepSeconds)
        {
            if (_freighter == null || !_freighter.Hull.IsActive)
            {
                yield return Fail("화물선이 파괴되었습니다.");
                yield break;
            }

            SpawnDueWaves();
            Hud.SetTimer("워프 준비", _data.warpPrepSeconds - _elapsed);
            Hud.SetBar("화물선 선체", _freighter.Hull.CurrentHp / _freighter.Hull.MaxHp);
            Hud.SetEnemies(AliveEnemies());

            yield return null;
            _elapsed += Time.deltaTime;
        }

        // 화물선 워프
        float hpRatio = _freighter.Hull.CurrentHp / _freighter.Hull.MaxHp;
        bool bonus = _bonusPossible && !_bonusLost && _bonusShipsSpawned > 0 && _bonusShipsKilled >= _bonusShipsSpawned;

        CombatHud.HideWaypoint();
        _freighter.WarpOut(_warpOutPrefab);
        Hud.SetStatus("화물선 워프 개시");
        while (_freighter != null)
        {
            Hud.SetEnemies(AliveEnemies());
            yield return null;
        }

        int baseReward = _data.BaseReward(hpRatio);
        var lines = new List<string>();
        if (baseReward < _data.reward)
            lines.Add($"화물선 손상으로 보수 감소 ({_data.reward:N0} → {baseReward:N0} CR)");
        if (bonus)
            lines.Add($"{_data.bonusLabel} +{_data.bonusReward:N0} CR");

        yield return Succeed(_data, baseReward + (bonus ? _data.bonusReward : 0),
            $"보수 +{baseReward:N0} CR (화물선 선체 {Mathf.RoundToInt(hpRatio * 100f)}%)", lines);
    }

    // ── 화물선 ─────────────────────────────────────────

    private void SpawnFreighter()
    {
        // 플레이어가 바라보는 쪽으로, 플레이어 오른쪽 앞에 나타나 같은 쪽으로 나아간다
        Vector3 forward = Player.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 position = Player.position + right * _freighterOffset.x + forward * _freighterOffset.y;
        _freighter = Instantiate(_freighterPrefab, position, Quaternion.LookRotation(forward));
        _freighter.name = "EscortFreighter";
        _freighter.Init(_data.freighterHp, forward);
    }

    // ── 적 ─────────────────────────────────────────────

    private void SpawnDueWaves()
    {
        while (_nextWave < _data.waves.Length && _data.waves[_nextWave].time <= _elapsed)
        {
            SpawnWave(_data.waves[_nextWave]);
            _nextWave++;
        }
    }

    private void SpawnWave(EscortMissionData.Wave wave)
    {
        Vector3 center = _freighter != null ? _freighter.transform.position : Player.position;
        float baseAngle = Random.Range(0f, 360f);

        for (int i = 0; i < wave.count; i++)
        {
            float angle = (baseAngle + (i - (wave.count - 1) * 0.5f) * _enemySpread) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 position = center + dir * _enemySpawnDistance;
            position.y = Player.position.y;

            Transform target = wave.target == EscortMissionData.Priority.Freighter && _freighter != null
                ? _freighter.transform
                : Player;
            PirateShip enemy = SpawnEnemy(wave.shipClass, position, -dir, target);
            if (enemy == null)
                continue;

            if (_bonusPossible && wave.shipClass == _data.bonusShipClass)
            {
                _bonusShipsSpawned++;
                if (enemy.Hull != null)
                    enemy.Hull.OnBroken += () => _bonusShipsKilled++;
            }
        }
    }

    // ── 보너스 ─────────────────────────────────────────

    private void PrepareBonus()
    {
        EnemyShipCatalog.ShipClass bonusClass = string.IsNullOrEmpty(_data.bonusShipClass) || Catalog == null
            ? null
            : Catalog.Find(_data.bonusShipClass);

        _bonusPossible = bonusClass != null;
        _bonusWeapon = bonusClass != null ? bonusClass.weapon : default;
        _bonusLost = false;
        _bonusShipsSpawned = 0;
        _bonusShipsKilled = 0;
    }

    /// <summary>보너스 함종의 무기가 화물선에 닿으면 보너스를 잃는다.</summary>
    private void HandleHit(IDamageable target, F3DFXType weapon, WeaponOwner owner)
    {
        if (!_bonusPossible || _bonusLost || owner != WeaponOwner.Enemy || weapon != _bonusWeapon || _freighter == null)
            return;

        if (target is Component part && part.transform.IsChildOf(_freighter.transform))
            _bonusLost = true;
    }
}
