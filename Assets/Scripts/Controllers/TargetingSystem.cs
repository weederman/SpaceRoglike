using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 목표 지정. Tab = 가장 가까운 적(다시 누르면 그다음으로 가까운 적), 적 근처를 좌클릭 = 그 적.
/// 적은 플레이어 함선이 아닌, 살아 있는 HullHealth다. 지정하면 스토리에 신호 "target_locked"를 보낸다.
/// 조준은 바꾸지 않는다(터렛은 계속 마우스를 따른다). 표시는 CombatHud가 Current를 읽어 그린다.
/// CombatHud 프리팹(Assets/Resources/UI/CombatHud.prefab)에 붙어 함께 생성된다.
/// </summary>
public class TargetingSystem : MonoBehaviour
{
    public const string LockedSignal = "target_locked";

    public static TargetingSystem Instance { get; private set; }

    [Tooltip("적 위를 클릭했다고 볼 화면 거리(픽셀). 함선이 작게 보여도 고르기 쉽게 넉넉히 둔다.")]
    [SerializeField, Min(0f)] private float _clickPickRadius = 70f;

    /// <summary>목표가 바뀌거나 사라졌을 때 (없으면 null)</summary>
    public event Action<HullHealth> OnTargetChanged;

    public HullHealth Current { get; private set; }

    /// <summary>목표 함선의 쉴드 (없으면 null)</summary>
    public ShieldHealth CurrentShield { get; private set; }

    private SpaceshipController _player;
    private HullHealth _playerHull;
    private readonly List<HullHealth> _candidates = new List<HullHealth>();

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
        // 목표가 격파되었거나 오브젝트가 사라졌으면(파괴된 Unity 오브젝트는 == null) 해제
        if (!ReferenceEquals(Current, null) && (Current == null || !Current.IsActive))
            Clear();

        if (GameInputLock.IsLocked)
            return;

        if (Input.GetKeyDown(KeyCode.Tab))
            CycleNearest();
        else if (Input.GetMouseButtonDown(0) && !IsPointerOverUi())
            PickAtCursor();
    }

    /// <summary>가장 가까운 적부터 차례로 고른다 (지금 목표가 있으면 그다음으로 가까운 적).</summary>
    public void CycleNearest()
    {
        if (!CollectCandidates())
            return;

        Vector3 origin = _player.transform.position;
        _candidates.Sort((a, b) => (a.transform.position - origin).sqrMagnitude
            .CompareTo((b.transform.position - origin).sqrMagnitude));

        int index = Current != null ? _candidates.IndexOf(Current) : -1;
        Select(_candidates[(index + 1) % _candidates.Count]);
    }

    /// <summary>커서에서 가장 가까운(반경 안의) 적을 고른다. 빈 곳을 클릭하면 목표를 유지한다(클릭은 발사이기도 하다).</summary>
    private void PickAtCursor()
    {
        Camera cam = Camera.main;
        if (cam == null || !CollectCandidates())
            return;

        Vector2 mouse = Input.mousePosition;
        HullHealth best = null;
        float bestDistance = _clickPickRadius;

        foreach (HullHealth candidate in _candidates)
        {
            Vector3 screen = cam.WorldToScreenPoint(candidate.transform.position);
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

    public void Select(HullHealth target)
    {
        if (target == null)
            return;

        if (target != Current)
        {
            Current = target;

            // HullHealth는 함선 루트가 아니라 선체 판정 자식(HullHitbox)에 붙어 있으므로, 쉴드는 함선 루트(Rigidbody)에서 찾는다
            Rigidbody ship = target.GetComponentInParent<Rigidbody>();
            CurrentShield = (ship != null ? ship.transform : target.transform.root).GetComponentInChildren<ShieldHealth>();
            OnTargetChanged?.Invoke(Current);
        }

        StoryPlayer.Signal(LockedSignal);
    }

    public void Clear()
    {
        if (ReferenceEquals(Current, null))
            return;

        Current = null;
        CurrentShield = null;
        OnTargetChanged?.Invoke(null);
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
            if (hull != _playerHull && hull.IsActive)
                _candidates.Add(hull);
        }

        return _candidates.Count > 0;
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
