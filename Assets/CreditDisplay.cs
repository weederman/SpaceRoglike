using UnityEngine;
using UnityEngine.UI;

/// <summary>화면에 현재 크레딧을 표시한다.</summary>
[RequireComponent(typeof(Text))]
public class CreditDisplay : MonoBehaviour
{
    [SerializeField] private string _format = "크레딧: {0}";

    private Text _text;
    private CreditWallet _wallet;

    private void Awake()
    {
        _text = GetComponent<Text>();
    }

    private void Start()
    {
        _wallet = CreditWallet.Instance;
        if (_wallet == null)
            return;

        _wallet.OnChanged += Refresh;
        Refresh(_wallet.Amount);
    }

    private void OnDestroy()
    {
        if (_wallet != null)
            _wallet.OnChanged -= Refresh;
    }

    private void Refresh(int amount)
    {
        _text.text = string.Format(_format, amount);
    }
}
