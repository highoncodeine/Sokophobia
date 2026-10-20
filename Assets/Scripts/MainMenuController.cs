using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private AudioSource bgmSource;
    private void Start()
    {
        if (bgmSource != null && !bgmSource.isPlaying)
        {
            bgmSource.Play();
        }
    }
    
    public void PlayGame()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
        SceneManager.LoadScene("SampleScene");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
