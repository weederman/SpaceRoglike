using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정거장 주변을 오가는 민간 화물선 (주변 교통, AmbientTraffic이 만든다). 호송 임무의 화물선 프리팹(EscortFreighter)을 바탕으로 쓰되,
/// 임무용 이동(EscortFreighter)은 떼고 이 컴포넌트가 움직인다.
///  1. 정거장 바깥의 한 지점까지 천천히 날아간다(Cruise).
///  2. 그다음 둘 중 하나로 떠난다.
///     - 도킹(Dock): 정거장 선체 쪽으로 다가가며 서서히 투명해져 사라진다.
///     - 이탈(Depart): 정거장 반대쪽으로 나아가다가 워프 게이트를 열고 돌진해 사라진다(WarpDash, 플레이어 워프와 같은 방식).
/// 아군(FriendlyShip)이라 플레이어의 무기에 맞지 않고, 해적은 노릴 수 있다. 선체가 깨지면 ShipDestructionEffect가 폭발시킨다.
/// </summary>
[RequireComponent(typeof(FriendlyShip))]
public class NpcFreighter : MonoBehaviour
{
    [Serializable]
    public class Settings
    {
        [Min(1f)] public float maxHp = 600f;
        [Min(0.1f)] public float speed = 22f;
        [Tooltip("진행 방향을 바꾸는 빠르기 (도/초)")]
        [Min(1f)] public float turnDegreesPerSecond = 20f;
        [Tooltip("도킹하면서 사라질 확률 (나머지는 정거장 반대쪽으로 워프해 떠난다)")]
        [Range(0f, 1f)] public float dockChance = 0.5f;
        [Tooltip("도킹할 때 정거장 선체 끝에서 이만큼 떨어진 곳에서 완전히 사라진다")]
        [Min(0f)] public float dockClearance = 60f;
        [Tooltip("도킹하며 투명해지는 시간 (초)")]
        [Min(0.1f)] public float fadeSeconds = 4f;
        [Tooltip("이탈할 때 정거장 반대쪽으로 이만큼 나아간 뒤 워프한다 (초)")]
        [Min(0f)] public float departSeconds = 8f;
        [Tooltip("워프 게이트가 충전된 뒤 이만큼 돌진하고 사라진다")]
        [Min(1f)] public float warpDistance = 400f;
        [Min(0.1f)] public float warpDashSeconds = 1.4f;
    }

    private enum Phase
    {
        Cruise,
        Dock,
        Depart,
        Warp
    }

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public HullHealth Hull { get; private set; }

    private Settings _settings;
    private StationZone _station;
    private GameObject _warpPrefab;
    private Rigidbody _rb;
    private Phase _phase;
    private Vector3 _goal;
    private bool _dock;
    private float _departUntil;

    private readonly List<Material> _fadeMaterials = new List<Material>();
    private readonly List<Color> _baseColors = new List<Color>();
    private float _fade = 1f;
    private bool _fading;

    /// <summary>
    /// waypoint(정거장 바깥)까지 날아간 뒤 도킹하거나 이탈한다. 처음 바라보는 방향은 waypoint 쪽.
    /// </summary>
    public void Init(Settings settings, StationZone station, Vector3 waypoint, GameObject warpPrefab)
    {
        _settings = settings;
        _station = station;
        _warpPrefab = warpPrefab;
        _goal = waypoint;
        _dock = UnityEngine.Random.value < settings.dockChance;
        _phase = Phase.Cruise;

        _rb = GetComponent<Rigidbody>();
        Hull = GetComponentInChildren<HullHealth>();
        if (Hull != null)
            Hull.SetMaxHp(settings.maxHp);

        Vector3 dir = waypoint - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);

