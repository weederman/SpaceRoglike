using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>상세 정보 '세부정보' 탭의 한 줄: 이름, 값, (있으면) 게이지. ItemDetailView가 견본을 복제해 쓴다.</summary>
public class DetailStatRow : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private TMP_Text _value;
    [SerializeField] private GameObject _bar;
    [SerializeField] private Image _barFill;

    /// <summary>ratio가 0 이상이면 게이지를 그 비율만큼 채워 보여준다.</summary>
    public void Set(string label, string value, float ratio)
    {
        _label.text = label;
        _value.text = value;
        _bar.SetActive(ratio >= 0f);
        _barFill.fillAmount = Mathf.Clamp01(ratio);
    }
}
