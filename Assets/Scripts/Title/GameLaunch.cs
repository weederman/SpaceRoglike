using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 타이틀 화면(TitleScene)에서 고른 시작 방식을 메인 씬(MainScene)에 넘긴다. 씬을 넘어가도 남도록 정적으로 둔다.
///  - NewGame : 프롤로그 전에 플레이어 이름을 받는다(NameEntryView). 이름을 정하면 기존 저장을 지우고 프롤로그가 이어진다.
///  - Continue: 저장(SaveSystem)에서 이어 한다.
///  - Direct  : 타이틀을 거치지 않고 메인 씬을 바로 Play한 경우(개발용). 예전처럼 개발 설정
///              (메뉴 SpaceRoglike/저장/에디터에서 이어 하기, dev.skipPrologue)을 따른다.
/// </summary>
public static class GameLaunch
{
    public const string TitleSceneName = "TitleScene";
    public const string MainSceneName = "MainScene";

    public enum Mode
    {
        Direct,
        NewGame,
        Continue
    }

    public static Mode Current { get; private set; }

    /// <summary>새 게임인데 아직 플레이어 이름을 정하지 않았는지 (프롤로그가 이름을 받은 뒤 시작한다)</summary>
    public static bool NeedsName => Current == Mode.NewGame && !_named;

    private static bool _named;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = Mode.Direct;
        _named = false;
    }

    public static void StartNewGame()
    {
        Current = Mode.NewGame;
        _named = false;
        PlayerProfile.Name = null;
        SceneManager.LoadScene(MainSceneName);
    }

    public static void ContinueGame()
    {
        Current = Mode.Continue;
        SceneManager.LoadScene(MainSceneName);
    }

    /// <summary>새 게임의 이름을 정했다. 이제 이전 판의 저장을 지운다 (이름 입력 전에 끄면 저장은 남는다).</summary>
    public static void ConfirmName(string name)
    {
        PlayerProfile.Name = name;
        _named = true;
        SaveSystem.Clear();
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
