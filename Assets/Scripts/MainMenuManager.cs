using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// Manages the main menu, level selection, cutscenes, and transitions
/// Handles all UI navigation and video playback
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Level Locking Settings")]
    [Tooltip("If TRUE, players must complete levels in order. If FALSE, all levels are unlocked from start.")]
    public bool enableLevelLocking = false; // Set to false to unlock all levels!
    
    [Header("Main Menu UI")]
    public GameObject mainMenuPanel;
    public GameObject levelSelectPanel;
    public GameObject controlsPanel;
    public Button playButton;
    public Button controlsButton;
    public Button quitButton;
    
    [Header("Background Settings")]
    public RawImage backgroundImage;
    public Texture2D menuBackgroundTexture;
    
    [Header("Level Selection")]
    public Button[] levelButtons = new Button[5]; // 5 levels
    public TextMeshProUGUI[] levelDescriptions = new TextMeshProUGUI[5];
    public TextMeshProUGUI[] levelScores = new TextMeshProUGUI[5]; // Display scores for each level
    private int unlockedLevels = 1; // Start with level 1 unlocked
    private int[] levelHighScores = new int[5]; // Track high scores for each level
    
    [Header("Cutscene Settings")]
    public VideoPlayer videoPlayer;
    public RawImage videoDisplay;
    public VideoClip levelStartCutscene;
    public VideoClip levelCompleteCutscene;
    
    [Header("Level Configurations")]
    public LevelConfig[] levelConfigs = new LevelConfig[5];
    
    [Header("Alien Settings")]
    public RawImage alienImage;
    public Sprite alienSprite;
    public float alienMinInterval = 10f;
    public float alienMaxInterval = 30f;
    public float alienAnimationDuration = 3f;
    
    [Header("Sound Effects")]
    [Tooltip("Sound effect that plays when player wins a level")]
    public AudioClip victorySound;
    [Tooltip("Volume for victory sound (0-1)")]
    public float victorySoundVolume = 1f;
    private AudioSource audioSource;
    
    private Canvas mainCanvas;
    private int selectedLevel = -1;
    private bool isPlayingCutscene = false;
    private Coroutine alienCoroutine;
    
    void Start()
    {
        CreateMainMenuUI();
        LoadUnlockedLevels();
        UpdateLevelButtons();
        
        // Set up audio source for sound effects
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Start random alien appearance
        alienCoroutine = StartCoroutine(RandomAlienAppearance());
    }
    
    void CreateMainMenuUI()
    {
        // Find or create canvas
        mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        
        // Ensure EventSystem exists for UI interaction
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        
        // Create background image
        if (backgroundImage == null)
        {
            GameObject bgObj = new GameObject("BackgroundImage");
            bgObj.transform.SetParent(mainCanvas.transform, false);
            backgroundImage = bgObj.AddComponent<RawImage>();
            RectTransform bgRect = backgroundImage.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            // Assign background texture if available
            if (menuBackgroundTexture != null)
            {
                backgroundImage.texture = menuBackgroundTexture;
            }
            else
            {
                // Create a simple gradient background
                backgroundImage.color = new Color(0.1f, 0.1f, 0.2f);
            }
        }
        
        // Create main menu panel
        if (mainMenuPanel == null)
        {
            mainMenuPanel = CreatePanel("MainMenuPanel", mainCanvas.transform);
        }
        
        // Create level select panel
        if (levelSelectPanel == null)
        {
            levelSelectPanel = CreatePanel("LevelSelectPanel", mainCanvas.transform);
            levelSelectPanel.SetActive(false);
        }
        
        // Create controls panel
        CreateControlsPanel();
        
        // Create play button
        if (playButton == null)
        {
            playButton = CreateButton("PlayButton", mainMenuPanel.transform, "PLAY", new Vector2(0, 100));
            playButton.onClick.AddListener(ShowLevelSelect);
        }
        
        // Create controls button
        CreateControlsButton();
        
        // Create quit button
        if (quitButton == null)
        {
            quitButton = CreateButton("QuitButton", mainMenuPanel.transform, "QUIT", new Vector2(0, -100));
            quitButton.onClick.AddListener(QuitGame);
        }
        
        // Create level buttons
        CreateLevelButtons();
        
        // Create alien image (hidden by default)
        if (alienImage == null)
        {
            GameObject alienObj = new GameObject("AlienImage");
            alienObj.transform.SetParent(mainCanvas.transform, false);
            alienImage = alienObj.AddComponent<RawImage>();
            RectTransform alienRect = alienImage.GetComponent<RectTransform>();
            alienRect.anchorMin = new Vector2(1, 1);
            alienRect.anchorMax = new Vector2(1, 1);
            alienRect.pivot = new Vector2(0.5f, 0.5f);
            alienRect.sizeDelta = new Vector2(100, 100);
            alienRect.anchoredPosition = new Vector2(-100, -100);
            alienImage.gameObject.SetActive(false);
        }
        
        // Setup video player
        SetupVideoPlayer();
    }
    
    GameObject CreatePanel(string name, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image img = panel.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.5f);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        return panel;
    }
    
    Button CreateButton(string name, Transform parent, string text, Vector2 position)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        
        Image img = btnObj.AddComponent<Image>();
        img.color = Color.white;
        
        Button btn = btnObj.AddComponent<Button>();
        
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(200, 60);
        rect.anchoredPosition = position;
        
        // Create text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = 24;
        tmpText.color = Color.black;
        tmpText.alignment = TextAlignmentOptions.Center;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        return btn;
    }
    
    void CreateLevelButtons()
    {
        string[] levelNames = new string[]
        {
            "Level 1: Calm Start",
            "Level 2: Windy Heights",
            "Level 3: Rainy Storm",
            "Level 4: Snowy Clouds",
            "Level 5: Boss Battle"
        };
        
        string[] levelDescs = new string[]
        {
            "Start at 0m - Build to 10m!",
            "Start at 10m - Build to 20m!\nWind affects blocks",
            "Start at 20m - Build to 30m!\nRain and power-ups",
            "Start at 30m - Build to 40m!\nSnow falls from above",
            "Start at 40m - Build to 50m!\nDefeat the boss!"
        };
        
        // Create a title for the level select screen
        GameObject titleObj = new GameObject("LevelSelectTitle");
        titleObj.transform.SetParent(levelSelectPanel.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "SELECT LEVEL";
        titleText.fontSize = 48;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;
        
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0, -50);
        titleRect.sizeDelta = new Vector2(400, 60);
        
        // Create level buttons VERTICALLY STACKED
        for (int i = 0; i < 5; i++)
        {
            int levelIndex = i; // Capture for closure
            
            // Calculate vertical position (stacked from top to bottom)
            float yPosition = 150 - (i * 80); // Start at 150, go down by 80 each time
            
            // Create button
            if (levelButtons[i] == null)
            {
                levelButtons[i] = CreateButton(
                    $"Level{i+1}Button", 
                    levelSelectPanel.transform, 
                    levelNames[i], 
                    new Vector2(-200, yPosition) // Left side for button
                );
                levelButtons[i].onClick.AddListener(() => SelectLevel(levelIndex));
                
                RectTransform btnRect = levelButtons[i].GetComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(250, 60); // Wider buttons for text
                btnRect.anchoredPosition = new Vector2(-200, yPosition);
            }
            
            // Create description to the RIGHT of button
            if (levelDescriptions[i] == null)
            {
                GameObject descObj = new GameObject($"Level{i+1}Description");
                descObj.transform.SetParent(levelSelectPanel.transform, false);
                levelDescriptions[i] = descObj.AddComponent<TextMeshProUGUI>();
                levelDescriptions[i].text = levelDescs[i];
                levelDescriptions[i].fontSize = 16;
                levelDescriptions[i].color = Color.white;
                levelDescriptions[i].alignment = TextAlignmentOptions.Left;
                
                RectTransform descRect = descObj.GetComponent<RectTransform>();
                descRect.anchorMin = new Vector2(0.5f, 0.5f);
                descRect.anchorMax = new Vector2(0.5f, 0.5f);
                descRect.pivot = new Vector2(0, 0.5f);
                descRect.anchoredPosition = new Vector2(80, yPosition); // Right side for description
                descRect.sizeDelta = new Vector2(300, 60);
            }
            
            // Create score display below description
            if (levelScores[i] == null)
            {
                GameObject scoreObj = new GameObject($"Level{i+1}Score");
                scoreObj.transform.SetParent(levelSelectPanel.transform, false);
                levelScores[i] = scoreObj.AddComponent<TextMeshProUGUI>();
                levelScores[i].text = "Best Score: -";
                levelScores[i].fontSize = 14;
                levelScores[i].color = Color.yellow;
                levelScores[i].alignment = TextAlignmentOptions.Left;
                
                RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
                scoreRect.anchorMin = new Vector2(0.5f, 0.5f);
                scoreRect.anchorMax = new Vector2(0.5f, 0.5f);
                scoreRect.pivot = new Vector2(0, 0.5f);
                scoreRect.anchoredPosition = new Vector2(80, yPosition - 25); // Below description
                scoreRect.sizeDelta = new Vector2(300, 30);
            }
        }
        
        // Back button at the bottom
        Button backButton = CreateButton("BackFromLevelButton", levelSelectPanel.transform, "BACK", new Vector2(0, -250));
        backButton.onClick.AddListener(ShowMainMenu);
    }
    
    void SetupVideoPlayer()
    {
        if (videoPlayer == null)
        {
            GameObject videoObj = new GameObject("VideoPlayer");
            videoObj.transform.SetParent(mainCanvas.transform, false);
            videoPlayer = videoObj.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        }
        
        if (videoDisplay == null)
        {
            GameObject displayObj = new GameObject("VideoDisplay");
            displayObj.transform.SetParent(mainCanvas.transform, false);
            videoDisplay = displayObj.AddComponent<RawImage>();
            RectTransform displayRect = videoDisplay.GetComponent<RectTransform>();
            displayRect.anchorMin = Vector2.zero;
            displayRect.anchorMax = Vector2.one;
            displayRect.sizeDelta = Vector2.zero;
            videoDisplay.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// CRITICAL: This is called when returning to menu - ensures proper cleanup and reset
    /// Fixed to avoid circular reference with GameManager
    /// </summary>
    public void ShowMainMenu(bool skipCleanup = false)
    {
        // Make sure time scale is normal
        Time.timeScale = 1f;
        
        // Save current level's score if coming back from a game
        if (!skipCleanup)
        {
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null)
            {
                // Save score if level was active
                if (selectedLevel >= 0 && selectedLevel < 5)
                {
                    int finalScore = gm.currentScore;
                    // Update high score if this is better
                    if (finalScore > levelHighScores[selectedLevel])
                    {
                        levelHighScores[selectedLevel] = finalScore;
                        PlayerPrefs.SetInt($"Level{selectedLevel}HighScore", finalScore);
                        PlayerPrefs.Save();
                    }
                }
                
                // Just call cleanup, don't call ReturnToMenu to avoid circular reference
                gm.CleanupGameDirect();
            }
        }
        
        // Reload scores from PlayerPrefs
        LoadUnlockedLevels();
        
        // Hide any victory text or game UI
        ClearGameUI();
        
        // Show main menu panel
        mainMenuPanel.SetActive(true);
        levelSelectPanel.SetActive(false);
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(false);
        }
        
        // Make sure canvas is visible
        mainCanvas.gameObject.SetActive(true);
        
        // Restart alien animation if it was stopped
        if (alienCoroutine == null)
        {
            alienCoroutine = StartCoroutine(RandomAlienAppearance());
        }
        
        // Hide video display if it was showing
        if (videoDisplay != null)
        {
            videoDisplay.gameObject.SetActive(false);
        }
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
        
        // Update level buttons in case any were completed
        UpdateLevelButtons();
        
        Debug.Log("Returned to main menu - all game objects cleaned up");
    }
    
    /// <summary>
    /// Overloaded version for parameterless calls (for button onClick events)
    /// </summary>
    public void ShowMainMenu()
    {
        ShowMainMenu(false);
    }
    
    /// <summary>
    /// Clear any lingering game UI elements
    /// </summary>
    void ClearGameUI()
    {
        // Find and hide/reset any game UI that might be visible
        TextMeshProUGUI[] allTexts = FindObjectsOfType<TextMeshProUGUI>();
        foreach (var text in allTexts)
        {
            if (text.name.Contains("PowerUp") || text.name.Contains("Victory"))
            {
                text.text = "";
            }
        }
        
        // Hide game over panels
        GameObject gameOverPanel = GameObject.Find("GameOverPanel");
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }
    
    void ShowLevelSelect()
    {
        mainMenuPanel.SetActive(false);
        levelSelectPanel.SetActive(true);
    }
    
    void UpdateLevelButtons()
    {
        if (!enableLevelLocking)
        {
            // Unlock all levels
            unlockedLevels = 5;
        }
        
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] != null)
            {
                bool isUnlocked = (i < unlockedLevels) || !enableLevelLocking;
                levelButtons[i].interactable = isUnlocked;
                
                // Visual feedback for locked levels
                Image btnImage = levelButtons[i].GetComponent<Image>();
                if (btnImage != null)
                {
                    btnImage.color = isUnlocked ? Color.white : Color.gray;
                }
                
                // Update text color
                TextMeshProUGUI btnText = levelButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.color = isUnlocked ? Color.black : Color.gray;
                }
            }
            
            // Update score display
            if (levelScores[i] != null)
            {
                if (levelHighScores[i] > 0)
                {
                    levelScores[i].text = $"Best Score: {levelHighScores[i]}";
                }
                else
                {
                    levelScores[i].text = "Best Score: -";
                }
            }
        }
    }
    
    void LoadUnlockedLevels()
    {
        unlockedLevels = PlayerPrefs.GetInt("UnlockedLevels", 1);
        if (!enableLevelLocking)
        {
            unlockedLevels = 5;
        }
        
        // Load high scores for each level
        for (int i = 0; i < 5; i++)
        {
            levelHighScores[i] = PlayerPrefs.GetInt($"Level{i}HighScore", 0);
        }
    }
    
    void SaveUnlockedLevels()
    {
        if (enableLevelLocking)
        {
            PlayerPrefs.SetInt("UnlockedLevels", unlockedLevels);
            PlayerPrefs.Save();
        }
    }
    
    void SelectLevel(int level)
    {
        selectedLevel = level;
        
        // Initialize level config if not set
        if (levelConfigs[level] == null)
        {
            levelConfigs[level] = new LevelConfig
            {
                levelNumber = level + 1,
                levelName = $"Level {level + 1}",
                baseHeight = level * 10f,
                targetHeight = (level + 1) * 10f,
                hasWind = level >= 1,
                hasPowerups = level >= 2,
                hasRain = level == 2,
                hasSnow = level == 3,
                hasBoss = level == 4,
                unlockedCarCount = Mathf.Min(level + 1, 3)
            };
        }
        
        // Start cutscene if available, otherwise start game directly
        StartCoroutine(PlayCutsceneAndStart());
    }
    
    IEnumerator PlayCutsceneAndStart()
    {
        isPlayingCutscene = true;
        
        // Hide menu
        levelSelectPanel.SetActive(false);
        
        // Play cutscene if available
        if (levelStartCutscene != null)
        {
            videoDisplay.gameObject.SetActive(true);
            videoPlayer.clip = levelStartCutscene;
            
            // Create render texture for video
            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            videoPlayer.targetTexture = rt;
            videoDisplay.texture = rt;
            
            videoPlayer.Play();
            
            // Wait for 5 seconds
            yield return new WaitForSeconds(5f);
            
            videoPlayer.Stop();
            videoDisplay.gameObject.SetActive(false);
        }
        else
        {
            // If no cutscene, just wait a moment
            yield return new WaitForSeconds(1f);
        }
        
        isPlayingCutscene = false;
        
        // Load game scene with level data
        StartGame();
    }
    
    void StartGame()
    {
        // Save selected level data
        PlayerPrefs.SetInt("SelectedLevel", selectedLevel);
        PlayerPrefs.Save();
        
        // Load game scene (assumes you have a game scene)
        // If you're using the same scene, just initialize the game
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            // Hide menu
            mainCanvas.gameObject.SetActive(false);
            
            // Stop alien animation while playing
            if (alienCoroutine != null)
            {
                StopCoroutine(alienCoroutine);
                alienCoroutine = null;
            }
            
            // Initialize game with level config
            gm.InitializeLevel(levelConfigs[selectedLevel]);
        }
        else
        {
            // Load game scene if separate
            SceneManager.LoadScene("GameScene");
        }
    }
    
    /// <summary>
    /// Play victory sound effect
    /// </summary>
    public void PlayVictorySound()
    {
        if (victorySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(victorySound, victorySoundVolume);
        }
        else
        {
            Debug.Log("Victory! (No sound effect assigned)");
        }
    }
    
    public IEnumerator PlayLevelCompleteAndReturn()
    {
        // Save the score from GameManager before it gets cleaned up
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null && selectedLevel >= 0 && selectedLevel < 5)
        {
            int finalScore = gm.currentScore;
            // Update high score if this is better
            if (finalScore > levelHighScores[selectedLevel])
            {
                levelHighScores[selectedLevel] = finalScore;
                PlayerPrefs.SetInt($"Level{selectedLevel}HighScore", finalScore);
                PlayerPrefs.Save();
            }
        }
        
        // Play victory sound immediately
        PlayVictorySound();
        
        // Play completion cutscene
        if (levelCompleteCutscene != null)
        {
            videoDisplay.gameObject.SetActive(true);
            videoPlayer.clip = levelCompleteCutscene;
            
            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            videoPlayer.targetTexture = rt;
            videoDisplay.texture = rt;
            
            videoPlayer.Play();
            
            yield return new WaitForSeconds(5f);
            
            videoPlayer.Stop();
            videoDisplay.gameObject.SetActive(false);
        }
        else
        {
            // If no cutscene, wait a bit for the sound to play
            yield return new WaitForSeconds(2f);
        }
        
        // Unlock next level (only if level locking is enabled)
        if (enableLevelLocking && selectedLevel + 1 < 5)
        {
            unlockedLevels = Mathf.Max(unlockedLevels, selectedLevel + 2);
            SaveUnlockedLevels();
        }
        
        // Return to main menu - pass true to skip cleanup since it's already done
        ShowMainMenu(true);
    }
    
    IEnumerator RandomAlienAppearance()
    {
        while (true)
        {
            // Wait random interval
            float waitTime = Random.Range(alienMinInterval, alienMaxInterval);
            yield return new WaitForSeconds(waitTime);
            
            // Only show if not in cutscene
            if (!isPlayingCutscene)
            {
                yield return StartCoroutine(ShowAlienAnimation());
            }
        }
    }
    
    IEnumerator ShowAlienAnimation()
    {
        alienImage.gameObject.SetActive(true);
        
        RectTransform alienRect = alienImage.GetComponent<RectTransform>();
        
        // Start position (off screen right)
        alienRect.anchoredPosition = new Vector2(Screen.width + 100, -100);
        
        float elapsed = 0f;
        
        while (elapsed < alienAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / alienAnimationDuration;
            
            // Move across screen
            float xPos = Mathf.Lerp(Screen.width + 100, -200, t);
            alienRect.anchoredPosition = new Vector2(xPos, -100);
            
            // Spin
            alienRect.Rotate(Vector3.forward, 360f * Time.deltaTime / alienAnimationDuration * 2f);
            
            yield return null;
        }
        
        alienImage.gameObject.SetActive(false);
    }
    
    void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    
    void CreateControlsPanel()
    {
        if (controlsPanel == null)
        {
            controlsPanel = CreatePanel("ControlsPanel", mainCanvas.transform);
            controlsPanel.SetActive(false);
            
            // Title
            GameObject titleObj = new GameObject("ControlsTitle");
            titleObj.transform.SetParent(controlsPanel.transform, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "CONTROLS";
            titleText.fontSize = 48;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;
            
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -50);
            titleRect.sizeDelta = new Vector2(800, 60);
            
            // Controls text
            GameObject controlsTextObj = new GameObject("ControlsText");
            controlsTextObj.transform.SetParent(controlsPanel.transform, false);
            TextMeshProUGUI controlsText = controlsTextObj.AddComponent<TextMeshProUGUI>();
            controlsText.text = 
                "WASD / Arrow Keys - Move block horizontally\n\n" +
                "Q / E - Rotate block left/right\n\n" +
                "SPACE - Drop block\n\n" +
                "ESC - Pause / Return to menu\n\n" +
                "GOAL:\n" +
                "Build your tower to the target height!\n" +
                "Blocks freeze after 3 seconds of no movement.\n\n" +
                "POWER-UPS:\n" +
                "FREEZE - Instantly locks current block in place\n" +
                "GLUE - Makes blocks stick better for 10 seconds";
            
            controlsText.fontSize = 28;
            controlsText.color = Color.white;
            controlsText.alignment = TextAlignmentOptions.TopLeft;
            
            RectTransform controlsRect = controlsTextObj.GetComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0.5f, 0.5f);
            controlsRect.anchorMax = new Vector2(0.5f, 0.5f);
            controlsRect.pivot = new Vector2(0.5f, 0.5f);
            controlsRect.anchoredPosition = new Vector2(0, 0);
            controlsRect.sizeDelta = new Vector2(800, 600);
            
            // Back button
            Button backBtn = CreateButton("BackFromControlsButton", controlsPanel.transform, "BACK", new Vector2(0, -300));
            backBtn.onClick.AddListener(ShowMainMenu);
        }
    }
    
    void CreateControlsButton()
    {
        if (controlsButton == null)
        {
            controlsButton = CreateButton("ControlsButton", mainMenuPanel.transform, "CONTROLS", new Vector2(0, 0));
            controlsButton.onClick.AddListener(ShowControls);
        }
    }
    
    void ShowControls()
    {
        mainMenuPanel.SetActive(false);
        levelSelectPanel.SetActive(false);
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(true);
        }
    }
}

/// <summary>
/// Configuration data for each level
/// </summary>
[System.Serializable]
public class LevelConfig
{
    public int levelNumber;
    public string levelName;
    public float baseHeight; // Starting height (0, 10, 20, 30, 40)
    public float targetHeight; // Win height (10, 20, 30, 40, 50)
    public bool hasWind;
    public bool hasPowerups;
    public bool hasRain;
    public bool hasSnow;
    public bool hasBoss;
    public int unlockedCarCount; // 1, 2, or 3 car types available
}