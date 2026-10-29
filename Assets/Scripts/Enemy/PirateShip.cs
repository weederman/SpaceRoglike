using System;
using System.Collections;
using System.Collections.Generic;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 튜토리얼(장면 4) 해적선의 행동 상태. CombatTutorialDirector가 적 프리팹(EnEmy)을 생성한 뒤 붙이고 상태를 바꾼다.
///  Hold  : 정해진 위치로 다가와 플레이어를 바라보며 멈춰 있다 (쏘지 않는다. 통신 대화 중)
///  Fight : 평소에는 플레이어에게서 떨어진 암석 뒤에 숨어 천천히 떠다닌다(Lurk). 플레이어가 가까이 오면 맞서 쏜다.
///          Director가 공격 신호를 주면 숨어 있던 배들이 한 방향으로 모여 들어와 쏘고(Attack), 신호가 끝나면 다시 흩어진다.
///  Flee  : 쏘지 않고 플레이어 반대쪽으로 도망친다. 너무 멀어지면 궁지에 몰려 다시 싸운다
///  Warp  : 교전을 포기하고 워프한다. 플레이어 워프와 같이 게이트가 충전되는 동안 멈췄다가 점점 세게 가속하며 사라진다
///          (WarpDash, 격파가 아니므로 폭발/보상 없음)
/// 이동은 이 컴포넌트가 직접 한다(기존 공전 AI EnemyOrbitController는 꺼 둔다). 사격은 기존 EnemyTurretController를 켜고 끈다.
/// 수치는 Director 프리팹의 Tuning에서 받는다.
/// </summary>
public class PirateShip : MonoBehaviour
{
    public enum Mode { Hold, Fight, Flee, Warp }

    [Serializable]
    public class Tuning
    {
        [Tooltip("대기 위치까지 다가가는 데 걸리는 대략의 시간(초)")]
        [Min(0.1f)] public float approachSmoothTime = 2.5f;
        [Min(1f)] public float approachMaxSpeed = 220f;

        [Header("전투 공통")]
        [Min(1f)] public float fightAcceleration = 24f;
        [Min(1f)] public float fireRange = 240f;
        [Tooltip("터렛 하나의 발사 간격(초). 적 발칸 공격력(WeaponStatsTable)이 높아 튜토리얼에서는 길게 둔다.")]
        [Min(0.1f)] public float fireInterval = 4.5f;
        [Tooltip("해적선끼리 이보다 가까우면 서로 밀어낸다")]
        [Min(0f)] public float separation = 45f;

        [Header("숨기 (Lurk)")]
        [Tooltip("숨을 암석을 고르는 플레이어와의 거리 범위")]
        public Vector2 lurkDistance = new Vector2(170f, 300f);
        [Min(1f)] public float lurkMaxSpeed = 26f;
        [Tooltip("숨은 자리 주위를 떠다니는 반지름")]
        [Min(0f)] public float lurkWander = 18f;
        [Tooltip("숨어 있다가도 플레이어가 이 거리 안에 들어오면 맞서 쏜다")]
        [Min(0f)] public float lurkDefendRange = 150f;

        [Header("모여서 공격 (Attack)")]
        [Tooltip("공격할 때 플레이어와 유지하는 거리")]
        [Min(1f)] public float attackDistance = 110f;
        [Min(1f)] public float attackMaxSpeed = 48f;

        [Header("도주")]
        [Min(1f)] public float fleeMaxSpeed = 40f;
        [Min(1f)] public float fleeAcceleration = 30f;
        [Tooltip("플레이어와 이만큼 멀어지면 도주를 멈추고 다시 싸운다")]
        [Min(1f)] public float fleeGiveUpDistance = 650f;

        [Header("워프 이탈")]
        [Min(0f)] public float warpTurnSeconds = 1.0f;
        [Min(0.5f)] public float warpMaxSeconds = 5f;
        [Tooltip("게이트가 충전된 뒤 이만큼 돌진하고 사라진다")]
        [Min(1f)] public float warpVanishDistance = 160f;
        [Tooltip("돌진하는 시간(초). 짧을수록 세게 가속한다.")]
        [Min(0.1f)] public float warpDashSeconds = 1.2f;
    }

    /// <summary>워프로 사라졌을 때 (격파가 아님)</summary>
    public event Action<PirateShip> OnWarpedOut;

    public Mode Current { get; private set; }
    public HullHealth Hull { get; private set; }
    public bool IsAttacking => Current == Mode.Fight && _attacking;

