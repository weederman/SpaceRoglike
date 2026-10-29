using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 함선 체력바의 공통 로직. 막대 전체 폭은 선체(Hull) 최대 체력 기준이다.
/// 왼쪽부터 hull_hp/hull_max만큼 초록으로 채우고, 그 위 오른쪽 끝부터 shield_hp/hull_max만큼 흰색(쉴드)을 덮는다.
/// 배경(빨강)은 잃은 선체 체력이다. 쉴드가 깎일수록 흰색이 줄며 아래의 초록이 드러나고,
/// 쉴드가 0이 되면 초록만 보이다가 이후 선체가 깎이면 초록이 줄고 빨강이 늘어난다.
/// 대상은 부모 함선에서 찾거나(적 머리 위 EnemyHealthBar), Bind로 정한다(화면 HUD의 플레이어 게이지, PlayerHud).
/// _label이 있으면 "선체 x / y · 쉴드 z"를 함께 쓴다.
/// </summary>
public class ShipHealthBar : MonoBehaviour
{
    [SerializeField] protected ShieldHealth _shield;
    [SerializeField] protected HullHealth _hull;
    [SerializeField] protected Image _greenFill;
    [SerializeField] protected Image _whiteFill;
    [Tooltip("숫자 표시 (없어도 된다)")]
    [SerializeField] private TMP_Text _label;

    private static Sprite _fillSprite;

    protected virtual void Awake()
    {
        if (_shield == null)
            _shield = GetComponentInParent<ShieldHealth>();

        if (_hull == null)
            _hull = GetComponentInParent<HullHealth>();

        // Image.Type.Filled는 sprite가 없으면 fillAmount를 무시하고 항상 꽉 찬 사각형으로 그려지는
        // 유니티의 함정이 있어서, 비어 있으면 흰 1x1 스프라이트를 직접 만들어 채워준다.
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

    /// <summary>보여 줄 함선을 바꾼다 (쉴드는 없어도 된다)</summary>
    public void Bind(HullHealth hull, ShieldHealth shield)
    {
        Unsubscribe();
        _hull = hull;
        _shield = shield;
        if (isActiveAndEnabled)
            Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        // ShieldHealth/HullHealth의 Awake가 끝난 뒤(초기 체력 세팅 이후)에 첫 값을 읽어야 하므로
        // OnEnable이 아니라 Start에서 처음 갱신한다.
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    // 쉴드가 없는 함선(호위 화물선 등)은 선체만 표시한다
    private void Subscribe()
    {
        if (_shield != null)
            _shield.OnHpChanged += HandleChanged;
        if (_hull != null)
            _hull.OnHpChanged += HandleChanged;
    }

    private void Unsubscribe()
    {
        if (_shield != null)
            _shield.OnHpChanged -= HandleChanged;
        if (_hull != null)
            _hull.OnHpChanged -= HandleChanged;
    }

    private void HandleChanged(float current, float max)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_hull == null || _hull.MaxHp <= 0f)
            return;

        bool hasShield = _shield != null && _shield.Installed;
        _greenFill.fillAmount = Mathf.Clamp01(_hull.CurrentHp / _hull.MaxHp);
        _whiteFill.fillAmount = hasShield ? Mathf.Clamp01(_shield.CurrentHp / _hull.MaxHp) : 0f;

        if (_label != null)
            _label.text = hasShield
                ? $"선체 {Mathf.CeilToInt(_hull.CurrentHp)} / {Mathf.CeilToInt(_hull.MaxHp)}   쉴드 {Mathf.CeilToInt(_shield.CurrentHp)}"
                : $"선체 {Mathf.CeilToInt(_hull.CurrentHp)} / {Mathf.CeilToInt(_hull.MaxHp)}";
    }
}
