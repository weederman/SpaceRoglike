using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정거장 창 오른쪽의 상세 정보 (아이콘, 이름, 가격, 한 줄 정보, '설명'/'세부정보' 탭, 아래쪽 버튼 두 개와 안내 문장).
/// 세부정보는 "이름 · 값 · 게이지" 줄을 견본에서 복제해 쌓는다. 무엇을 보여줄지는 창이 정한다.
/// </summary>
public class ItemDetailView : MonoBehaviour
{
    [Header("머리")]
    [SerializeField] private GameObject _content;
    [SerializeField] private TMP_Text _empty;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private TMP_Text _price;
    [SerializeField] private TMP_Text _info;

    [Header("탭")]
    [SerializeField] private Button _descriptionTab;
    [SerializeField] private Button _statsTab;
    [SerializeField] private Sprite _tabOn;
    [SerializeField] private Sprite _tabOff;
    [SerializeField] private TMP_Text _description;
    [SerializeField] private RectTransform _stats;
    [SerializeField] private DetailStatRow _statTemplate;

    [Header("버튼")]
    [SerializeField] private Button _primary;
    [SerializeField] private TMP_Text _primaryLabel;
    [SerializeField] private Button _secondary;
    [SerializeField] private TMP_Text _secondaryLabel;
    [SerializeField] private TMP_Text _message;

    private readonly List<DetailStatRow> _statRows = new List<DetailStatRow>();
    private Action _primaryAction;
    private Action _secondaryAction;
    private bool _showStats;

    private void Awake()
    {
        _statTemplate.gameObject.SetActive(false);
        _descriptionTab.onClick.AddListener(() => ShowStats(false));
        _statsTab.onClick.AddListener(() => ShowStats(true));
        _primary.onClick.AddListener(() => _primaryAction?.Invoke());
        _secondary.onClick.AddListener(() => _secondaryAction?.Invoke());
    }

    /// <summary>아무것도 고르지 않았을 때: 안내 문장만 보여준다</summary>
    public void ShowEmpty(string text)
    {
        _content.SetActive(false);
        _empty.gameObject.SetActive(true);
        _empty.text = text;
    }

    /// <summary>새 항목을 보여주기 시작한다. 설명·세부정보·버튼은 비워진 상태로 시작한다.</summary>
    public void Show(Sprite icon, string name, string price, string info)
    {
        _empty.gameObject.SetActive(false);
        _content.SetActive(true);
        _icon.sprite = icon;
        _icon.enabled = icon != null;
        _name.text = name;
        _price.text = price ?? "";
        _info.text = info ?? "";
        _description.text = "";
        ClearStats();
        SetPrimary(null, false, null);
        SetSecondary(null, false, null);
        SetMessage(null);
        ShowStats(_showStats);
    }

    public void SetDescription(string text)
    {
        _description.text = text ?? "";
    }

    public void ClearStats()
    {
        foreach (DetailStatRow row in _statRows)
            Destroy(row.gameObject);
        _statRows.Clear();
    }

    /// <summary>세부정보 한 줄. ratio가 0 이상이면 게이지를 보여준다.</summary>
    public void AddStat(string label, string value, float ratio = -1f)
    {
        DetailStatRow row = Instantiate(_statTemplate, _stats);
        row.gameObject.SetActive(true);
        row.Set(label, value, ratio);
        _statRows.Add(row);
    }

    public void ShowStats(bool stats)
    {
        _showStats = stats;
        _description.gameObject.SetActive(!stats);
        _stats.gameObject.SetActive(stats);
        ((Image)_descriptionTab.targetGraphic).sprite = stats ? _tabOff : _tabOn;
        ((Image)_statsTab.targetGraphic).sprite = stats ? _tabOn : _tabOff;
    }

    /// <summary>아래쪽 첫 버튼. label이 null이면 숨긴다.</summary>
    public void SetPrimary(string label, bool interactable, Action action)
    {
        SetButton(_primary, _primaryLabel, label, interactable);
        _primaryAction = action;
    }

    /// <summary>아래쪽 둘째 버튼. label이 null이면 숨긴다.</summary>
    public void SetSecondary(string label, bool interactable, Action action)
    {
        SetButton(_secondary, _secondaryLabel, label, interactable);
        _secondaryAction = action;
    }

    public void SetMessage(string text)
    {
        _message.text = text ?? "";
    }

    private static void SetButton(Button button, TMP_Text text, string label, bool interactable)
    {
        button.gameObject.SetActive(label != null);
        if (label == null)
            return;

        text.text = label;
        button.interactable = interactable;
    }
}
