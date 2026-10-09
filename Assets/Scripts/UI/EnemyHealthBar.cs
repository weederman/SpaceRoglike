using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 적 함선 위에 떠 있는 체력바. 막대 전체 폭은 선체(Hull) 최대 체력 기준이며,
/// 왼쪽부터 hull_hp/hull_max만큼 초록으로 채우고, 그 위 오른쪽 끝부터
/// shield_hp/hull_max만큼 흰색을 덮어씌운다. 배경(빨강)은 잃은 선체 체력을 나타낸다.
/// 쉴드가 가득 찼을 때는 흰색이 초록의 오른쪽 일부를 가려 보이고,
/// 쉴드가 깎일수록 흰색이 줄어들며 그 아래 초록이 드러난다.
/// 쉴드가 0이 되면 완전히 초록만 보이고, 이후 선체가 깎이면 초록이 줄고 빨강이 늘어난다.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private ShieldHealth _shield;
    [SerializeField] private HullHealth _hull;

    [SerializeField] private Image _greenFill;
    [SerializeField] private Image _whiteFill;

    [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 15f, 0f);

    private Transform _followTarget;
    private Camera _cam;

    private static Sprite _fillSprite;

    private void Awake()
    {
        if (_shield == null)
            _shield = GetComponentInParent<ShieldHealth>();

        if (_hull == null)
            _hull = GetComponentInParent<HullHealth>();

        _followTarget = transform.parent;
        _cam = Camera.main;

        // Image.Type.Filled는 sprite가 없으면 fillAmount를 무시하고
        // 항상 꽉 찬 사각형으로 그려지는 유니티의 알려진 함정이 있다.
        // 스프라이트가 비어 있으면 흰 1x1 스프라이트를 직접 만들어 채워준다.
        if (_fillSprite == null)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _fillSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        if (_greenFill != null && _greenFill.sprite == null)
            _greenFill.sprite = _fillSprite;

        if (_whiteFill != null && _whiteFill.sprite == null)
            _whiteFill.sprite = _fillSprite;
    }

    private void OnEnable()
    {
        if (_shield != null)
            _shield.OnHpChanged += HandleShieldChanged;

        if (_hull != null)
            _hull.OnHpChanged += HandleHullChanged;
    }

    private void Start()
    {
        // 모든 오브젝트의 Awake()가 끝난 뒤(ShieldHealth/HullHealth의 초기 체력 세팅 이후)
        // 값을 읽어야 하므로 OnEnable이 아니라 Start에서 첫 갱신을 한다.
        Refresh();
    }

    private void OnDisable()
    {
        if (_shield != null)
            _shield.OnHpChanged -= HandleShieldChanged;

        if (_hull != null)
            _hull.OnHpChanged -= HandleHullChanged;
    }

    private void LateUpdate()
    {
        if (_followTarget != null)
            transform.position = _followTarget.position + _worldOffset;

        if (_cam == null)
            _cam = Camera.main;

        if (_cam != null)
            transform.rotation = _cam.transform.rotation;
    }

    private void HandleShieldChanged(float current, float max)
    {
        Refresh();
    }

    private void HandleHullChanged(float current, float max)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_hull == null || _hull.MaxHp <= 0f)
            return;

        if (_greenFill != null)
            _greenFill.fillAmount = Mathf.Clamp01(_hull.CurrentHp / _hull.MaxHp);

        if (_whiteFill != null)
            _whiteFill.fillAmount = _shield != null ? Mathf.Clamp01(_shield.CurrentHp / _hull.MaxHp) : 0f;
    }
}
