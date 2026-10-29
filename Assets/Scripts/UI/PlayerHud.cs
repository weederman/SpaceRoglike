using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 항상 보이는 플레이어 HUD (SCI-FI UI Pack 기반): 왼쪽 위 크레딧, 오른쪽 위 선체·쉴드 게이지(ShipHealthBar).
/// 플레이어 선체가 파괴되면(HullHealth.OnBroken) 게임을 멈추고 패배 창을 띄우며, '다시 시작'을 누르면 씬을 다시 불러온다.
/// 정거장 안에서도 보인다(정거장 화면보다 위, 전투 HUD보다 아래). 프롤로그처럼 연출이 화면을 쓰는 동안은 Hidden으로 숨긴다.
/// 게임 씬(플레이어 함선이 있는 씬)을 불러올 때마다 자동으로 생긴다. 프리팹: Assets/Resources/UI/PlayerHud.prefab
/// </summary>
public class PlayerHud : MonoBehaviour
{
    private const string PrefabPath = "UI/PlayerHud";

    /// <summary>true면 크레딧·게이지를 숨긴다 (프롤로그 연출 등). 패배 창은 그대로 뜬다.</summary>
    public static bool Hidden { get; set; }

    [SerializeField] private CanvasGroup _hud;
    [SerializeField] private TMP_Text _credits;
    [SerializeField] private string _creditFormat = "{0:N0} CR";
    [SerializeField] private ShipHealthBar _healthBar;

    [Header("패배")]
    [SerializeField] private GameObject _defeatPanel;
    [SerializeField] private Button _restartButton;

    private CreditWallet _wallet;
    private HullHealth _playerHull;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Hidden = false;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Hidden = false;

        // 플레이어 함선이 있는 게임 씬에서만 (씬과 함께 사라진다)
        if (FindObjectOfType<SpaceshipController>() == null)
            return;

        var prefab = Resources.Load<PlayerHud>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[PlayerHud] Assets/Resources/{PrefabPath}.prefab을 찾지 못해 크레딧·체력 HUD 없이 시작합니다.");
            return;
        }

        Instantiate(prefab);
    }

    private void Awake()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        _defeatPanel.SetActive(false);
        _restartButton.onClick.AddListener(Restart);

        var ship = FindObjectOfType<SpaceshipController>();
        if (ship != null)
        {
            _playerHull = ship.GetComponentInChildren<HullHealth>();
            _healthBar.Bind(_playerHull, ship.GetComponentInChildren<ShieldHealth>(true));
            if (_playerHull != null)
                _playerHull.OnBroken += ShowDefeat;
        }
    }

    private void Start()
    {
        _wallet = CreditWallet.Instance;
        if (_wallet != null)
        {
            _wallet.OnChanged += RefreshCredits;
            RefreshCredits(_wallet.Amount);
        }
    }

    private void OnDestroy()
    {
        if (_wallet != null)
            _wallet.OnChanged -= RefreshCredits;
        if (_playerHull != null)
            _playerHull.OnBroken -= ShowDefeat;
    }

    private void LateUpdate()
    {
        float alpha = Hidden ? 0f : 1f;
        if (!Mathf.Approximately(_hud.alpha, alpha))
            _hud.alpha = alpha;
    }

    private void RefreshCredits(int amount)
    {
        _credits.text = string.Format(_creditFormat, amount);
    }

    private void ShowDefeat()
    {
        _defeatPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
