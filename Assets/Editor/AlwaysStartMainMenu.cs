using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class AlwaysStartMainMenu
{
    static AlwaysStartMainMenu()
    {
        // Wait until the editor has finished importing assets after compilation.
        EditorApplication.delayCall += SetStartScene;
    }

    private static void SetStartScene()
    {
        EditorSceneManager.playModeStartScene =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(
                "Assets/Scenes/MainMenu.unity");
    }
}