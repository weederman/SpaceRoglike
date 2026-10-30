using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 잔해 수색 임무 실행 담당 (메인 스토리 ① 4. 「버려진 화물」, 공통 부분은 MissionDirector).
///  1) 수락하면 목적지 비콘을 정하고 지도에 표시한다.
///  2) 도착하면 플레이어 앞쪽에 잔해 지대(잔해·파편·대기 중인 해적)가 생긴다.
///  3) 가장 가까운 미수색 잔해를 화면 표시로 알려 주고, 곁에 머무르면 수색해 특수 합금을 얻는다 (FirstShipQuest.AddAlloys).
///     해적은 가까이 가거나 공격하면 덤빈다. 피하든 격퇴하든 상관없다.
///  4) 필요량을 모두 모으면 성공 → FirstShipQuest가 다음 단계(고급 동력 코어)로.
/// 중단하거나 실패해도 이미 모은 특수 합금은 남고, 다시 출발하면 남은 양만큼 잔해가 생긴다.
/// 프리팹: Assets/Resources/Missions/SalvageMissionDirector.prefab
/// </summary>
public class SalvageMissionDirector : MissionDirector
{
    [Header("잔해 지대")]
    [Tooltip("도착한 플레이어 앞쪽으로 잔해 지대 중심까지의 거리")]
    [SerializeField, Min(0f)] private float _siteDistance = 1200f;
    [Tooltip("해적이 대기하는 곳: 잔해 지대 중심에서의 거리 (최소~최대). 바깥쪽 잔해는 들키지 않고 뒤질 수 있다")]
    [SerializeField] private Vector2 _guardDistance = new Vector2(60f, 280f);
    [Tooltip("잔해의 높이 (함선이 다니는 높이 기준, 음수면 아래로 지나간다)")]
    [SerializeField] private float _wreckHeight = -40f;
    [Tooltip("큰 잔해의 윗면이 함선이 다니는 높이보다 이만큼은 아래에 오게 더 내린다 (함선이 잔해를 뚫고 지나가 보이지 않게)")]
    [SerializeField, Min(0f)] private float _wreckClearance = 10f;
    [Tooltip("잔해를 눕혀 놓을 때 기울이는 최대 각도 (위에서 내려다볼 때 긴 변이 보이게 거의 눕힌다)")]
    [SerializeField, Range(0f, 90f)] private float _wreckTilt = 15f;

    private SalvageMissionData _data;
    private StarSystem.Location _destination = StarSystem.Location.None;
    private readonly List<SalvageWreck> _wrecks = new List<SalvageWreck>();
    private readonly List<GameObject> _siteObjects = new List<GameObject>();
    private bool _aggro;

    protected override bool TravelBlocked => false;

    protected override bool Setup(MissionData data)
    {
        _data = data as SalvageMissionData;
        return _data != null;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (StarMapUI.MissionTarget == _destination)
            StarMapUI.MissionTarget = StarSystem.Location.None;

        foreach (GameObject go in _siteObjects)
        {
            if (go != null)
                Destroy(go);
        }
    }

    // ── 진행 ───────────────────────────────────────────

    protected override IEnumerator Run()
    {
        yield return PlayStory(_data.acceptSequence);

        FirstShipQuestData quest = FirstShipQuest.Data;
        int remaining = quest.alloyRequired - FirstShipQuest.Alloys;
        Hud.Show(_data.title, _data.objective);

        StarSystem system = StarSystem.Instance;
        _destination = system != null ? NearestBeacon(system, _data.beaconLevels) : StarSystem.Location.None;
        if (_destination.IsNone)
        {
            yield return Fail($"레벨 {_data.beaconLevels.x}~{_data.beaconLevels.y} 워프 비콘을 찾지 못했습니다.");
            yield break;
        }

        // 이동
        StarMapUI.MissionTarget = _destination;
        Hud.SetStatus($"목적지 {system.LocationName(_destination)}");
        Hud.HideBar("M 지도에서 목적지로 워프하세요");
        while (!IsAt(system, _destination))
            yield return null;

        // 도착: 잔해 지대
        StarMapUI.MissionTarget = StarSystem.Location.None;
        SpawnSite(remaining);
        Hud.ShowNotice("잔해 지대 · 잔해 곁에 머무르면 수색합니다");

        while (FirstShipQuest.Alloys < quest.alloyRequired)
        {
            UpdateAggro();
            UpdateSearch(quest);
            yield return null;
        }

        CombatHud.HideWaypoint();
        FirstShipQuest.CompleteAlloyStage();
        yield return Succeed(_data, 0, $"{quest.alloyName} {quest.alloyRequired}개 회수",
            new[] { _aggro ? "잔해 지대의 해적과 교전" : "교전 없이 회수" });
    }