    // 전투 중인 해적선들 (숨을 암석이 겹치지 않게, 서로 밀어내기 위해)
    private static readonly List<PirateShip> Fighting = new List<PirateShip>();
    private static readonly List<AsteroidField.Rock> RockBuffer = new List<AsteroidField.Rock>();

    private Tuning _tuning;
    private Transform _player;
    private GameObject _warpPrefab;
    private Rigidbody _rb;
    private EnemyOrbitController _orbit;
    private EnemyTurretController[] _turrets;
    private ShieldHealth _shield;

    private Vector3 _holdPoint;
    private Vector3 _holdVelocity;

    // Fight
    private bool _attacking;
    private Vector3 _attackDirection;
    private bool _hasCover;
    private Vector3 _coverRock;
    private Vector3 _coverPoint;
    private Vector3 _wanderOffset;
    private float _nextWander;
    private float _nextCoverCheck;
    private bool _turretsOn;

    public void Init(Tuning tuning, Transform player, GameObject warpPrefab)
    {
        _tuning = tuning;
        _player = player;
        _warpPrefab = warpPrefab;

        _rb = GetComponent<Rigidbody>();
        _orbit = GetComponent<EnemyOrbitController>();
        _turrets = GetComponentsInChildren<EnemyTurretController>(true);
        _shield = GetComponentInChildren<ShieldHealth>();
        Hull = GetComponentInChildren<HullHealth>();

        // 이동은 이 컴포넌트가 직접 한다
        if (_orbit != null)
            _orbit.enabled = false;
    }

    private void OnDisable()
    {
        Fighting.Remove(this);
    }

    /// <summary>대기 위치로 다가가 멈춘다. snap이면 다가가는 과정 없이 바로 그 자리에 둔다(건너뛰기).</summary>
    public void Hold(Vector3 point, bool snap)
    {
        _holdPoint = point;
        SetMode(Mode.Hold);

        if (snap)
            SnapToHold();
    }

    public void SnapToHold()
    {
        if (Current != Mode.Hold)
            return;

        Quaternion rot = FacePlayer(_holdPoint);
        transform.SetPositionAndRotation(_holdPoint, rot);
        _rb.position = _holdPoint;
        _rb.rotation = rot;
        _holdVelocity = Vector3.zero;
    }

    public void SetMode(Mode mode)
    {
        if (Current == Mode.Warp)
            return; // 워프는 되돌리지 않는다

        Current = mode;

        bool physics = mode == Mode.Fight || mode == Mode.Flee;
        _rb.isKinematic = !physics;

        if (mode == Mode.Fight)
        {
            if (!Fighting.Contains(this))
                Fighting.Add(this);

            _attacking = false;
            _hasCover = false;
        }
        else
        {
            Fighting.Remove(this);
        }

        SetTurrets(false, true);

        if (mode == Mode.Warp)
            StartCoroutine(WarpRoutine());
    }

    /// <summary>모여서 공격: direction(플레이어 기준 수평 방향) 쪽에서 플레이어에게 다가가 쏜다.</summary>
    public void Attack(Vector3 direction)
    {
        if (Current != Mode.Fight)
            return;

        direction.y = 0f;
        _attackDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
        _attacking = true;
    }

    /// <summary>공격을 끝내고 흩어져 새 암석 뒤로 숨는다.</summary>
    public void Disperse()
    {
        if (Current != Mode.Fight || !_attacking)
            return;

        _attacking = false;
        _hasCover = false;
    }

    private void FixedUpdate()
    {
        if (_player == null)
            return;

        switch (Current)
        {
            case Mode.Hold:
            {
                Vector3 next = Vector3.SmoothDamp(_rb.position, _holdPoint, ref _holdVelocity,
                    _tuning.approachSmoothTime, _tuning.approachMaxSpeed, Time.fixedDeltaTime);
                _rb.MovePosition(next);
                _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, FacePlayer(next), 4f * Time.fixedDeltaTime));
                break;
            }

            case Mode.Fight:
                FightStep();
                break;

