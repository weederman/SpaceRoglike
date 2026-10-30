using System.Collections.Generic;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 플레이어 편 NPC 함선 (정거장 경비선). EnemyShipCatalog.SpawnAlly로 만들고, 주변 교통(AmbientTraffic)이 순찰할 정거장을 정한다.
///  Patrol : 정거장 선체 바깥을 한 바퀴씩 돌며 떠다닌다 (쏘지 않는다)
///  Engage : 가까이에 해적이 나타나면 가장 가까운 해적에게 다가가 그 주위를 돌며 쏜다(해적의 Raid와 같은 방식).
///           해적이 모두 사라지거나 정거장에서 너무 멀어지면 다시 순찰한다.
/// 아군 표시(FriendlyShip)라 플레이어의 무기에 맞지 않고, 이 배의 무기(WeaponOwner.Ally)도 해적에게만 맞는다.
/// 이동은 해적선과 같이 Rigidbody에 힘을 주어 한다. 행동 수치는 함종의 Tuning을 쓴다
/// (approachMaxSpeed: 적에게 다가갈 때, attackDistance/attackMaxSpeed: 적 주위를 돌 때, fireRange/fireInterval, separation, raidLeadAngle).
/// </summary>
public class AllyShip : MonoBehaviour
{
    public enum Mode
    {
        Patrol,
        Engage
    }

    private const float RetargetInterval = 0.5f;

    private static readonly List<AllyShip> Active = new List<AllyShip>();

    public Mode Current { get; private set; }
    public HullHealth Hull { get; private set; }

    /// <summary>지금 노리는 해적 (없으면 null)</summary>
    public PirateShip Target { get; private set; }

    private PirateShip.Tuning _tuning;
    private Rigidbody _rb;
    private EnemyTurretController[] _turrets;
    private bool _turretsOn;
    private Transform _aimedAt;

    // 순찰
    private StationZone _station;
    private Vector2 _clearance;
    private float _stepDegrees;
    private float _patrolSpeed;
    private float _engageRange;
    private float _leashDistance;
    private float _patrolAngle;
    private float _patrolSign = 1f;
    private Vector3 _patrolPoint;
    private float _nextRetarget;
    private float _orbitSign = 1f;

    public void Init(PirateShip.Tuning tuning)
    {
        _tuning = tuning;

        _rb = GetComponent<Rigidbody>();
        _turrets = GetComponentsInChildren<EnemyTurretController>(true);
        Hull = GetComponentInChildren<HullHealth>();

        ShipMovementBase.ConfigureBody(_rb);
        _rb.isKinematic = false;
        SetTurrets(false, true);

        // 적 프리팹을 바탕으로 만들어 나타날 때 워프 도착음이 나는데, 늘 떠 있는 경비선이라 끈다
        AudioSource arrival = GetComponentInChildren<AudioSource>();
        if (arrival != null)
            arrival.Stop();

        ShipMarker.Attach(gameObject, ShipMarker.Side.Friendly); // 아군 표시: 초록 원
    }

    /// <summary>
    /// station 선체 바깥 clearance(최소~최대) 거리에서 stepDegrees씩 돌며 순찰한다.
    /// 해적이 engageRange 안에 들어오면 싸우고, 정거장 선체에서 leashDistance보다 멀어지면 돌아온다.
    /// </summary>
    public void Patrol(StationZone station, Vector2 clearance, float stepDegrees, float patrolSpeed, float engageRange,
        float leashDistance)
    {
        _station = station;
        _clearance = clearance;
        _stepDegrees = stepDegrees;
        _patrolSpeed = patrolSpeed;
        _engageRange = engageRange;
        _leashDistance = leashDistance;
        _patrolSign = Random.value < 0.5f ? -1f : 1f;

        // 지금 있는 자리의 각도에서 순찰을 시작한다
        Vector3 from = transform.position - station.transform.position;
        _patrolAngle = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
        NextPatrolPoint();
        Current = Mode.Patrol;
    }

    private void OnEnable()
    {
        Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private void FixedUpdate()
    {
        if (_tuning == null || _station == null || Hull == null || !Hull.IsActive)
            return;

        if (Time.time >= _nextRetarget)
        {
            _nextRetarget = Time.time + RetargetInterval;
            Retarget();
        }

        if (Current == Mode.Engage && Target != null)
            EngageStep();
        else
            PatrolStep();
    }

    // ── 순찰 ──────────────────────────────────────────

    private void PatrolStep()
    {
        SetTurrets(false);

        Vector3 to = _patrolPoint - _rb.position;
        to.y = 0f;
        if (to.magnitude < 40f)
            NextPatrolPoint();

        Vector3 velocity = SteerTo(_patrolPoint, _patrolSpeed);
        Face(velocity);
    }

    private void NextPatrolPoint()
    {
        _patrolAngle += _patrolSign * _stepDegrees;
        float rad = _patrolAngle * Mathf.Deg2Rad;
        var dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        _patrolPoint = _station.PointOutside(dir, Random.Range(_clearance.x, _clearance.y), _rb.position.y);
    }

    // ── 교전 ──────────────────────────────────────────

    /// <summary>가장 가까운 해적을 노린다. 해적이 없거나 정거장에서 너무 멀어졌으면 순찰로 돌아간다.</summary>
    private void Retarget()
    {
        PirateShip best = null;
        float bestDistance = _engageRange;
        foreach (PirateShip pirate in PirateShip.All)
        {
            if (!pirate.IsHostileTarget || _station.DistanceToHull(pirate.transform.position) > _leashDistance)
                continue;

            float d = HorizontalDistance(pirate.transform.position, _rb.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = pirate;
            }
        }

        if (best != Target)
            _orbitSign = Random.value < 0.5f ? -1f : 1f;

        Target = best;
        Current = best != null ? Mode.Engage : Mode.Patrol;
    }

    /// <summary>노리는 해적에게 다가와(멀면 approachMaxSpeed) attackDistance 거리로 그 주위를 돌며, 사거리 안이면 쏜다.</summary>
    private void EngageStep()
    {
        Transform target = Target.transform;
        if (_aimedAt != target)
        {
            _aimedAt = target;
            foreach (EnemyTurretController turret in _turrets)
                turret.target = target;
        }

        Vector3 from = _rb.position - target.position;
        from.y = 0f;
        bool inRange = from.magnitude <= _tuning.fireRange;

        float angle = Mathf.Atan2(from.x, from.z) + _orbitSign * _tuning.raidLeadAngle * Mathf.Deg2Rad;
        Vector3 goal = target.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * _tuning.attackDistance;
        goal.y = _rb.position.y;

        SetTurrets(inRange);
        Vector3 velocity = SteerTo(goal, inRange ? _tuning.attackMaxSpeed : _tuning.approachMaxSpeed);
        Face(inRange ? -from : velocity);
    }

    // ── 이동 ──────────────────────────────────────────

    /// <summary>목표 지점으로 다가가며 가까워질수록 느려진다 (아군끼리 밀어내기 포함). 조향 뒤의 수평 속도를 돌려준다.</summary>
    private Vector3 SteerTo(Vector3 goal, float maxSpeed)
    {
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
        {
            velocity = velocity.normalized * maxSpeed;
            _rb.velocity = velocity;
        }

        return velocity;
    }

    private void Face(Vector3 look)
    {
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, Quaternion.LookRotation(look), 2f * Time.fixedDeltaTime));
    }

    /// <summary>가까운 아군 함선에게서 멀어지는 방향 (0~1 세기)</summary>
    private Vector3 Separation()
    {
        Vector3 push = Vector3.zero;
        foreach (AllyShip other in Active)
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

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
