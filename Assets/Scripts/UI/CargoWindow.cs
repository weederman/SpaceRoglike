using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 중 화물칸(인벤토리) 창. I 키로 열고 닫는다 (SCI-FI UI Pack 창 기반, 정거장 창과 같은 목록 StationListView).
/// 화물칸(CargoHold)에 실린 광석을 종류별로 양·시장가·합계와 함께 보여주고, 진행 중인 채굴 임무가 있으면 납품할 수 있는 양을 알려 준다.
/// 보기 전용이라 조작을 막지 않는다(열어 둔 채로 날고 쏠 수 있다). 정거장 안(도킹 중)이나 대사·지도 등으로 조작이 잠긴 동안은 닫힌다.
/// CombatHud 프리팹(Assets/Resources/UI/CombatHud.prefab)에 붙어 함께 생성된다.
/// </summary>
public class CargoWindow : MonoBehaviour
{
    [SerializeField] private KeyCode _toggleKey = KeyCode.I;
    [SerializeField] private GameObject _window;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private Slider _bar;
    [SerializeField] private TMP_Text _capacity;
    [SerializeField] private StationListView _list;
    [SerializeField] private TMP_Text _footer;

    private CargoHold _cargo;
    private bool _dirty;

    public bool IsOpen => _window.activeSelf;

    private void Awake()
    {
        foreach (TMP_Text text in _window.GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);
        _window.SetActive(false);
    }

    private void Start()
    {
        _cargo = CargoHold.Instance;
        if (_cargo != null)
            _cargo.OnChanged += MarkDirty;
    }

    private void OnDestroy()
    {
        if (_cargo != null)
            _cargo.OnChanged -= MarkDirty;
    }

    private void MarkDirty()
    {
        _dirty = true;
    }

    private void Update()
    {
        if (StationDocking.IsDocked || GameInputLock.IsLocked)
        {
            if (IsOpen)
                _window.SetActive(false);
            return;
        }

        if (Input.GetKeyDown(_toggleKey))
        {
            _window.SetActive(!IsOpen);
            _dirty = true;
        }

        // 채굴 중에는 매 프레임 조금씩 늘어나므로 열려 있을 때만, 바뀌었을 때만 다시 그린다
        if (IsOpen && _dirty)
            Rebuild();
    }

    private void Rebuild()
    {
        _dirty = false;
        _title.text = $"화물칸   <size=60%><color=#8FD8E8>{_toggleKey} 닫기</color></size>";
        _list.Clear(true);

        if (_cargo == null)
        {
            _capacity.text = "";
            _footer.text = "화물칸이 없습니다.";
            return;
        }

        _bar.value = _cargo.Ratio;
        _capacity.text = $"{Mathf.FloorToInt(_cargo.Ore)} / {Mathf.FloorToInt(_cargo.Capacity)}";

        OreCatalog catalog = OreCatalog.Load();
        var contents = _cargo.Contents.Where(p => p.Value >= 1f).ToList();
        _list.AddHeader(contents.Count > 0 ? "광석" : "광석 · 비어 있음");

        int total = 0;
        foreach (var pair in contents)
        {
            OreCatalog.Ore ore = catalog != null ? catalog.Find(pair.Key) : null;
            int amount = Mathf.FloorToInt(pair.Value);
            int price = ore != null ? ore.price : 0;
            total += amount * price;

            StationListRow row = _list.AddRow();
            row.Bind(ore != null ? ore.icon : null, ore != null ? ore.displayName : pair.Key, $"시장가 {price:N0} CR / 개",
                $"{amount:N0}개 · {amount * price:N0} CR", () => { });
        }

        string footer = contents.Count > 0
            ? $"시장가 합계 {total:N0} CR · 정거장 상점에서 팔 수 있습니다."
            : "광맥 소행성을 골라(Tab·클릭) 가까이에서 F를 누르면 채굴합니다.";

        if (MissionDirector.Current is MiningMissionDirector mining && mining.Data != null)
        {
            int carried = Mathf.FloorToInt(mining.CarriedForMission());
            footer += $"\n<color=#FFD166>임무 납품용 {mining.Data.OreLabel} {carried:N0}개 · 납품 {mining.Delivered:N0} / {mining.Data.amount:N0}</color>";
        }

        _footer.text = footer;
    }
}
