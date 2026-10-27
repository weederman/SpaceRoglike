using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 선택지 목록의 한 칸. SCI-FI UI Pack의 Btn04Cyan을 바탕으로 한 프리팹(Assets/Prefabs/UI/StoryChoiceButton.prefab)에 붙어 있다.
/// 강조(키보드로 고른 칸 / 마우스를 올린 칸)는 이 컴포넌트가 직접 스프라이트를 바꿔서 보여준다.
/// Button의 자동 전환을 쓰지 않는 이유: 키보드 선택과 마우스 호버가 서로 다른 칸을 가리킬 때 둘이 엇갈리지 않게 하려고.
/// 어느 칸이 선택되는지의 판단은 StoryPlayer가 하고, 여기서는 그리기와 클릭/호버 알림만 한다.
/// </summary>
public class StoryChoiceItem : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Image _marker;

    private Sprite _normalSprite;
    private Sprite _highlightSprite;

    public event Action<int> Clicked;
    public event Action<int> Hovered;

    public int Index { get; private set; }

    private void Awake()
    {
        // 팩이 Button에 지정해 둔 "평소/강조" 스프라이트를 그대로 가져와 쓴다
        _normalSprite = _background.sprite;
        _highlightSprite = _button.spriteState.selectedSprite != null ? _button.spriteState.selectedSprite : _normalSprite;

        _button.transition = Selectable.Transition.None;
        _button.navigation = new Navigation { mode = Navigation.Mode.None };
        _button.onClick.AddListener(() => Clicked?.Invoke(Index));

        GameFonts.ApplyKorean(_label);
    }

    public void Bind(int index, string text)
    {
        Index = index;
        _label.text = text;
        SetHighlighted(false);
    }

    public void SetHighlighted(bool highlighted)
    {
        _background.sprite = highlighted ? _highlightSprite : _normalSprite;
        _marker.enabled = highlighted;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Hovered?.Invoke(Index);
    }
}
