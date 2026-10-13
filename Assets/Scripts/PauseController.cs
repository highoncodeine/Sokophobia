using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;

    private bool isPaused;

    private void Start()
    {
        Time.timeScale = 1f;
        CreateRuntimeHitboxes();
        SetPaused(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetPaused(!isPaused);
            return;
        }

    }

    private void OnGUI()
    {
        if (!isPaused || Event.current.type != EventType.MouseDown || Event.current.button != 0)
        {
            return;
        }

        float normalizedX = Event.current.mousePosition.x / Screen.width;
        float normalizedTopY = Event.current.mousePosition.y / Screen.height;

        if (normalizedX >= 0.25f && normalizedX <= 0.75f)
        {
            if (normalizedTopY >= 0.37f && normalizedTopY <= 0.47f)
            {
                ResumeGame();
                Event.current.Use();
            }
            else if (normalizedTopY >= 0.48f && normalizedTopY <= 0.60f)
            {
                QuitToMainMenu();
                Event.current.Use();
            }
        }
    }

    private void CreateRuntimeHitboxes()
    {
        if (pauseMenu == null)
        {
            return;
        }

        CreateHitbox("ResumeHitboxRuntime", new Vector2(0f, 38f), ResumeGame);
        CreateHitbox("QuitHitboxRuntime", new Vector2(0f, -20f), QuitToMainMenu);
    }

    private void CreateHitbox(string objectName, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        Transform existing = pauseMenu.transform.Find(objectName);
        if (existing != null)
        {
            return;
        }

        GameObject hitboxObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        hitboxObject.transform.SetParent(pauseMenu.transform, false);

        RectTransform hitboxRect = hitboxObject.GetComponent<RectTransform>();
        hitboxRect.anchorMin = new Vector2(0.5f, 0.5f);
        hitboxRect.anchorMax = new Vector2(0.5f, 0.5f);
        hitboxRect.anchoredPosition = anchoredPosition;
        hitboxRect.sizeDelta = new Vector2(340f, 90f);

        Image hitboxImage = hitboxObject.GetComponent<Image>();
        hitboxImage.color = Color.clear;
        hitboxImage.raycastTarget = true;

        Button hitboxButton = hitboxObject.GetComponent<Button>();
        hitboxButton.targetGraphic = hitboxImage;
        hitboxButton.onClick.AddListener(onClick);
    }

    public void ResumeGame()
    {
        SetPaused(false);
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (pauseMenu != null)
        {
            pauseMenu.SetActive(paused);
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