    private void UpdateSearch(FirstShipQuestData quest)
    {
        SalvageWreck target = _wrecks.Where(w => w != null && !w.Searched)
            .OrderBy(w => HorizontalDistance(w.transform.position, Player.position))
            .FirstOrDefault();

        int alive = AliveEnemies();
        Hud.SetEnemies(alive > 0 && _aggro ? alive : -1);

        if (target == null)
        {
            // 잔해를 다 뒤졌는데 모자라면 (데이터 실수) 남은 만큼 채운다
            FirstShipQuest.AddAlloys(quest.alloyRequired);
            return;
        }

        CombatHud.ShowWaypoint(target.transform, "잔해");
        float distance = HorizontalDistance(target.transform.position, Player.position);
        bool inRange = distance <= _data.salvageRange && !StarSystemWarp.IsWarping;

        if (inRange)
        {
            target.Progress += Time.deltaTime;
            Hud.SetBar("수색", Mathf.Clamp01(target.Progress / _data.salvageSeconds));
            Hud.SetStatus("잔해 수색 중 · 곁에 머무르세요");
            if (target.Progress >= _data.salvageSeconds)
                Search(target, quest);
        }
        else
        {
            Hud.SetBar(quest.alloyName, (float)FirstShipQuest.Alloys / quest.alloyRequired,
                $"{FirstShipQuest.Alloys} / {quest.alloyRequired}");
            Hud.SetStatus($"가까운 잔해까지 {distance:0}");
        }
    }

    private void Search(SalvageWreck wreck, FirstShipQuestData quest)
    {
        wreck.MarkSearched();
        if (wreck.Contents > 0)
        {
            FirstShipQuest.AddAlloys(wreck.Contents);
            Hud.ShowNotice($"{quest.alloyName} {wreck.Contents}개 회수 ({FirstShipQuest.Alloys} / {quest.alloyRequired})");
        }
        else
        {
            Hud.ShowNotice("쓸 만한 화물이 남아 있지 않습니다");
        }
    }

    // 해적: 가까이 오거나 맞으면 모두 덤빈다
    private void UpdateAggro()
    {
        if (_aggro || Player == null)
            return;

        foreach (PirateShip guard in Enemies)
        {
            if (!IsAlive(guard))
                continue;

            bool near = HorizontalDistance(guard.transform.position, Player.position) <= _data.aggroRadius;
            bool hurt = guard.Hull.CurrentHp < guard.Hull.MaxHp;
            ShieldHealth shield = guard.GetComponentInChildren<ShieldHealth>();
            if (shield != null && shield.Installed && shield.CurrentHp < shield.MaxHp)
                hurt = true;

            if (near || hurt)
            {
                _aggro = true;
                break;
            }
        }

        if (!_aggro)
            return;

        foreach (PirateShip guard in Enemies.Where(IsAlive))
            guard.Raid(Player);
        Hud.ShowNotice("해적이 눈치챘습니다");
    }

    // ── 잔해 지대 ──────────────────────────────────────

