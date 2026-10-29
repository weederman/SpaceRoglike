using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 목표 지정. Tab = 가장 가까운 목표(다시 누르면 그다음으로 가까운 목표), 목표 근처를 좌클릭 = 그 목표.
/// 목표는 플레이어 함선이 아닌 살아 있는 함선(HullHealth)과, 광물이 남은 광맥 소행성(MineableAsteroid)이다.
/// 지정하면 스토리에 신호 "target_locked"를 보낸다(광맥이면 "ore_locked"도).
/// 조준은 바꾸지 않는다(터렛은 계속 마우스를 따른다). 표시는 CombatHud가 Target을 읽어 그린다.
/// CombatHud 프리팹(Assets/Resources/UI/CombatHud.prefab)에 붙어 함께 생성된다.
/// </summary>
public class TargetingSystem : MonoBehaviour
{
    public const string LockedSignal = "target_locked";
    public const string OreLockedSignal = "ore_locked";

    public static TargetingSystem Instance { get; private set; }

    [Tooltip("목표 위를 클릭했다고 볼 화면 거리(픽셀). 함선이 작게 보여도 고르기 쉽게 넉넉히 둔다.")]
    [SerializeField, Min(0f)] private float _clickPickRadius = 70f;

    /// <summary>목표가 바뀌거나 사라졌을 때 (없으면 null)</summary>
    public event Action<Component> OnTargetChanged;

    /// <summary>지정한 목표 (HullHealth 또는 MineableAsteroid, 없으면 null)</summary>
    public Component Target { get; private set; }

    /// <summary>목표가 함선이면 그 선체 (아니면 null)</summary>
    public HullHealth Current => Target as HullHealth;

    /// <summary>목표가 광맥 소행성이면 그 소행성 (아니면 null)</summary>
    public MineableAsteroid CurrentOre => Target as MineableAsteroid;

    /// <summary>목표 함선의 쉴드 (없으면 null)</summary>
    public ShieldHealth CurrentShield { get; private set; }

    private SpaceshipController _player;
    private HullHealth _playerHull;
    private readonly List<Component> _candidates = new List<Component>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // 목표가 격파·고갈되었거나 오브젝트가 사라졌으면(파괴된 Unity 오브젝트는 == null) 해제
        if (!ReferenceEquals(Target, null) && !IsValid(Target))
            Clear();

        if (GameInputLock.IsLocked)
            return;

        if (Input.GetKeyDown(KeyCode.Tab))
            CycleNearest();
        else if (Input.GetMouseButtonDown(0) && !IsPointerOverUi())
            PickAtCursor();
    }

    /// <summary>목표의 화면/거리 기준 위치. 함선은 선체 판정 자식이 아니라 함선 중심(Rigidbody)이다.</summary>
    public static Vector3 PositionOf(Component target)
    {
        if (target is HullHealth)
        {
            Rigidbody ship = target.GetComponentInParent<Rigidbody>();
            if (ship != null)
                return ship.transform.position;
        }

        return target.transform.position;
    }

    /// <summary>가장 가까운 목표부터 차례로 고른다 (지금 목표가 있으면 그다음으로 가까운 목표).</summary>
    public void CycleNearest()
    {
        if (!CollectCandidates())
            return;

        Vector3 origin = _player.transform.position;
        _candidates.Sort((a, b) => (PositionOf(a) - origin).sqrMagnitude
            .CompareTo((PositionOf(b) - origin).sqrMagnitude));

        int index = Target != null ? _candidates.IndexOf(Target) : -1;
        Select(_candidates[(index + 1) % _candidates.Count]);
    }

    /// <summary>커서에서 가장 가까운(반경 안의) 목표를 고른다. 빈 곳을 클릭하면 목표를 유지한다(클릭은 발사이기도 하다).</summary>
    private void PickAtCursor()
    {
        Camera cam = Camera.main;
        if (cam == null || !CollectCandidates())
            return;

        Vector2 mouse = Input.mousePosition;
        Component best = null;
        float bestDistance = _clickPickRadius;

        foreach (Component candidate in _candidates)
        {
            Vector3 screen = cam.WorldToScreenPoint(PositionOf(candidate));
            if (screen.z <= 0f)
                continue;

            float distance = Vector2.Distance(mouse, screen);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        if (best != null)
            Select(best);
    }

    public void Select(Component target)
    {
        if (target == null)
            return;

        if (target != Target)
        {
            Target = target;

            // HullHealth는 함선 루트가 아니라 선체 판정 자식(HullHitbox)에 붙어 있으므로, 쉴드는 함선 루트(Rigidbody)에서 찾는다
            CurrentShield = null;
            if (target is HullHealth)
            {
                Rigidbody ship = target.GetComponentInParent<Rigidbody>();
                CurrentShield = (ship != null ? ship.transform : target.transform.root).GetComponentInChildren<ShieldHealth>();
            }

            OnTargetChanged?.Invoke(Target);
        }

        StoryPlayer.Signal(LockedSignal);
        if (target is MineableAsteroid)
            StoryPlayer.Signal(OreLockedSignal);
    }

    public void Clear()
    {
        if (ReferenceEquals(Target, null))
            return;

        Target = null;
        CurrentShield = null;
        OnTargetChanged?.Invoke(null);
    }

    private static bool IsValid(Component target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        if (target is HullHealth hull)
            return hull.IsActive;

        return target is MineableAsteroid ore && ore.HasOre;
    }

    private bool CollectCandidates()
    {
        if (_player == null)
        {
            _player = FindObjectOfType<SpaceshipController>();
            _playerHull = _player != null ? _player.GetComponentInChildren<HullHealth>() : null;
        }

        _candidates.Clear();
        if (_player == null)
            return false;

        foreach (HullHealth hull in FindObjectsOfType<HullHealth>())
        {
            if (hull != _playerHull && hull.IsActive && !FriendlyShip.Is(hull))
                _candidates.Add(hull);
        }

        foreach (MineableAsteroid ore in MineableAsteroid.All)
        {
            if (ore.HasOre)
                _candidates.Add(ore);
        }

        return _candidates.Count > 0;
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
