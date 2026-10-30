using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 새 게임의 플레이어 이름 입력 창. 프롤로그(PrologueDirector)가 시작하기 전에 검은 화면 위에 띄운다(Ask).
/// 앞뒤 공백을 뺀 PlayerProfile.MinLength~MaxLength 글자일 때만 확인 버튼이 켜지고, Enter로도 확인한다.
/// 확인하면 GameLaunch.ConfirmName으로 이름을 정한다(이때 이전 판의 저장을 지운다). 창이 떠 있는 동안 조작을 잠근다.
/// 모양은 Assets/Resources/UI/NameEntry.prefab (SCI-FI UI Pack Popup001 · InputField01 · LargeButton01 기반).
/// </summary>
public class NameEntryView : MonoBehaviour
{
    private const string PrefabPath = "UI/NameEntry";

    [SerializeField] private TMP_InputField _input;
    [SerializeField] private Button _confirm;
    [SerializeField] private TMP_Text _hint;
    [Tooltip("{0} 최소 글자 수, {1} 최대 글자 수")]
    [SerializeField] private string _hintFormat = "{0}~{1}글자로 입력하세요";

    private bool _done;

    /// <summary>이름 입력 창을 띄우고 확인할 때까지 기다린다</summary>
    public static IEnumerator Ask()
    {
        var prefab = Resources.Load<NameEntryView>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[NameEntry] Assets/Resources/{PrefabPath}.prefab을 찾지 못해 이름 입력 없이 시작합니다.");
            yield break;
        }

        NameEntryView view = Instantiate(prefab);
        GameInputLock.Lock(view);
        while (!view._done)
            yield return null;

        GameInputLock.Unlock(view);
        Destroy(view.gameObject);
    }

    private void Awake()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        _input.text = "";
        _input.characterLimit = PlayerProfile.MaxLength;
        _input.onValidateInput = (text, index, c) => c == '<' || c == '>' ? '\0' : c;
        _input.onValueChanged.AddListener(_ => Refresh());
        _input.onSubmit.AddListener(_ => TryConfirm());
        _confirm.onClick.AddListener(TryConfirm);

        if (_hint != null)
            _hint.text = string.Format(_hintFormat, PlayerProfile.MinLength, PlayerProfile.MaxLength);
    }

    private void Start()
    {
        Refresh();
        _input.Select();
        _input.ActivateInputField();
    }

    private void Refresh()
    {
        bool valid = PlayerProfile.IsValid(_input.text);
        _confirm.interactable = valid;

        // UI 팩 버튼은 꺼진 모습(스프라이트)이 따로 없으므로 꺼지면 흐리게 한다
        CanvasGroup group = _confirm.GetComponent<CanvasGroup>();
        if (group == null)
            group = _confirm.gameObject.AddComponent<CanvasGroup>();
        group.alpha = valid ? 1f : 0.4f;
    }

    private void TryConfirm()
    {
        if (_done || !PlayerProfile.IsValid(_input.text))
        {
            // Enter로 제출하면 입력란이 꺼지므로 다시 켠다
            _input.ActivateInputField();
            return;
        }

        GameLaunch.ConfirmName(PlayerProfile.Clean(_input.text));
        _done = true;
    }
}
