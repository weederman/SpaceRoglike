using System.Collections.Generic;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 튜토리얼 장면 4("첫 교전") 연출 담당. "tutorial_combat"(통신 대화) → "tutorial_combat_fight"(조작하며 듣는 전투)
/// → "tutorial_combat_end"(마무리 대화) 시퀀스의 cue를 받아 해적선을 움직이고, 전투 상황을 스토리 신호로 알린다.
///
///  s4_pirates_spawn   해적선 3척이 레이더 신호 방향(RadarPing과 같은 각도)에서 나타나 다가와 멈춘다 (쏘지 않음)
///  s4_combat_start    해적선이 흩어져 암석 뒤에 숨고, 일정 간격으로 한 방향에 모여 공격해 온다.
///                     플레이어 선체에 하한을 건다(튜토리얼 중 격파 방지). 하한에 닿으면 "비상 프로토콜"이 발동해
///                     쉴드가 다시 켜지고 더는 깎이지 않는다(화면에 경고 표시).
///  s4_combat_end      하한을 풀고 남은 해적선을 정리한다
///
/// 보내는 신호 (JSON의 waitFor에 쓴다):
///  hit:무기이름     플레이어가 그 무기(F3DFXType 이름)로 해적선을 맞혔다 (예: hit:LaserImpulse)
///  player_hit       해적선의 공격이 플레이어에게 맞았다
///  pirate_down      첫 번째 해적선 격파 → 남은 한 척이 도주를 시작한다
///  pirate2_down     두 번째 해적선 격파 → 마지막 한 척이 워프로 이탈한다
///  pirate_escaped   마지막 해적선이 워프로 사라졌다
/// (목표 지정 신호 "target_locked"는 TargetingSystem이 보낸다)
///
/// cue는 모두 즉시 끝나는 상태 전환이라 큐 없이 바로 처리한다. 통신 대화를 Esc로 건너뛰면 해적선은 다가오는 과정 없이
/// 대기 위치에 바로 놓인다. 프리팹: Assets/Resources/Directors/Cue/CombatTutorialDirector.prefab
/// </summary>
public class CombatTutorialDirector : MonoBehaviour
{
    private const string IntroSequence = "tutorial_combat";
    private const string SpawnCue = "s4_pirates_spawn";
    private const string StartCue = "s4_combat_start";
    private const string EndCue = "s4_combat_end";

    private const string HitSignalPrefix = "hit:";
    private const string PlayerHitSignal = "player_hit";
    private const string FirstDownSignal = "pirate_down";
    private const string SecondDownSignal = "pirate2_down";
    private const string EscapedSignal = "pirate_escaped";

    // 전투 튜토리얼 단계가 기다리는 신호 (TutorialCombat.json의 waitFor 순서)
    private static readonly string[] TutorialStepSignals =
    {
        "target_locked",
        HitSignalPrefix + nameof(F3DFXType.LaserImpulse),
        HitSignalPrefix + nameof(F3DFXType.Vulcan),
        PlayerHitSignal,
        FirstDownSignal,
    };

    [Header("에셋 (프리팹에서 연결)")]
    [SerializeField] private GameObject _piratePrefab;
    [Tooltip("해적선이 워프로 이탈할 때의 이펙트 (FirstWarpDirector와 같은 워프 아웃)")]
    [SerializeField] private GameObject _warpOutPrefab;

    [Header("등장")]
    [Tooltip("해적선이 나타나는 방향 (위쪽=북쪽=0도, 시계 방향). 장면 3 레이더(RadarPing)의 신호 각도와 맞춘다.")]
    [SerializeField] private float[] _approachAngles = { 20f, 75f, -40f };
    [SerializeField, Min(1f)] private float _spawnDistance = 900f;
    [Tooltip("다가와 멈추는 거리 (플레이어 기준). 통신 대화 중에 화면 안에 보이는 거리.")]
    [SerializeField, Min(1f)] private float _holdDistance = 160f;

    [Header("해적선 행동")]
    [SerializeField] private PirateShip.Tuning _pirateTuning = new PirateShip.Tuning();

    [Header("해적선 체력 (적 프리팹 값 대신 튜토리얼에서만 쓴다)")]
    [Tooltip("레이저(쉴드 1.5배)로 약 3.5초, 발칸으로 약 5초. 레이저로 쉴드를 깨도록 유도할 만큼 두껍게.")]
    [SerializeField, Min(1f)] private float _pirateShieldHp = 200f;
    [Tooltip("발칸으로 약 3초, 레이저(선체 0.5배)로 약 6초. 쉴드를 깬 뒤 발칸으로 바꾸도록 유도.")]
    [SerializeField, Min(1f)] private float _pirateHullHp = 120f;

