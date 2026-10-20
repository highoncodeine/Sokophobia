using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sokophobia.Grid;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;

    private bool isPaused;
    private bool isGameOver;
    private bool isPopupVisible;

    private void OnEnable()
    {
        GameEvents.OnPlayerDied += HandleGameOver;
        GameEvents.OnLevelWon += HandleGameOver;
        GameEvents.OnGameplayPopupChanged += HandlePopupChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDied -= HandleGameOver;
        GameEvents.OnLevelWon -= HandleGameOver;
        GameEvents.OnGameplayPopupChanged -= HandlePopupChanged;
    }

    private void HandlePopupChanged(bool visible)
    {
        isPopupVisible = visible;

        // Unlock cursor while popup is visible, lock it when closed
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = visible;
    }

    private void HandleGameOver()
    {
        isGameOver = true;
        isPaused = false;
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
            
        // Ensure cursor is unlocked on death/win screens
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Start()
    {
        CreateRuntimeHitboxes();
        if (isGameOver || isPopupVisible)
            return;

        Time.timeScale = 1f;
        SetPaused(false);
    }

    private void Update()
    {
        if (isGameOver || isPopupVisible)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetPaused(!isPaused);
            return;
        }
    }

    private void OnGUI()
    {
        if (isGameOver || isPopupVisible || !isPaused || Event.current.type != EventType.MouseDown || Event.current.button != 0)
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
        if (pauseMenu == null) return;

        CreateHitbox("ResumeHitboxRuntime", new Vector2(0f, 38f), ResumeGame);
        CreateHitbox("QuitHitboxRuntime", new Vector2(0f, -20f), QuitToMainMenu);
    }

    private void CreateHitbox(string objectName, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        Transform existing = pauseMenu.transform.Find(objectName);
        if (existing != null) return;

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
        if (isPopupVisible) return;

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void SetPaused(bool paused)
    {
        if (isGameOver || isPopupVisible) return;

        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (pauseMenu != null)
        {
            pauseMenu.SetActive(paused);
        }

        // LOCK/UNLOCK CURSOR
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}