            case Mode.Flee:
                FleeStep();
                break;
        }
    }

    // ── 전투 ──────────────────────────────────────────

    private void FightStep()
    {
        Vector3 playerPos = _player.position;
        Vector3 toPlayer = playerPos - _rb.position;
        toPlayer.y = 0f;
        float playerDistance = toPlayer.magnitude;

        Vector3 goal;
        float maxSpeed;
        bool fire;

        if (_attacking)
        {
            goal = playerPos + _attackDirection * _tuning.attackDistance;
            maxSpeed = _tuning.attackMaxSpeed;
            fire = playerDistance <= _tuning.fireRange;
        }
        else
        {
            UpdateCover(playerPos);
            goal = _coverPoint + _wanderOffset;
            maxSpeed = _tuning.lurkMaxSpeed;
            fire = playerDistance <= _tuning.lurkDefendRange;
        }

        goal.y = _rb.position.y;
        SetTurrets(fire);

        // 목표 지점으로 다가가며 가까워질수록 느려진다 (도착 조향)
        Vector3 to = goal - _rb.position;
        to.y = 0f;
        float distance = to.magnitude;
        Vector3 desired = distance > 0.5f ? to / distance * Mathf.Min(maxSpeed, distance * 0.6f) : Vector3.zero;
        desired += Separation() * maxSpeed;

        Vector3 velocity = _rb.velocity;
        velocity.y = 0f;
        Vector3 steer = Vector3.ClampMagnitude((desired - velocity) * 2f, _tuning.fightAcceleration);
        _rb.AddForce(steer, ForceMode.Acceleration);

        velocity = _rb.velocity;
        velocity.y = 0f;
        if (velocity.magnitude > maxSpeed)
            _rb.velocity = velocity.normalized * maxSpeed;

        // 빠르게 움직일 때는 진행 방향을, 공격 중이거나 거의 멈춰 있을 때는 플레이어를 바라본다
        Vector3 look = !_attacking && velocity.magnitude > maxSpeed * 0.5f ? velocity : toPlayer;
        if (look.sqrMagnitude > 0.01f)
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, Quaternion.LookRotation(look), 2f * Time.fixedDeltaTime));
    }

    /// <summary>숨을 자리를 정하고, 그 자리가 쓸모없어지면(플레이어가 멀어짐, 암석이 옮겨짐) 새로 고른다.</summary>
    private void UpdateCover(Vector3 playerPos)
    {
        if (_hasCover && Time.time >= _nextCoverCheck)
        {
            _nextCoverCheck = Time.time + 1f;

            Vector3 d = _coverPoint - playerPos;
            d.y = 0f;
            if (d.magnitude > _tuning.lurkDistance.y + 120f || !RockStillThere(_coverRock))
                _hasCover = false;
        }

        if (!_hasCover)
            PickCover(playerPos);

        if (Time.time >= _nextWander)
        {
            _nextWander = Time.time + UnityEngine.Random.Range(2.5f, 4.5f);
            Vector2 w = UnityEngine.Random.insideUnitCircle * _tuning.lurkWander;
            _wanderOffset = new Vector3(w.x, 0f, w.y);
        }
    }

    private void PickCover(Vector3 playerPos)
    {
        _hasCover = true;
        _nextCoverCheck = Time.time + 1f;

        float bestScore = float.NegativeInfinity;
        bool found = false;

        if (AsteroidField.Active != null)
        {
            AsteroidField.Active.GetRocks(RockBuffer);
            foreach (AsteroidField.Rock rock in RockBuffer)
            {
                Vector3 fromPlayer = rock.position - playerPos;
                fromPlayer.y = 0f;
                float r = fromPlayer.magnitude;
                if (r < _tuning.lurkDistance.x || r > _tuning.lurkDistance.y || IsCoverTaken(rock.position))
                    continue;

                // 가까운 암석을 고르되, 매번 같은 곳만 고르지 않도록 무작위를 섞는다
                Vector3 fromMe = rock.position - _rb.position;
                fromMe.y = 0f;
                float score = -fromMe.magnitude + UnityEngine.Random.Range(0f, 120f);
                if (score > bestScore)
                {
                    bestScore = score;
                    found = true;
                    _coverRock = rock.position;
                    // 플레이어 쪽에서 봤을 때 암석 바로 뒤
                    _coverPoint = rock.position + fromPlayer.normalized * (rock.radius * 0.6f + 12f);
                }
            }
        }

        if (!found)
        {
            // 암석이 없으면 플레이어 주위의 아무 곳 (지금 있는 쪽에서 크게 벗어나지 않게)
            Vector3 away = _rb.position - playerPos;
            away.y = 0f;
            float angle = Mathf.Atan2(away.x, away.z) + UnityEngine.Random.Range(-1f, 1f);
            float r = UnityEngine.Random.Range(_tuning.lurkDistance.x, _tuning.lurkDistance.y);
            _coverRock = Vector3.positiveInfinity;
            _coverPoint = playerPos + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * r;
        }
    }

    private bool IsCoverTaken(Vector3 rock)
    {
        foreach (PirateShip other in Fighting)
        {
            if (other != this && other._hasCover && !other._attacking && (other._coverRock - rock).sqrMagnitude < 1f)
                return true;
        }

        return false;
    }

    private static bool RockStillThere(Vector3 rock)
    {
        if (float.IsInfinity(rock.x))
            return true; // 암석 없이 고른 자리

        if (AsteroidField.Active == null)
            return false;

        AsteroidField.Active.GetRocks(RockBuffer);
        foreach (AsteroidField.Rock r in RockBuffer)
        {
            if ((r.position - rock).sqrMagnitude < 1f)
                return true;
        }

        return false;
    }

    /// <summary>가까운 해적선에게서 멀어지는 방향 (0~1 세기)</summary>
    private Vector3 Separation()
    {
        Vector3 push = Vector3.zero;
        foreach (PirateShip other in Fighting)
        {
            if (other == this)
                continue;

            Vector3 d = _rb.position - other._rb.position;
            d.y = 0f;
            float distance = d.magnitude;
            if (distance > 0.01f && distance < _tuning.separation)
                push += d / distance * (1f - distance / _tuning.separation);
        }

        return Vector3.ClampMagnitude(push, 1f);
    }

    private void SetTurrets(bool on, bool force = false)
    {
        if (_turretsOn == on && !force)
            return;

        _turretsOn = on;
        foreach (EnemyTurretController turret in _turrets)
        {
            turret.enabled = on;
            if (on)
            {
                turret.fireRange = _tuning.fireRange;
                turret.fireInterval = _tuning.fireInterval;
            }
            else if (turret.fxController != null)
            {
                turret.fxController.Stop();
            }
        }
    }

    // ── 도주 ──────────────────────────────────────────

    private void FleeStep()
    {
        Vector3 away = _rb.position - _player.position;
        away.y = 0f;

        if (away.magnitude > _tuning.fleeGiveUpDistance)
        {
            SetMode(Mode.Fight);
            return;
        }

        _rb.AddForce(away.normalized * _tuning.fleeAcceleration, ForceMode.Acceleration);

        Vector3 velocity = _rb.velocity;
        velocity.y = 0f;
        if (velocity.magnitude > _tuning.fleeMaxSpeed)
            _rb.velocity = velocity.normalized * _tuning.fleeMaxSpeed;

        if (velocity.sqrMagnitude > 0.1f)
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, Quaternion.LookRotation(velocity), 4f * Time.fixedDeltaTime));
    }

    // ── 워프 이탈 ─────────────────────────────────────

    private IEnumerator WarpRoutine()
    {
        // 워프 중에는 격파되지 않는다 (이미 도망치기로 한 배)
        if (Hull != null) Hull.Invincible = true;
        if (_shield != null) _shield.Invincible = true;

        Vector3 away = transform.position - (_player != null ? _player.position : transform.position - transform.forward);
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = transform.forward;

        Quaternion from = transform.rotation;
        Quaternion to = Quaternion.LookRotation(away.normalized);
        for (float t = 0f; t < _tuning.warpTurnSeconds; t += Time.deltaTime)
        {
            Quaternion rot = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / _tuning.warpTurnSeconds));
            transform.rotation = rot;
            _rb.rotation = rot;
            yield return null;
        }

        // 함선 자리에 게이트가 열리고, 충전되는 동안 멈췄다가 점점 세게 가속하며 사라진다
        Vector3 start = transform.position;
        Vector3 dir = away.normalized;
        GameObject fx = WarpDash.SpawnGate(_warpPrefab, start, dir);
        System.Action<Vector3> move = p =>
        {
            transform.position = p;
            _rb.position = p;
        };

        yield return WarpDash.Launch(start, Vector3.zero, WarpDash.GateChargeSeconds, dir,
            _tuning.warpVanishDistance, _tuning.warpDashSeconds, move);

        if (fx != null)
            Destroy(fx, 3f);

        OnWarpedOut?.Invoke(this);
        Destroy(gameObject);
    }

    private Quaternion FacePlayer(Vector3 from)
    {
        Vector3 dir = _player.position - from;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : transform.rotation;
    }
}