    [Header("모여서 공격하는 주기 (초)")]
    [Tooltip("전투 시작 후 첫 공격까지 (그동안 흩어져 숨는다)")]
    [SerializeField, Min(0f)] private float _firstAttackDelay = 9f;
    [Tooltip("공격과 공격 사이 숨어 있는 시간 (최소~최대)")]
    [SerializeField] private Vector2 _lurkSeconds = new Vector2(8f, 12f);
    [Tooltip("한 번 모여서 공격하는 시간 (최소~최대)")]
    [SerializeField] private Vector2 _attackSeconds = new Vector2(6f, 8f);
    [Tooltip("모여 들어올 때 해적선끼리 벌어지는 각도")]
    [SerializeField, Range(0f, 90f)] private float _attackSpread = 28f;

    [Header("플레이어")]
    [Tooltip("전투 동안 플레이어 선체가 이 비율 아래로 내려가지 않는다 (튜토리얼에서 격파되지 않게)")]
    [SerializeField, Range(0f, 1f)] private float _playerHullFloor = 0.3f;
    [Tooltip("선체가 하한에 닿았을 때 화면에 띄우는 경고")]
    [SerializeField] private string _emergencyTitle = "비상 프로토콜 발동";
    [SerializeField] private string _emergencyBody = "선체 손상 한계 도달 · 비상 방어막 전개";

    private readonly List<PirateShip> _pirates = new List<PirateShip>();
    private SpaceshipController _player;
    private HullHealth _playerHull;
    private ShieldHealth _playerShield;
    private bool _spawned;
    private bool _fighting;
    private bool _emergency;
    private int _kills;
    private bool _waveAttacking;
    private float _nextWaveSwitch;

    private void OnEnable()
    {
        StoryPlayer.OnCue += HandleCue;
        StoryPlayer.OnSequenceEnded += HandleEnded;
        HitResolver.OnHit += HandleHit;
    }

    private void OnDisable()
    {
        StoryPlayer.OnCue -= HandleCue;
        StoryPlayer.OnSequenceEnded -= HandleEnded;
        HitResolver.OnHit -= HandleHit;
        SetPlayerHullFloor(0f);
        EndEmergency();
    }

    private void Update()
    {
        if (!_fighting || Time.time < _nextWaveSwitch)
            return;

        if (_waveAttacking)
            EndWave();
        else
            StartWave();
    }

    private void HandleCue(string cue)
    {
        switch (cue)
        {
            case SpawnCue:
                SpawnPirates(StoryPlayer.IsSkipping);
                break;
            case StartCue:
                StartCombat();
                break;
            case EndCue:
                EndCombat();
                break;
        }
    }

    private void HandleEnded(string sequenceId, bool skipped)
    {
        // 통신 대화를 건너뛰면 다가오는 과정을 생략하고 대기 위치에 둔다
        if (sequenceId == IntroSequence && skipped)
        {
            foreach (PirateShip pirate in _pirates)
                pirate.SnapToHold();
        }
    }

    // ── 상태 전환 ──────────────────────────────────────

    private void SpawnPirates(bool snap)
    {
        if (_spawned || !EnsurePlayer())
            return;

        _spawned = true;
        _kills = 0;

        Vector3 center = _player.transform.position;
        for (int i = 0; i < _approachAngles.Length; i++)
        {
            float rad = _approachAngles[i] * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            Vector3 spawn = center + dir * _spawnDistance;

            GameObject go = Instantiate(_piratePrefab, spawn, Quaternion.LookRotation(-dir));
            go.name = $"Pirate_{i + 1}";

            var pirate = go.AddComponent<PirateShip>();
            pirate.Init(_pirateTuning, _player.transform, _warpOutPrefab);
            pirate.Hold(center + dir * _holdDistance, snap);
            pirate.OnWarpedOut += HandleWarpedOut;

            ShieldHealth shield = go.GetComponentInChildren<ShieldHealth>();
            if (shield != null)
                shield.SetMaxHp(_pirateShieldHp);

            if (pirate.Hull != null)
            {
                pirate.Hull.SetMaxHp(_pirateHullHp);
                pirate.Hull.OnBroken += () => HandlePirateDestroyed(pirate);
            }

            _pirates.Add(pirate);
        }
    }

    private void StartCombat()
    {
        if (_fighting)
            return;

        if (!_spawned)
            SpawnPirates(true);

        _fighting = true;
        SetPlayerHullFloor(_playerHullFloor);

        // 처음엔 흩어져 숨는다
        foreach (PirateShip pirate in _pirates)
            pirate.SetMode(PirateShip.Mode.Fight);

        _waveAttacking = false;
        _nextWaveSwitch = Time.time + _firstAttackDelay;
    }

    /// <summary>숨어 있던 해적선들이 한쪽 방향에 모여 함께 들어와 쏜다.</summary>
    private void StartWave()
    {
        _waveAttacking = true;
        _nextWaveSwitch = Time.time + Random.Range(_attackSeconds.x, _attackSeconds.y);

        var attackers = new List<PirateShip>();
        Vector3 center = Vector3.zero;
        foreach (PirateShip pirate in _pirates)
        {
            if (pirate != null && pirate.Current == PirateShip.Mode.Fight)
            {
                attackers.Add(pirate);
                center += pirate.transform.position;
            }
        }

        if (attackers.Count == 0 || !EnsurePlayer())
            return;

        // 해적선들이 많이 있는 쪽에서 들어온다. 겹치지 않게 조금씩 벌린다.
        Vector3 baseDir = center / attackers.Count - _player.transform.position;
        baseDir.y = 0f;
        if (baseDir.sqrMagnitude < 1f)
            baseDir = Vector3.forward;

        for (int i = 0; i < attackers.Count; i++)
        {
            float offset = (i - (attackers.Count - 1) * 0.5f) * _attackSpread;
            attackers[i].Attack(Quaternion.Euler(0f, offset, 0f) * baseDir);
        }
    }

