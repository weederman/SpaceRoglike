using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 정거장의 임무 창. 같은 창을 두 곳에 쓴다 (_mode):
///  - 스페이스 라운지(Offers): 이 정거장에서 받을 수 있는 임무(MissionBoard가 정거장마다 무작위로 뽑은 _offerCount개)를 종류별로 보여주고,
///    고르면 오른쪽에 의뢰인·목표·보상과 '수락' 버튼이 나온다. 수락하면 창을 닫고 MissionDirector.TryStart로 임무를 시작한다 (수락 대사부터).
///    진행 중인 임무가 있으면 수락할 수 없다.
///  - 미션(Active): 지금 받은 임무를 보여준다. 진행 중인 채굴 임무는 납품처 정거장에서 '납품' 버튼으로 화물칸의 광석을 낸다
///    (MiningMissionDirector.Deliver).
/// 임무 목록은 Assets/Resources/Missions/ 아래의 임무 데이터(MissionData)다.
/// </summary>
public class MissionBoardWindow : StationWindow
{
    public enum Mode { Offers, Active }

    [SerializeField] private Mode _mode = Mode.Offers;
    [Tooltip("스페이스 라운지: 정거장마다 받을 수 있는 임무 수")]
    [SerializeField, Min(1)] private int _offerCount = 3;
    [SerializeField] private StationListView _list;
    [SerializeField] private ItemDetailView _detail;
    [Tooltip("호송 임무 아이콘")]
    [SerializeField] private Sprite _escortIcon;
    [Tooltip("해적 소탕 임무 아이콘")]
    [SerializeField] private Sprite _sweepIcon;
    [Tooltip("채굴 임무 아이콘")]
    [SerializeField] private Sprite _miningIcon;

    /// <summary>임무를 수락해 창들을 닫아야 할 때 (StationInteriorView가 받는다)</summary>
    public event System.Action Accepted;

    private string _selectedId;
    private string _message;

    protected override void OnOpened()
    {
        SetTitle(_mode == Mode.Offers ? "스페이스 라운지" : "미션");
        _message = null;
        _selectedId = null;
        Rebuild(false);
    }

    private void Rebuild(bool keepScroll)
    {
        _list.Clear(keepScroll);
        List<MissionData> missions = _mode == Mode.Offers ? MissionBoard.OffersAt(Zone, _offerCount) : ActiveMissions();
        StationListRow chosenRow = null;
        MissionData chosen = null;

        if (_mode == Mode.Active && missions.Count > 0)
            _list.AddHeader("진행 중인 임무");

        foreach (var group in missions.GroupBy(KindName))
        {
            if (_mode == Mode.Offers)
                _list.AddHeader(group.Key);

            foreach (MissionData mission in group)
            {
                MissionData m = mission;
                StationListRow row = _list.AddRow();
                bool running = IsRunning(m);
                row.Bind(IconOf(m), $"{m.id}  {m.title}", $"{m.tier} · {m.client}",
                    running ? "<color=#FFD166>진행 중</color>" : $"{m.reward:N0} CR", () => Select(m.id));

                if (m.id == _selectedId || chosen == null)
                {
                    chosen = m;
                    chosenRow = row;
                }
            }
        }

        if (chosen == null)
        {
            _detail.ShowEmpty(_mode == Mode.Offers
                ? "지금 이 정거장에서 받을 수 있는 임무가 없습니다."
                : "진행 중인 임무가 없습니다.\n스페이스 라운지에서 임무를 받을 수 있습니다.");
            return;
        }

        _selectedId = chosen.id;
        _list.Select(chosenRow);
        ShowMission(chosen);
    }

    private static List<MissionData> ActiveMissions()
    {
        var list = new List<MissionData>();
        if (MissionDirector.Current != null && MissionDirector.Current.Mission != null)
            list.Add(MissionDirector.Current.Mission);
        return list;
    }

    private void Select(string id)
    {
        if (id != _selectedId)
            _message = null;
        _selectedId = id;
        Rebuild(true);
    }

    private void ShowMission(MissionData m)
    {
        _detail.Show(IconOf(m), m.title, $"{m.reward:N0} CR", $"{m.tier} · {m.Summary}");

        string extra = m.extraRewards != null && m.extraRewards.Length > 0 ? $"\n추가 보상: {string.Join(", ", m.extraRewards)}" : "";
        _detail.SetDescription($"의뢰인: {m.client}\n\n{m.objective}{extra}");

        _detail.AddStat("번호", m.id);
        _detail.AddStat("종류", KindName(m));
        _detail.AddStat("난이도", m.tier);
        _detail.AddStat("의뢰인", m.client);
        _detail.AddStat("보수", $"{m.reward:N0} CR");
        if (m.extraRewards != null && m.extraRewards.Length > 0)
            _detail.AddStat("추가 보상", string.Join(", ", m.extraRewards));
        _detail.AddStat("내용", m.Summary);

        bool running = IsRunning(m);
        if (running && MissionDirector.Current is MiningMissionDirector mining)
        {
            ShowDelivery(mining);
            return;
        }

        if (running)
            _detail.SetPrimary("진행 중", false, null);
        else if (MissionDirector.IsRunning)
            _detail.SetPrimary("다른 임무 진행 중", false, null);
        else
            _detail.SetPrimary("수락", true, () => Accept(m));

        _detail.SetMessage(_message ?? (running && _mode == Mode.Offers ? "진행 상황은 미션 탭에서 볼 수 있습니다." : null));
    }

    /// <summary>진행 중인 채굴 임무: 납품 현황과 '납품' 버튼</summary>
    private void ShowDelivery(MiningMissionDirector mining)
    {
        MiningMissionData data = mining.Data;
        _detail.AddStat("납품", $"{mining.Delivered:N0} / {data.amount:N0}", (float)mining.Delivered / data.amount);
        _detail.AddStat("납품처", mining.Station != null ? mining.Station.DisplayName : "-");
        _detail.AddStat("실은 양", $"{data.OreLabel} {Mathf.FloorToInt(mining.CarriedForMission()):N0}");

        int amount = mining.Deliverable(Zone, out string reason);
        if (amount > 0)
        {
            _detail.SetPrimary($"납품 · {amount:N0}개", true, () =>
            {
                int delivered = mining.Deliver(Zone);
                _message = mining.Delivered >= data.amount
                    ? $"목표량을 모두 납품했습니다. 임무 완료 · 보너스 +{data.reward:N0} CR"
                    : $"{data.OreLabel} {delivered:N0}개를 납품했습니다.";
                Rebuild(true);
            });
            _detail.SetMessage(_message);
        }
        else
        {
            _detail.SetPrimary("진행 중", false, null);
            _detail.SetMessage(_message ?? reason);
        }

        _detail.ShowStats(true);
    }

    private void Accept(MissionData m)
    {
        if (MissionDirector.TryStart(m.id, out string error))
        {
            Accepted?.Invoke();
            return;
        }

        _message = error;
        Rebuild(true);
    }

    private static bool IsRunning(MissionData m)
    {
        return MissionDirector.Current != null && MissionDirector.Current.Mission == m;
    }

    private static string KindName(MissionData m)
    {
        if (m is EscortMissionData) return "호송";
        if (m is SweepMissionData) return "해적 소탕";
        if (m is MiningMissionData) return "채굴";
        return "기타";
    }

    private Sprite IconOf(MissionData m)
    {
        if (m is EscortMissionData) return _escortIcon;
        if (m is MiningMissionData) return _miningIcon;
        return _sweepIcon;
    }
}
