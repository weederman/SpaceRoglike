using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정거장 내부에서 사이드바 버튼으로 여는 가운데 창 하나 (SCI-FI UI Pack Window010 기반). 닫기(X) 버튼과 제목을 갖는다.
/// 내용은 하위 클래스(HangarWindow, MissionBoardWindow, StationInfoWindow)가 채우고, 스페이스 라운지처럼 내용이 고정된 창은 이것만 쓴다.
/// 여닫기는 StationInteriorView가 정한다(한 번에 하나, Esc로 닫기).
/// </summary>
public class StationWindow : MonoBehaviour
{
    [SerializeField] private TMP_Text _title;
    [SerializeField] private Button _closeButton;

    /// <summary>닫기 버튼을 눌렀을 때</summary>
    public event Action<StationWindow> CloseRequested;

    public bool IsOpen => gameObject.activeSelf;
    protected StationZone Zone { get; private set; }

    protected virtual void Awake()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(() => CloseRequested?.Invoke(this));
    }

    public void Open(StationZone zone)
    {
        Zone = zone;
        gameObject.SetActive(true);
        OnOpened();
    }

    public void Close()
    {
        if (!gameObject.activeSelf)
            return;

        gameObject.SetActive(false);
        OnClosed();
    }

    protected void SetTitle(string title)
    {
        if (_title != null)
            _title.text = title;
    }

    protected virtual void OnOpened() { }
    protected virtual void OnClosed() { }
}
