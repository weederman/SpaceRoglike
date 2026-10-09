using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 플레이어 선체가 파괴되면(HullHealth.OnBroken) 게임을 멈추고 화면 중앙에
/// 패배 패널과 재시작 버튼을 보여준다. 버튼을 누르면 현재 씬을 다시 불러온다.
/// </summary>
public class DefeatController : MonoBehaviour
{
    [SerializeField] private HullHealth _playerHull;
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _restartButton;

    private void Awake()
    {
        _panel.SetActive(false);
        _restartButton.onClick.AddListener(Restart);
        _playerHull.OnBroken += ShowDefeat;
    }

    private void OnDestroy()
    {
        if (_playerHull != null)
            _playerHull.OnBroken -= ShowDefeat;
    }

    private void ShowDefeat()
    {
        _panel.SetActive(true);
        Time.timeScale = 0f;
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