        ShipMarker.Attach(gameObject, ShipMarker.Side.Friendly); // 아군 표시: 초록 원
    }

    private void FixedUpdate()
    {
        if (_settings == null || _phase == Phase.Warp || (Hull != null && !Hull.IsActive))
            return;

        if (_station == null)
        {
            Destroy(gameObject);
            return;
        }

        switch (_phase)
        {
            case Phase.Cruise:
                if (Fly(_goal))
                {
                    if (_dock)
                    {
                        _phase = Phase.Dock;
                        _goal = _station.PointOutside(transform.position - _station.transform.position,
                            _settings.dockClearance, transform.position.y);
                    }
                    else
                    {
                        _phase = Phase.Depart;
                        _departUntil = Time.time + _settings.departSeconds;
                        Vector3 away = transform.position - _station.transform.position;
                        away.y = 0f;
                        _goal = transform.position + away.normalized * 100000f; // 정거장 반대쪽으로 계속
                    }
                }
                break;

            case Phase.Dock:
                DockStep();
                break;

            case Phase.Depart:
                Fly(_goal);
                if (Time.time >= _departUntil && AlignedWith(_goal))
                {
                    _phase = Phase.Warp;
                    if (Hull != null)
                        Hull.Invincible = true; // 워프하는 동안은 피해를 받지 않는다
                    StartCoroutine(WarpRoutine());
                }
                break;
        }
    }

    /// <summary>goal 쪽으로 천천히 돌며 나아간다. 도착했으면 true.</summary>
    private bool Fly(Vector3 goal)
    {
        Vector3 to = goal - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance < 5f)
            return true;

        Quaternion want = Quaternion.LookRotation(to / distance);
        Quaternion rot = Quaternion.RotateTowards(transform.rotation, want,
            _settings.turnDegreesPerSecond * Time.fixedDeltaTime);
        Vector3 next = transform.position + rot * Vector3.forward * Mathf.Min(_settings.speed * Time.fixedDeltaTime, distance);
        Move(next, rot);
        return false;
    }

    private bool AlignedWith(Vector3 goal)
    {
        Vector3 to = goal - transform.position;
        to.y = 0f;
        return to.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, to) < 5f;
    }

    // ── 도킹 (서서히 사라진다) ─────────────────────────

    private void DockStep()
    {
        bool arrived = Fly(_goal);

        Vector3 to = _goal - transform.position;
        to.y = 0f;
        float secondsLeft = to.magnitude / Mathf.Max(0.1f, _settings.speed);
        if (!_fading && (arrived || secondsLeft <= _settings.fadeSeconds))
            BeginFade();

        if (_fading)
        {
            _fade = Mathf.MoveTowards(_fade, 0f, Time.fixedDeltaTime / _settings.fadeSeconds);
            ApplyFade();
            if (_fade <= 0f)
                Destroy(gameObject);
        }
    }

    /// <summary>
    /// 모델의 머티리얼을 복제해 반투명(Fade) 모드로 바꾼다. 체력바·편 표시 원은 바로 숨긴다.
    /// Standard 계열(_Mode가 있는) 셰이더가 아니면 알파는 건드리지 않고 크기만 줄인다.
    /// </summary>
    private void BeginFade()
    {
        _fading = true;

        foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
            canvas.gameObject.SetActive(false);

        var marker = GetComponent<ShipMarker>();
        if (marker != null)
            marker.enabled = false;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is LineRenderer || r is ParticleSystemRenderer)
                continue;

            foreach (Material m in r.materials)
            {
                if (!m.HasProperty("_Mode") || !m.HasProperty(ColorId))
                    continue;

                m.SetFloat("_Mode", 2f); // Fade
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                _fadeMaterials.Add(m);
                _baseColors.Add(m.GetColor(ColorId));
            }
        }
    }

    private void ApplyFade()
    {
        for (int i = 0; i < _fadeMaterials.Count; i++)
        {
            Color c = _baseColors[i];
            c.a *= _fade;
            _fadeMaterials[i].SetColor(ColorId, c);
        }

        // 정거장 안으로 들어가는 느낌이 나게 조금 작아진다 (반투명이 안 되는 셰이더도 사라지는 것이 보이게)
        if (_fadeMaterials.Count == 0)
            transform.localScale = Vector3.one * Mathf.Max(0.01f, _fade);
    }

    private void OnDestroy()
    {
        foreach (Material m in _fadeMaterials)
        {
            if (m != null)
                Destroy(m);
        }
    }

    // ── 이탈 (워프) ───────────────────────────────────

    private IEnumerator WarpRoutine()
    {
        Vector3 start = transform.position;
        Vector3 dir = transform.forward;
        GameObject fx = WarpDash.SpawnGate(_warpPrefab, start, dir);

        Action<Vector3> move = p => Move(p, transform.rotation);
        yield return WarpDash.Launch(start, dir * _settings.speed, WarpDash.GateChargeSeconds, dir,
            _settings.warpDistance, _settings.warpDashSeconds, move);

        if (fx != null)
            Destroy(fx, 3f);

        Destroy(gameObject);
    }

    private void Move(Vector3 position, Quaternion rotation)
    {
        if (_rb != null && _rb.isKinematic)
        {
            _rb.MovePosition(position);
            _rb.MoveRotation(rotation);
        }

        transform.SetPositionAndRotation(position, rotation);
    }
}
