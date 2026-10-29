using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정거장 내부(도킹 중) 화면 UI. 위쪽 정거장 이름과 아래쪽 "출항" 버튼(SCI-FI UI Pack 기반), 그리고 도킹/출항 때 쓰는 화면 전체 페이드.
/// 동작은 StationDocking이 정한다. 프리팹: Assets/Resources/UI/StationInterior.prefab
/// </summary>
public class StationInteriorView : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private Button _undockButton;
    [SerializeField] private Image _fade;

    public TMP_Text Title => _title;
    public Button UndockButton => _undockButton;

    private void Awake()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        SetFade(0f);
    }

    public void SetVisible(bool visible)
    {
        _root.SetActive(visible);
    }

    /// <summary>0 = 투명, 1 = 화면 전체가 검게</summary>
    public void SetFade(float alpha)
    {
        Color c = _fade.color;
        c.a = alpha;
        _fade.color = c;
        _fade.gameObject.SetActive(alpha > 0.001f);
    }
}
