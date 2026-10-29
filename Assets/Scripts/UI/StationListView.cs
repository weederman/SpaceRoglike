using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정거장 창 왼쪽의 스크롤 목록. 머리글(분류 이름)과 줄(StationListRow)을 견본에서 복제해 위에서부터 쌓는다.
/// 무엇을 넣을지는 창(HangarWindow 등)이 정한다.
/// </summary>
public class StationListView : MonoBehaviour
{
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private RectTransform _content;
    [SerializeField] private TMP_Text _headerTemplate;
    [SerializeField] private StationListRow _rowTemplate;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<StationListRow> _rows = new List<StationListRow>();

    private void Awake()
    {
        _headerTemplate.gameObject.SetActive(false);
        _rowTemplate.gameObject.SetActive(false);
    }

    /// <summary>목록을 비운다. keepScroll이면 스크롤 위치를 그대로 둔다 (같은 목록을 새로 그릴 때).</summary>
    public void Clear(bool keepScroll = false)
    {
        foreach (GameObject go in _spawned)
            Destroy(go);

        _spawned.Clear();
        _rows.Clear();

        if (!keepScroll && _scroll != null)
            _scroll.verticalNormalizedPosition = 1f;
    }

    public void AddHeader(string text)
    {
        TMP_Text header = Instantiate(_headerTemplate, _content);
        header.gameObject.SetActive(true);
        header.text = text;
        _spawned.Add(header.gameObject);
    }

    public StationListRow AddRow()
    {
        StationListRow row = Instantiate(_rowTemplate, _content);
        row.gameObject.SetActive(true);
        _spawned.Add(row.gameObject);
        _rows.Add(row);
        return row;
    }

    /// <summary>이 줄만 선택 표시한다 (null이면 모두 해제)</summary>
    public void Select(StationListRow selected)
    {
        foreach (StationListRow row in _rows)
            row.SetSelected(row == selected);
    }
}
