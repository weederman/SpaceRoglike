using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 고정(HUD) 플레이어 체력바. EnemyHealthBar와 같은 규칙이다:
/// 막대 전체 폭은 선체 최대 체력 기준이고, 왼쪽부터 hull_hp/hull_max만큼 초록,
/// 그 위 오른쪽 끝부터 shield_hp/hull_max만큼 흰색(쉴드)을 덮는다. 배경(빨강)은 잃은 선체 체력.
/// 쉴드가 다 닳으면 흰색이 사라지고 이후 선체가 깎이며 초록이 줄어든다.
/// </summary>
public class PlayerHealthBar : MonoBehaviour
{
    [SerializeField] private ShieldHealth _shield;
    [SerializeField] private HullHealth _hull;
    [SerializeField] private Image _greenFill;
    [SerializeField] private Image _whiteFill;

    private static Sprite _fillSprite;

    private void Awake()
    {
        // Image.Type.Filled는 sprite가 없으면 fillAmount를 무시한다(EnemyHealthBar와 같은 이유).
        if (_fillSprite == null)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _fillSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        if (_greenFill.sprite == null)
            _greenFill.sprite = _fillSprite;

        if (_whiteFill.sprite == null)
            _whiteFill.sprite = _fillSprite;
    }

    private void OnEnable()
    {
        _shield.OnHpChanged += HandleChanged;
        _hull.OnHpChanged += HandleChanged;
    }

    private void Start()
    {
        // ShieldHealth/HullHealth의 Awake가 끝난 뒤 초기 체력을 읽어야 한다.
        Refresh();
    }

    private void OnDisable()
    {
        _shield.OnHpChanged -= HandleChanged;
        _hull.OnHpChanged -= HandleChanged;
    }

    private void HandleChanged(float current, float max)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_hull.MaxHp <= 0f)
            return;

        _greenFill.fillAmount = Mathf.Clamp01(_hull.CurrentHp / _hull.MaxHp);
        _whiteFill.fillAmount = Mathf.Clamp01(_shield.CurrentHp / _hull.MaxHp);
    }
}
