using UnityEditor;

[InitializeOnLoad]
internal partial class SceneSwitcherMenu : SceneSwitcher.Editor.SceneSwitcherWindow
{
    [MenuItem("File/", false, SeparatorMenuItemPriority)]
    private static void AddMenuSeparator()
    {
    }

    [MenuItem("File/Open Recent Scene... %#o", false, SeparatorMenuItemPriority + 1)]
    protected static void OpenRecentScene()
    {
        var window = EditorWindow.GetWindow<SceneSwitcherMenu>(true, "Open Recent Scene");
        window.autoRepaintOnSceneChange = true;
    }
}