    private void SpawnSite(int alloys)
    {
        Vector3 forward = Player.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        Vector3 center = Player.position + forward * _siteDistance;
        center.y = Player.position.y;

        // 잔해: 합금을 고르게 나눠 담고, 빈 잔해를 섞는다
        int filled = Mathf.Max(1, _data.filledWrecks);
        var contents = new List<int>();
        for (int i = 0; i < filled; i++)
            contents.Add(alloys / filled + (i < alloys % filled ? 1 : 0));
        for (int i = 0; i < _data.emptyWrecks; i++)
            contents.Add(0);
        contents = contents.OrderBy(_ => Random.value).ToList();

        for (int i = 0; i < contents.Count; i++)
        {
            GameObject model = _data.wreckModels.Length > 0 ? _data.wreckModels[i % _data.wreckModels.Length] : null;
            Vector3 position = RingPoint(center, _data.wreckDistance, i, contents.Count);
            position.y = center.y + _wreckHeight;

            // 위에서 내려다볼 때 커 보이도록 거의 눕히고(방향만 무작위), 윗면이 함선 높이 아래에 오게 한다
            Quaternion lying = Quaternion.Euler(Random.Range(-_wreckTilt, _wreckTilt), Random.Range(0f, 360f), Random.Range(-_wreckTilt, _wreckTilt));
            GameObject go = SpawnModel(model, position, _data.wreckSize, false, lying);
            float over = WorldBounds(go).max.y - (center.y - _wreckClearance);
            if (over > 0f)
                go.transform.position += Vector3.down * over;
            go.name = $"Wreck_{i + 1}";
            SalvageWreck wreck = go.AddComponent<SalvageWreck>();
            wreck.SpinFlat = true;
            wreck.Contents = contents[i];
            _wrecks.Add(wreck);
        }

        // 파편: 함선 높이에 떠 있는 장애물
        for (int i = 0; i < _data.debrisCount && _data.debrisModels.Length > 0; i++)
        {
            GameObject model = _data.debrisModels[i % _data.debrisModels.Length];
            Vector3 position = RingPoint(center, new Vector2(80f, _data.wreckDistance.y + 150f), i, _data.debrisCount);
            float size = Random.Range(_data.debrisSize.x, _data.debrisSize.y);
            GameObject go = SpawnModel(model, position, size, true);
            go.name = $"Debris_{i + 1}";
            go.AddComponent<SalvageWreck>(); // 천천히 도는 것만 쓴다 (수색 대상 아님)
        }

        // 해적: 잔해 지대 곳곳에서 대기
        int n = 0;
        int total = _data.guards.Sum(g => g.count);
        foreach (SalvageMissionData.Group g in _data.guards)
        {
            for (int i = 0; i < g.count; i++, n++)
            {
                Vector3 point = RingPoint(center, _guardDistance, n, total);
                point.y = center.y;
                PirateShip guard = SpawnEnemy(g.shipClass, point, Player.position - point, Player);
                if (guard != null)
                    guard.Hold(point, true);
            }
        }
    }

    private static Vector3 RingPoint(Vector3 center, Vector2 distance, int index, int count)
    {
        float angle = (index + Random.Range(0.1f, 0.9f)) / Mathf.Max(1, count) * Mathf.PI * 2f;
        float d = Random.Range(distance.x, distance.y);
        return center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * d;
    }

    /// <summary>모델을 놓고 가장 긴 변을 size로 맞춘다. solid면 부딪히는 구 판정을 단다. rotation이 없으면 무작위로 돌린다.</summary>
    private GameObject SpawnModel(GameObject model, Vector3 position, float size, bool solid, Quaternion? rotation = null)
    {
        Quaternion rot = rotation ?? Random.rotation;
        GameObject go = model != null ? Instantiate(model, position, rot) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetPositionAndRotation(position, rot);
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
            Destroy(c);
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);

        Bounds bounds = WorldBounds(go);
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest > 0.001f)
            go.transform.localScale *= size / longest;

        if (solid)
        {
            var sphere = go.AddComponent<SphereCollider>();
            float scale = Mathf.Max(go.transform.lossyScale.x, 0.0001f);
            sphere.radius = size * 0.4f / scale;
            sphere.center = go.transform.InverseTransformPoint(WorldBounds(go).center);
        }

        _siteObjects.Add(go);
        return go;
    }

    private static Bounds WorldBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);
        return bounds;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
