using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class AlwaysStartMainMenu
{
    static AlwaysStartMainMenu()
    {
        EditorApplication.delayCall += SetStartScene;
    }

    private static void SetStartScene()
    {
        EditorSceneManager.playModeStartScene =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(
                "Assets/Scenes/MainMenu.unity");
    }
}