    private void EndWave()
    {
        _waveAttacking = false;
        _nextWaveSwitch = Time.time + Random.Range(_lurkSeconds.x, _lurkSeconds.y);

        foreach (PirateShip pirate in _pirates)
        {
            if (pirate != null)
                pirate.Disperse();
        }
    }

    private void EndCombat()
    {
        _fighting = false;
        SetPlayerHullFloor(0f);
        EndEmergency();

        foreach (PirateShip pirate in _pirates)
        {
            if (pirate != null)
                Destroy(pirate.gameObject);
        }

        _pirates.Clear();
    }

    // ── 전투 상황 → 스토리 신호 ─────────────────────────

    private void HandleHit(IDamageable target, F3DFXType weapon, WeaponOwner owner)
    {
        if (!_fighting || !(target is Component component))
            return;

        Transform root = component.transform.root;

        if (owner == WeaponOwner.Player && IsPirate(root))
            StoryPlayer.Signal(HitSignalPrefix + weapon);
        else if (owner == WeaponOwner.Enemy && _player != null && root == _player.transform.root)
            StoryPlayer.Signal(PlayerHitSignal);
    }

    private void HandlePirateDestroyed(PirateShip pirate)
    {
        _pirates.Remove(pirate);
        _kills++;

        if (_kills == 1)
        {
            StoryPlayer.Signal(FirstDownSignal);

            // 남은 두 척 중 한 척은 암석 사이로 도망친다
            PirateShip runner = FirstAlive();
            if (runner != null)
                runner.SetMode(PirateShip.Mode.Flee);
        }
        else if (_kills == 2)
        {
            // 해적을 빨리 격파해 앞 단계(발칸 사용, 피격)를 거치지 못했어도 마지막 한 척이 떠나면
            // 그 단계를 다시 할 수 없으므로, 남은 튜토리얼 단계를 완료로 처리해 스토리가 멈추지 않게 한다
            foreach (string step in TutorialStepSignals)
                StoryPlayer.Signal(step);
            StoryPlayer.Signal(SecondDownSignal);

            // 마지막 한 척은 교전을 포기하고 워프한다
            PirateShip last = FirstAlive();
            if (last != null)
                last.SetMode(PirateShip.Mode.Warp);
            else
                StoryPlayer.Signal(EscapedSignal);
        }
    }

    private void HandleWarpedOut(PirateShip pirate)
    {
        _pirates.Remove(pirate);
        StoryPlayer.Signal(EscapedSignal);
    }

    // ── 공용 ──────────────────────────────────────────

    private bool IsPirate(Transform root)
    {
        foreach (PirateShip pirate in _pirates)
        {
            if (pirate != null && pirate.transform == root)
                return true;
        }

        return false;
    }

    private PirateShip FirstAlive()
    {
        foreach (PirateShip pirate in _pirates)
        {
            if (pirate != null && pirate.Current != PirateShip.Mode.Warp)
                return pirate;
        }

        return null;
    }

    private bool EnsurePlayer()
    {
        if (_player == null)
        {
            _player = FindObjectOfType<SpaceshipController>();
            _playerHull = _player != null ? _player.GetComponentInChildren<HullHealth>() : null;
            _playerShield = _player != null ? _player.GetComponentInChildren<ShieldHealth>() : null;
        }

        return _player != null;
    }

    private void SetPlayerHullFloor(float ratio)
    {
        if (!EnsurePlayer() || _playerHull == null)
            return;

        _playerHull.MinHpRatio = ratio;

        _playerHull.OnHpChanged -= HandlePlayerHull;
        if (ratio > 0f)
            _playerHull.OnHpChanged += HandlePlayerHull;
    }

    // ── 비상 프로토콜 ─────────────────────────────────

    /// <summary>선체가 하한에 닿으면 비상 프로토콜: 쉴드를 다시 켜서 더 맞지 않게 하고, 화면에 경고를 띄운다.</summary>
    private void HandlePlayerHull(float current, float max)
    {
        if (_emergency || !_fighting || current > max * _playerHullFloor + 0.5f)
            return;

        _emergency = true;

        if (_playerShield != null)
        {
            _playerShield.Restore(1f);
            _playerShield.Invincible = true; // 맞으면 쉴드 피격 연출만 나온다
        }

        CombatHud.ShowAlert(_emergencyTitle, _emergencyBody);
    }

    private void EndEmergency()
    {
        if (!_emergency)
            return;

        _emergency = false;

        if (_playerShield != null)
            _playerShield.Invincible = false;

        CombatHud.HideAlert();
    }
}
