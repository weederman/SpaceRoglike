using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 정거장의 임무 창. 같은 창을 두 곳에 쓴다 (_mode):
///  - 스페이스 라운지(Offers): 이 정거장에서 받을 수 있는 임무(MissionBoard가 정거장마다 무작위로 뽑은 _offerCount개)를 종류별로 보여주고,
///    고르면 오른쪽에 의뢰인·목표·보상과 '수락' 버튼이 나온다. 수락하면 창을 닫고 MissionDirector.TryStart로 임무를 시작한다 (수락 대사부터).
///    진행 중인 임무가 있으면 수락할 수 없다.
///  - 미션(Active): 지금 받은 임무를 보여준다. 진행 중인 채굴 임무는 납품처 정거장에서 '납품' 버튼으로 화물칸의 광석을 낸다
///    (MiningMissionDirector.Deliver). 운송 임무는 도착지 정거장에서 적을 모두 격파한 뒤 '납품'한다 (TransportMissionDirector.Deliver).
/// 임무 목록은 Assets/Resources/Missions/ 아래의 임무 데이터(MissionData)다.
/// 메인 스토리 ① 첫 번째 함선 퀘스트가 진행 중이면(FirstShipQuest.IsVisible) 두 탭 모두 맨 위에 '라스' 항목이 있고,
/// 단계별로 블루프린트 구매·다시 통신 / 광물 납품 / 특수 부품 임무 출발 / 최종 조립 버튼을 보여 준다.
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
    [Tooltip("운송 임무 아이콘")]
    [SerializeField] private Sprite _transportIcon;
    [Tooltip("메인 퀘스트(라스) 아이콘")]
    [SerializeField] private Sprite _mainQuestIcon;

    private const string MainQuestKey = "$first_ship";

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

        // 메인 퀘스트 (라스)
        if (!FirstShipQuest.IsVisible && _selectedId == MainQuestKey)
            _selectedId = null;
        if (FirstShipQuest.IsVisible)
        {
            _list.AddHeader("라스 · 메인 퀘스트");
            StationListRow row = _list.AddRow();
            row.Bind(_mainQuestIcon != null ? _mainQuestIcon : _transportIcon, MainQuestTitle(), "라스 · 1차 특수 함선",
                "<color=#FFD166>메인</color>", () => Select(MainQuestKey));
            if (_selectedId == MainQuestKey || _selectedId == null)
            {
                _selectedId = MainQuestKey;
                _list.Select(row);
                ShowMainQuest();
                chosenRow = row;
            }
        }

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

                if (_selectedId != MainQuestKey && (m.id == _selectedId || chosen == null))
                {
                    chosen = m;
                    chosenRow = row;
                }
            }
        }

        if (chosen == null && _selectedId == MainQuestKey && chosenRow != null)
            return;

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

        if (running && MissionDirector.Current is TransportMissionDirector transport)
        {
            ShowDelivery(transport);
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

    /// <summary>진행 중인 운송 임무: 화물·도착지와 '납품' 버튼</summary>
    private void ShowDelivery(TransportMissionDirector transport)
    {
        TransportMissionData data = transport.Data;
        CargoHold cargo = CargoHold.Instance;
        _detail.AddStat("화물", $"{data.cargoName} {(cargo != null ? cargo.MissionCargo : 0):N0} / {data.cargoAmount:N0}");
        _detail.AddStat("도착지", transport.Destination != null ? transport.Destination.DisplayName : "-");
        _detail.AddStat("도착지 적", transport.Cleared ? "격파 완료" : transport.Arrived ? "남아 있음" : "도착하면 나타남");

        if (transport.CanDeliver(Zone, out string reason))
        {
            _detail.SetPrimary($"납품 · {data.cargoName} {data.cargoAmount:N0}개", true, () =>
            {
                _message = transport.Deliver(Zone) ? $"화물을 납품했습니다. 임무 완료 · 보수 +{data.reward:N0} CR" : "납품하지 못했습니다.";
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

    // ── 메인 퀘스트 (라스) ─────────────────────────────

    private static string MainQuestTitle()
    {
        switch (FirstShipQuest.Current)
        {
            case FirstShipQuest.Stage.Offered: return "새로운 제안";
            case FirstShipQuest.Stage.Minerals: return "선체부터 준비해";
            case FirstShipQuest.Stage.Alloy: return "버려진 화물";
            case FirstShipQuest.Stage.Core: return "흔적을 쫓아서";
            default: return "새로운 함선";
        }
    }

    private void ShowMainQuest()
    {
        FirstShipQuestData data = FirstShipQuest.Data;
        if (data == null)
            return;

        FirstShipQuest.Stage stage = FirstShipQuest.Current;
        _detail.Show(_mainQuestIcon != null ? _mainQuestIcon : _transportIcon, MainQuestTitle(), "메인 퀘스트",
            "라스 · 1차 특수 함선 제작");

        string home = StationZone.Home != null ? StationZone.Home.DisplayName : "모항";
        switch (stage)
        {
            case FirstShipQuest.Stage.Offered:
                _detail.SetDescription($"라스가 함선 블루프린트를 제안했습니다. 가격은 {data.blueprintPrice:N0} CR이고, 제작 재료는 따로 모아야 합니다.\n\n" +
                    "블루프린트를 사면 재료를 모으는 퀘스트가 차례로 이어집니다.");
                break;
            case FirstShipQuest.Stage.Minerals:
                _detail.SetDescription($"선체를 만들 광물을 모아 {home}의 라스에게 납품합니다. 화물칸에 실은 만큼 여러 번 나눠 낼 수 있습니다.\n\n" +
                    "광물은 채굴하거나 정거장 상점에서 살 수 있습니다.");
                break;
            case FirstShipQuest.Stage.Alloy:
                _detail.SetDescription($"습격당한 군수품 화물선의 잔해를 수색해 {data.alloyName} {data.alloyRequired}개를 회수합니다.\n\n" +
                    "잔해 주변에는 먼저 온 해적이 있습니다. 피하든 격퇴하든 상관없습니다.");
                break;
            case FirstShipQuest.Stage.Core:
                _detail.SetDescription($"{data.coreName}을 실은 군수품 수송선을 추적해 격파합니다. 수송선은 호위 함대와 함께 달아납니다.\n\n" +
                    $"(선택) 지휘함을 격파하면 {data.commsLogName}을 얻습니다.");
                break;
            default:
                _detail.SetDescription($"모든 재료가 모였습니다. {home}의 라스에게서 최종 조립을 하면 새 함선을 받습니다.");
                break;
        }

        _detail.AddStat("블루프린트", FirstShipQuest.HasBlueprint ? "보유" : $"{data.blueprintPrice:N0} CR");
        OreCatalog ores = OreCatalog.Load();
        CargoHold cargo = CargoHold.Instance;
        foreach (FirstShipQuestData.Material m in data.materials)
        {
            int delivered = FirstShipQuest.DeliveredOf(m.oreId);
            int carried = cargo != null ? Mathf.FloorToInt(cargo.Amount(m.oreId)) : 0;
            string name = ores != null ? OreCatalog.NameOf(m.oreId) : m.oreId;
            string carry = stage == FirstShipQuest.Stage.Minerals && delivered < m.amount && carried > 0 ? $" (화물칸 {carried})" : "";
            _detail.AddStat(name, $"{Mathf.Min(delivered, m.amount):N0} / {m.amount:N0}{carry}", (float)delivered / m.amount);
        }

        _detail.AddStat(data.alloyName, $"{FirstShipQuest.Alloys} / {data.alloyRequired}", (float)FirstShipQuest.Alloys / data.alloyRequired);
        _detail.AddStat(data.coreName, FirstShipQuest.HasCore ? "1 / 1" : "0 / 1", FirstShipQuest.HasCore ? 1f : 0f);
        _detail.AddStat("통신 기록 (선택)", FirstShipQuest.HasCommsLog ? "회수" : "-");

        switch (stage)
        {
            case FirstShipQuest.Stage.Offered:
            {
                bool can = FirstShipQuest.CanPurchase(out string reason);
                _detail.SetPrimary(can ? $"블루프린트 구매 · {data.blueprintPrice:N0} CR" : "크레딧 부족", can, () =>
                {
                    Accepted?.Invoke();
                    FirstShipQuest.Purchase();
                });
                _detail.SetSecondary("라스와 통신", true, () =>
                {
                    Accepted?.Invoke();
                    FirstShipQuest.PlayOffer(true);
                });
                _detail.SetMessage(_message ?? reason);
                break;
            }
            case FirstShipQuest.Stage.Minerals:
            {
                bool can = FirstShipQuest.CanDeliver(Zone, out string reason);
                _detail.SetPrimary("재료 납품", can, () =>
                {
                    int delivered = FirstShipQuest.DeliverMinerals(Zone);
                    if (FirstShipQuest.Current != FirstShipQuest.Stage.Minerals)
                    {
                        Accepted?.Invoke(); // 다음 퀘스트 안내 대사
                        return;
                    }

                    _message = $"광물 {delivered:N0}개를 납품했습니다.";
                    Rebuild(true);
                });
                _detail.SetMessage(_message ?? reason);
                break;
            }
            case FirstShipQuest.Stage.Alloy:
            case FirstShipQuest.Stage.Core:
            {
                bool running = MissionDirector.IsRunning;
                string label = stage == FirstShipQuest.Stage.Alloy ? "잔해 수색 출발" : "수송선 추적 출발";
                _detail.SetPrimary(running ? "다른 임무 진행 중" : label, !running, () =>
                {
                    if (FirstShipQuest.StartPartMission(out string error))
                    {
                        Accepted?.Invoke();
                        return;
                    }

                    _message = error;
                    Rebuild(true);
                });
                _detail.SetMessage(_message);
                break;
            }
            default:
            {
                bool can = FirstShipQuest.CanAssemble(Zone, out string reason);
                _detail.SetPrimary("최종 조립", can, () =>
                {
                    Accepted?.Invoke();
                    FirstShipQuest.Assemble(Zone);
                });
                _detail.SetMessage(_message ?? reason);
                break;
            }
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
        if (m is TransportMissionData) return "운송";
        if (!m.OnBoard) return "메인 퀘스트";
        return "기타";
    }

    private Sprite IconOf(MissionData m)
    {
        if (m is EscortMissionData) return _escortIcon;
        if (m is MiningMissionData) return _miningIcon;
        if (m is TransportMissionData && _transportIcon != null) return _transportIcon;
        return _sweepIcon;
    }
}
