using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정거장 상점 창의 모양(SCI-FI UI Pack Popup005_Cyan 기반). 무엇을 보여줄지는 StationShop이 정하고, 이 컴포넌트는 참조만 들고 있다.
/// 위쪽은 함선 수리(선체 게이지 + 수리 버튼), 아래쪽은 무기 칸 목록, 맨 아래는 장착 위치 안내.
/// 프리팹: Assets/Resources/UI/StationShop.prefab
/// </summary>
public class StationShopView : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _title;

    [Header("함선 수리")]
    [SerializeField] private Image _hullFill;
    [SerializeField] private TMP_Text _hullText;
    [SerializeField] private Button _repairButton;
    [SerializeField] private TMP_Text _repairLabel;

    [Header("무기")]
    [SerializeField] private Transform _grid;
    [SerializeField] private ShopItemView _itemTemplate;
    [SerializeField] private GameObject _hint;

    public GameObject Root => _root;
    public TMP_Text Title => _title;
    public Image HullFill => _hullFill;
    public TMP_Text HullText => _hullText;
    public Button RepairButton => _repairButton;
    public TMP_Text RepairLabel => _repairLabel;
    public Transform Grid => _grid;
    public ShopItemView ItemTemplate => _itemTemplate;
    public GameObject Hint => _hint;

    private void Awake()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);
    }
}
