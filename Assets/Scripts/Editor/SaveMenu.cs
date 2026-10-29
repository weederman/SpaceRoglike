using UnityEditor;

/// <summary>
/// 에디터용 저장 메뉴.
///  - 에디터에서 이어 하기: 켜면 Play 때 저장을 불러와 이어 한다. 꺼 두면(기본) 저장이 있어도 프롤로그부터 시작한다.
///  - 저장 지우기: 저장 파일을 지운다.
/// </summary>
public static class SaveMenu
{
    private const string ContinueMenu = "SpaceRoglike/저장/에디터에서 이어 하기";

    [MenuItem(ContinueMenu)]
    private static void ToggleContinue()
    {
        bool on = !EditorPrefs.GetBool(SaveSystem.ContinueInEditorPrefKey, false);
        EditorPrefs.SetBool(SaveSystem.ContinueInEditorPrefKey, on);
    }

    [MenuItem(ContinueMenu, true)]
    private static bool ToggleContinueValidate()
    {
        Menu.SetChecked(ContinueMenu, EditorPrefs.GetBool(SaveSystem.ContinueInEditorPrefKey, false));
        return true;
    }

    [MenuItem("SpaceRoglike/저장/저장 지우기")]
    private static void ClearSave()
    {
        if (SaveSystem.HasSave && EditorUtility.DisplayDialog("저장 지우기", $"저장 파일을 지웁니다.\n{SaveSystem.FilePath}", "지우기", "취소"))
            SaveSystem.Clear();
    }
}
