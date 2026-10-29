using UnityEngine;

/// <summary>
/// 정거장의 정보 창. 지금 도킹한 정거장의 이름, 항성계·궤도, 좌표, 항성까지 거리, 모항 여부, 이용할 수 있는 시설을 보여준다.
/// 값은 항성계 데이터(StarSystemData)와 정거장(StationZone)에서 읽는다.
/// </summary>
public class StationInfoWindow : StationWindow
{
    [SerializeField] private ItemDetailView _detail;
    [SerializeField] private Sprite _stationIcon;

    protected override void OnOpened()
    {
        SetTitle("정보");

        StationZone zone = Zone;
        string name = zone != null && !string.IsNullOrEmpty(zone.DisplayName) ? zone.DisplayName : "정거장";
        StarSystem system = StarSystem.Instance;
        StarSystemData data = system != null ? system.Data : null;
        bool known = data != null && zone != null && zone.StationIndex >= 0 && zone.StationIndex < data.stations.Length;
        string kind = zone != null && zone.IsHome ? "우주정거장 · 모항" : "우주정거장";

        if (!known)
        {
            _detail.Show(_stationIcon, name, kind, "");
            _detail.SetDescription("이 정거장의 항로 정보가 없습니다.");
            return;
        }

        StarSystemData.Station station = data.stations[zone.StationIndex];
        string orbit = data.orbits[station.orbit].name;
        Vector2 position = data.StationPosition(zone.StationIndex);

        _detail.Show(_stationIcon, name, kind, $"{data.systemName} 항성계 · {orbit} 궤도");
        _detail.SetDescription($"{data.systemName} 항성계 {orbit} 궤도를 도는 {kind}입니다.\n\n" +
                               "이용할 수 있는 시설: 격납고(정비·장비 교체), 상점, 임무 게시판, 항성계 지도");

        _detail.AddStat("항성계", data.systemName);
        _detail.AddStat("궤도", orbit);
        _detail.AddStat("좌표", $"{position.x:N0}, {position.y:N0}");
        _detail.AddStat("항성까지", $"{position.magnitude:N0}");
        _detail.AddStat("구분", zone.IsHome ? "모항" : "일반 정거장");
        _detail.ShowStats(true);
    }
}
