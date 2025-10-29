using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Import TextMeshPro

public class GameManager : MonoBehaviour
{
    [Header("Game Objects")]
    public GameObject[] voxelBlockPrefabs; // Array of different voxel block prefabs from asset pack
    
    [Header("Block Scale Settings")]
    [Tooltip("Global scale multiplier for all blocks")]
    public float globalBlockScale = 0.5f;
    
    [Tooltip("Individual scale multipliers for each block prefab (matches voxelBlockPrefabs array)")]
    public float[] blockPrefabScaleMultipliers; // Individual scale for each block type

    [Header("Platform Settings - NO PREFAB NEEDED")]
    [Tooltip("Platform dimensions (width, height, depth)")]
    public Vector3 platformDimensions = new Vector3(30f, 1f, 10f);
    
    [Tooltip("Platform color")]
    public Color platformColor = new Color(0.5f, 0.5f, 0.5f);
    
    [Tooltip("Platform material (optional)")]
    public Material platformMaterial;

    [Header("Spawn Settings")]
    public Transform spawnPoint;
    public float spawnHeight = 10f;

    [Header("Game Settings")]
    public float freezeDelay = 3f;
    public float despawnHeight = -20f;
    public float moveSpeed = 5f; // Movement speed for current block
    public float rotationSpeed = 90f;

    [Header("Layer Settings")]
    public string previewLayer = "PreviewBlock"; // Layer for blocks before dropping
    public string droppedLayer = "DroppedBlock"; // Layer for blocks after dropping

    [Header("Power-Up Settings")]
    // Freeze and Glue power-ups are handled by PowerUpManager

    [Header("Weather System")]
    public WeatherSystem weatherSystem; // Assign in inspector or will find automatically

    [Header("UI - Auto-Generated TextMeshPro")]
    [Tooltip("These will be created automatically if not assigned")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI heightText;
    public TextMeshProUGUI powerUpText;
    public TextMeshProUGUI weatherText; // Keeping variable but won't use it
    public TextMeshProUGUI windStrengthText; // Display current wind strength
    public GameObject gameOverPanel;
    public Button pauseButton; // Button to pause and return to menu
    private Canvas mainCanvas;

    [Header("Game State")]
    public int currentScore = 0;
    public float currentMaxHeight = 0f;
    public bool isGameOver = false;
    public bool isPaused = false;
    public int blocksUsed = 0; // Track number of blocks/cars used

    [Header("Level Settings")]
    public LevelConfig currentLevel; // Current level configuration
    public float targetHeight = 10f; // Height needed to win
    public bool isLevelMode = false; // True when playing a specific level

    // Private variables
    private GameObject currentPlatform;
    public GameObject currentBlock; // Made public for PowerUpManager access
    private List<GameObject> activeBlocks = new List<GameObject>();
    private Vector2 movementBounds = new Vector2(10f, 10f); // X and Y boundaries for 2D movement
    private CameraController cameraController;
    private MainMenuManager mainMenuManager;
    private BlockDropper blockDropper;

    void Start()
    {
        // Check if we should initialize from a level selection
        mainMenuManager = FindObjectOfType<MainMenuManager>();
        
        // If main menu exists and is active, wait for it to start the game
        if (mainMenuManager != null && mainMenuManager.gameObject.activeInHierarchy)
        {
            // Main menu will call InitializeLevel when ready
            return;
        }
        
        // Otherwise, start normally (for testing or standalone play)
        InitializeLayers();
        InitializeGame();
    }

    void Update()
    {
        if (!isGameOver && !isPaused)
        {
            HandleInput();
            UpdateCurrentBlockPosition(); // Keep current block at camera height
            UpdateHeight();
            UpdateScore(); // Score is based on max height
            CheckFallenBlocks();
            UpdateUI();
            
            // Check for level completion
            if (isLevelMode)
            {
                CheckLevelVictory();
            }
        }
    }

    void InitializeLayers()
    {
    }

    void InitializeGame()
    {
        // Find camera controller
        cameraController = Camera.main.GetComponent<CameraController>();

        // Find or create canvas
        mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            GameObject canvasObj = new GameObject("MainCanvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create UI elements if they don't exist
        CreateUIElements();

        // Find or add weather system
        if (weatherSystem == null)
        {
            weatherSystem = FindObjectOfType<WeatherSystem>();
            if (weatherSystem == null)
            {
                weatherSystem = gameObject.AddComponent<WeatherSystem>();
            }
        }

        // Link weather system UI (but we won't show weather text)
        if (weatherSystem != null && weatherText != null)
        {
            weatherSystem.weatherText = weatherText;
        }

        // ALWAYS create platform procedurally - no prefab needed
        CreatePlatform();

        // Set up spawn point if not assigned
        if (spawnPoint == null)
        {
            GameObject spawnObj = new GameObject("SpawnPoint");
            spawnPoint = spawnObj.transform;
            spawnPoint.position = new Vector3(0, spawnHeight, 0);
        }

        // Find or create block dropper
        blockDropper = FindObjectOfType<BlockDropper>();
        if (blockDropper == null)
        {
            GameObject dropperObj = new GameObject("BlockDropper");
            dropperObj.transform.position = spawnPoint.position;
            blockDropper = dropperObj.AddComponent<BlockDropper>();
        }
        blockDropper.Initialize(this);

        // Reset camera at game start
        if (cameraController != null)
        {
            cameraController.ResetCamera();
        }

        // Initialize scale array if not set
        if (blockPrefabScaleMultipliers == null || blockPrefabScaleMultipliers.Length != voxelBlockPrefabs.Length)
        {
            blockPrefabScaleMultipliers = new float[voxelBlockPrefabs.Length];
            for (int i = 0; i < blockPrefabScaleMultipliers.Length; i++)
            {
                blockPrefabScaleMultipliers[i] = 1f; // Default to 1x multiplier
            }
        }

        // Spawn first block
        SpawnBlock();
    }

    /// <summary>
    /// Creates the platform procedurally without needing a prefab
    /// </summary>
    void CreatePlatform()
    {
        // Destroy existing platform if any
        if (currentPlatform != null)
        {
            Destroy(currentPlatform);
        }

        // Create new platform
        currentPlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        currentPlatform.name = "Base_Platform";
        currentPlatform.transform.position = Vector3.zero;
        currentPlatform.transform.localScale = platformDimensions;
        
        // Set platform material/color
        MeshRenderer renderer = currentPlatform.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            if (platformMaterial != null)
            {
                renderer.material = platformMaterial;
            }
            else
            {
                renderer.material.color = platformColor;
            }
        }
        
        // Make sure platform is on default layer and visible
        currentPlatform.layer = LayerMask.NameToLayer("Default");
        
        // Ensure platform has proper collider
        BoxCollider platformCollider = currentPlatform.GetComponent<BoxCollider>();
        if (platformCollider == null)
        {
            platformCollider = currentPlatform.AddComponent<BoxCollider>();
        }
        
        Debug.Log($"Platform created at {currentPlatform.transform.position} with dimensions {platformDimensions}");
    }

    void CreateUIElements()
    {
        // Create score text (top left with black background)
        if (scoreText == null)
        {
            GameObject scoreObj = new GameObject("ScoreText");
            scoreObj.transform.SetParent(mainCanvas.transform, false);
            
            // Create background panel for score
            Image bgImage = scoreObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f);
            
            RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0, 1);
            scoreRect.anchorMax = new Vector2(0, 1);
            scoreRect.pivot = new Vector2(0, 1);
            scoreRect.sizeDelta = new Vector2(200, 80);
            scoreRect.anchoredPosition = new Vector2(10, -10);

            // Create text child
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(scoreObj.transform, false);
            scoreText = textObj.AddComponent<TextMeshProUGUI>();
            scoreText.text = "Score: 0\nBlocks: 0";
            scoreText.fontSize = 24;
            scoreText.color = Color.white;
            scoreText.alignment = TextAlignmentOptions.TopLeft;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);
        }

        // Create height text (top right with black background)
        if (heightText == null)
        {
            GameObject heightObj = new GameObject("HeightText");
            heightObj.transform.SetParent(mainCanvas.transform, false);
            
            // Create background panel
            Image bgImage = heightObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f);
            
            RectTransform heightRect = heightObj.GetComponent<RectTransform>();
            heightRect.anchorMin = new Vector2(1, 1);
            heightRect.anchorMax = new Vector2(1, 1);
            heightRect.pivot = new Vector2(1, 1);
            heightRect.sizeDelta = new Vector2(250, 100);
            heightRect.anchoredPosition = new Vector2(-10, -10);

            // Create text child
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(heightObj.transform, false);
            heightText = textObj.AddComponent<TextMeshProUGUI>();
            heightText.text = "Height: 0.0m";
            heightText.fontSize = 24;
            heightText.color = Color.white;
            heightText.alignment = TextAlignmentOptions.TopRight;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);
        }

        // Create power-up text (center top)
        if (powerUpText == null)
        {
            GameObject powerUpObj = new GameObject("PowerUpText");
            powerUpObj.transform.SetParent(mainCanvas.transform, false);
            
            powerUpText = powerUpObj.AddComponent<TextMeshProUGUI>();
            powerUpText.text = "";
            powerUpText.fontSize = 36;
            powerUpText.color = Color.yellow;
            powerUpText.alignment = TextAlignmentOptions.Center;
            
            RectTransform powerUpRect = powerUpObj.GetComponent<RectTransform>();
            powerUpRect.anchorMin = new Vector2(0.5f, 1);
            powerUpRect.anchorMax = new Vector2(0.5f, 1);
            powerUpRect.pivot = new Vector2(0.5f, 1);
            powerUpRect.sizeDelta = new Vector2(600, 100);
            powerUpRect.anchoredPosition = new Vector2(0, -100);
        }

        // NO WEATHER TEXT - We're removing this display
        
        // NO WIND STRENGTH TEXT - We're removing this display too

        // Create game over panel (initially hidden)
        if (gameOverPanel == null)
        {
            GameObject gameOverObj = new GameObject("GameOverPanel");
            gameOverObj.transform.SetParent(mainCanvas.transform, false);
            
            Image img = gameOverObj.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.8f);
            
            RectTransform goRect = gameOverObj.GetComponent<RectTransform>();
            goRect.anchorMin = Vector2.zero;
            goRect.anchorMax = Vector2.one;
            goRect.sizeDelta = Vector2.zero;
            
            // Create game over text
            GameObject goText = new GameObject("GameOverText");
            goText.transform.SetParent(gameOverObj.transform, false);
            
            TextMeshProUGUI text = goText.AddComponent<TextMeshProUGUI>();
            text.text = "GAME OVER";
            text.fontSize = 72;
            text.color = Color.red;
            text.alignment = TextAlignmentOptions.Center;
            
            RectTransform textRect = goText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(600, 200);
            textRect.anchoredPosition = Vector2.zero;
            
            gameOverPanel = gameOverObj;
            gameOverPanel.SetActive(false);
        }
        
        // Create pause button - CENTER TOP
        if (pauseButton == null)
        {
            GameObject pauseObj = new GameObject("PauseButton");
            pauseObj.transform.SetParent(mainCanvas.transform, false);
            
            Image btnImg = pauseObj.AddComponent<Image>();
            btnImg.color = Color.white;
            
            pauseButton = pauseObj.AddComponent<Button>();
            pauseButton.onClick.AddListener(PauseAndMenu);
            
            RectTransform pauseRect = pauseObj.GetComponent<RectTransform>();
            // CENTER TOP
            pauseRect.anchorMin = new Vector2(0.5f, 1);
            pauseRect.anchorMax = new Vector2(0.5f, 1);
            pauseRect.pivot = new Vector2(0.5f, 1);
            pauseRect.sizeDelta = new Vector2(120, 40);
            pauseRect.anchoredPosition = new Vector2(0, -10); // 10 pixels from top
            
            // Create button text
            GameObject btnText = new GameObject("Text");
            btnText.transform.SetParent(pauseObj.transform, false);
            
            TextMeshProUGUI buttonText = btnText.AddComponent<TextMeshProUGUI>();
            buttonText.text = "MENU";
            buttonText.fontSize = 20;
            buttonText.color = Color.black;
            buttonText.alignment = TextAlignmentOptions.Center;
            
            RectTransform btnTextRect = btnText.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;
        }
    }

    void HandleInput()
    {
        if (currentBlock == null) 
            return;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Move block horizontally (no Z movement for 2D)
        if (horizontal != 0 || vertical != 0)
        {
            Vector3 movement = new Vector3(horizontal, 0, 0) * moveSpeed * Time.deltaTime;
            currentBlock.transform.position += movement;

            // Clamp position within bounds (no Z clamping for 2D)
            Vector3 pos = currentBlock.transform.position;
            pos.x = Mathf.Clamp(pos.x, -movementBounds.x, movementBounds.x);
            pos.z = 0f; // Lock Z to 0 for 2D
            currentBlock.transform.position = pos;
        }

        // Rotation controls (disabled for sprites facing camera)
        // (This was originally for a 3d version )
        /*
        if (Input.GetKey(KeyCode.Q))
        {
            currentBlock.transform.Rotate(Vector3.up, -rotationSpeed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.E))
        {
            currentBlock.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
        */

        // Drop block
        if (Input.GetKeyDown(KeyCode.Space))
        {
            DropCurrentBlock();
        }
    }

    void UpdateCurrentBlockPosition()
    {
        // Keep current block at a height relative to camera
        if (currentBlock != null && !currentBlock.GetComponent<BlockController>()?.isDropped == true)
        {
            if (cameraController != null)
            {
                Vector3 blockPos = currentBlock.transform.position;
                blockPos.y = cameraController.transform.position.y + 5f; // Keep block 5 units above camera center
                currentBlock.transform.position = blockPos;
            }
        }
    }

    void DropCurrentBlock()
    {
        if (currentBlock != null)
        {
            BlockController bc = currentBlock.GetComponent<BlockController>();
            if (bc != null)
            {
                bc.StartDrop();
                activeBlocks.Add(currentBlock);
                currentBlock = null;
                
                // Increment blocks used count
                blocksUsed++;
                
                // Spawn new block after a short delay
                Invoke(nameof(SpawnBlock), 0.5f);
            }
        }
    }

    void SpawnBlock()
    {
        if (voxelBlockPrefabs != null && voxelBlockPrefabs.Length > 0)
        {
            // Select a random block prefab
            int randomIndex = Random.Range(0, voxelBlockPrefabs.Length);
            GameObject selectedPrefab = voxelBlockPrefabs[randomIndex];

            if (selectedPrefab != null)
            {
                // Spawn the block at the spawn point
                currentBlock = Instantiate(selectedPrefab, spawnPoint.position, Quaternion.identity);
                currentBlock.name = "VoxelBlock_" + Time.time;
                
                // Apply scale multiplier for this specific block type
                float scaleMultiplier = 1f;
                if (blockPrefabScaleMultipliers != null && randomIndex < blockPrefabScaleMultipliers.Length)
                {
                    scaleMultiplier = blockPrefabScaleMultipliers[randomIndex];
                }
                
                // Apply both global and specific scale
                currentBlock.transform.localScale = Vector3.one * globalBlockScale * scaleMultiplier;

                // Add and initialize BlockController
                BlockController bc = currentBlock.GetComponent<BlockController>();
                if (bc == null)
                {
                    bc = currentBlock.AddComponent<BlockController>();
                }
                bc.Initialize(this, freezeDelay);

                // Set layer for preview (doesn't collide with dropped blocks)
                SetLayerRecursively(currentBlock, LayerMask.NameToLayer(previewLayer));
                
                Debug.Log($"Spawned block: {selectedPrefab.name} with scale multiplier {scaleMultiplier} (total scale: {globalBlockScale * scaleMultiplier})");
            }
        }
        else
        {
            Debug.LogWarning("No voxel block prefabs assigned! Please add prefabs in the inspector.");
        }
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        if (layer < 0) return; // Invalid layer
        
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    public void OnBlockFrozen(GameObject block)
    {
        // Change layer to dropped so it collides with other blocks
        SetLayerRecursively(block, LayerMask.NameToLayer(droppedLayer));
        
        // Update max height
        float blockHeight = block.transform.position.y;
        if (blockHeight > currentMaxHeight)
        {
            currentMaxHeight = blockHeight;
        }
    }

    void CheckFallenBlocks()
    {
        for (int i = activeBlocks.Count - 1; i >= 0; i--)
        {
            if (activeBlocks[i] == null)
            {
                activeBlocks.RemoveAt(i);
                continue;
            }

            if (activeBlocks[i].transform.position.y < despawnHeight)
            {
                Destroy(activeBlocks[i]);
                activeBlocks.RemoveAt(i);
            }
        }
    }

    void UpdateHeight()
    {
        // Track the maximum height reached
        foreach (GameObject block in activeBlocks)
        {
            if (block != null)
            {
                BlockController bc = block.GetComponent<BlockController>();
                if (bc != null && bc.isFrozen)
                {
                    float height = block.transform.position.y;
                    if (height > currentMaxHeight)
                    {
                        currentMaxHeight = height;
                    }
                }
            }
        }
    }

    void UpdateScore()
    {
        // ORIGINAL SCORE CALCULATION: Score based on height (10 points per meter) minus blocks used
        int heightScore = Mathf.RoundToInt(currentMaxHeight * 10);
        currentScore = heightScore - blocksUsed;
    }

    void UpdateUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {currentScore}\nBlocks: {blocksUsed}";
        }
        
        if (heightText != null)
        {
            if (isLevelMode)
            {
                heightText.text = $"Height: {currentMaxHeight:F1}m\nTarget: {targetHeight:F0}m";
            }
            else
            {
                heightText.text = $"Height: {currentMaxHeight:F1}m";
            }
        }
    }

    public void PauseAndMenu()
    {
        isPaused = true;
        Time.timeScale = 0f;
        ReturnToMenu();
    }

    public void GameOver()
    {
        isGameOver = true;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }

    /// <summary>
    /// Initialize the game with a specific level configuration
    /// Called by MainMenuManager when starting a level
    /// </summary>
    public void InitializeLevel(LevelConfig levelConfig)
    {
        // Clean up everything first
        CleanupGame();
        
        // Reset time scale in case it was paused
        Time.timeScale = 1f;
        
        // Reset all UI text to defaults
        ResetUIText();
        
        currentLevel = levelConfig;
        isLevelMode = true;
        targetHeight = levelConfig.targetHeight;
        
        InitializeLayers();
        InitializeGame();
        
        // Apply level-specific settings
        ApplyLevelSettings();
    }

    void ApplyLevelSettings()
    {
        if (currentLevel == null) return;

        // Configure weather system based on level settings
        if (weatherSystem != null)
        {
            // Check the level configuration flags and set weather accordingly
            if (currentLevel.hasWind)
            {
                weatherSystem.ChangeWeather(WeatherType.Windy);
                Debug.Log($"Level {currentLevel.levelNumber} ({currentLevel.levelName}) - Wind ENABLED with force: {weatherSystem.windForce}");
            }
            else if (currentLevel.hasRain)
            {
                weatherSystem.ChangeWeather(WeatherType.Rainy);
                Debug.Log($"Level {currentLevel.levelNumber} ({currentLevel.levelName}) - Rain enabled, Wind DISABLED");
            }
            else
            {
                weatherSystem.ChangeWeather(WeatherType.Sunny);
                Debug.Log($"Level {currentLevel.levelNumber} ({currentLevel.levelName}) - Sunny weather, Wind DISABLED");
            }
        }

        // Enable/disable power-ups based on level
        PowerUpManager powerUpManager = GetComponent<PowerUpManager>();
        if (powerUpManager != null)
        {
            powerUpManager.enabled = currentLevel.hasPowerups;
        }

        // Enable/disable boss system for level 5
        if (currentLevel.hasBoss)
        {
            BossSystem bossSystem = FindObjectOfType<BossSystem>();
            if (bossSystem == null)
            {
                gameObject.AddComponent<BossSystem>();
            }
        }

        // Enable snow dropper for level 4 (snowy level)
        if (currentLevel.hasSnow)
        {
            SnowDropper snowDropper = FindObjectOfType<SnowDropper>();
            if (snowDropper == null)
            {
                GameObject snowObj = new GameObject("SnowDropper");
                snowDropper = snowObj.AddComponent<SnowDropper>();
                snowDropper.Initialize(this);
            }
        }

        // Set starting height based on level
        if (currentLevel.baseHeight > 0)
        {
            // Move platform to starting height
            if (currentPlatform != null)
            {
                currentPlatform.transform.position = new Vector3(
                    currentPlatform.transform.position.x,
                    currentLevel.baseHeight,
                    currentPlatform.transform.position.z
                );
            }
            
            // Position camera at starting height using new method
            if (cameraController != null)
            {
                cameraController.ResetCameraToHeight(currentLevel.baseHeight);
            }
            
            // Update spawn point
            if (spawnPoint != null)
            {
                spawnPoint.position = new Vector3(
                    spawnPoint.position.x,
                    currentLevel.baseHeight + spawnHeight,
                    spawnPoint.position.z
                );
            }
            
            // Set current max height to base height
            currentMaxHeight = currentLevel.baseHeight;
        }

        // Update UI to show current height and target
        UpdateUI();
    }

    /// <summary>
    /// Check if the player has won the level
    /// </summary>
    void CheckLevelVictory()
    {
        if (isLevelMode && currentMaxHeight >= targetHeight)
        {
            OnLevelComplete();
        }
    }

    void OnLevelComplete()
    {
        isGameOver = true;
        isPaused = true;
        
        if (powerUpText != null)
        {
            powerUpText.text = "LEVEL COMPLETE!";
            powerUpText.fontSize = 72;
            powerUpText.color = Color.green;
        }
        
        // Play victory sound through MainMenuManager
        if (mainMenuManager != null)
        {
            mainMenuManager.PlayVictorySound();
        }

        // Return to main menu after delay
        StartCoroutine(ReturnToMenuAfterVictory());
    }

    IEnumerator ReturnToMenuAfterVictory()
    {
        yield return new WaitForSeconds(3f);

        // Find main menu manager and play completion cutscene
        if (mainMenuManager != null)
        {
            yield return StartCoroutine(mainMenuManager.PlayLevelCompleteAndReturn());
        }
        else
        {
            // If no main menu, just restart
            RestartGame();
        }
    }

    /// <summary>
    /// Return to main menu without completing level
    /// </summary>
    public void ReturnToMenu()
    {
        // Clean up before returning to menu
        CleanupGameDirect();
        
        if (mainMenuManager != null)
        {
            // ShowMainMenu with skipCleanup=true to avoid circular reference
            mainMenuManager.ShowMainMenu(true);
        }
        else
        {
            // Load main menu scene if separate
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }
    
    /// <summary>
    /// Direct cleanup method that doesn't call back to menu
    /// Used by MainMenuManager to avoid circular reference
    /// </summary>
    public void CleanupGameDirect()
    {
        CleanupGame();
    }

    /// <summary>
    /// Comprehensive cleanup of all game objects and states
    /// </summary>
    void CleanupGame()
    {
        // Destroy all active blocks
        foreach (GameObject block in activeBlocks)
        {
            if (block != null)
            {
                Destroy(block);
            }
        }
        activeBlocks.Clear();

        // Destroy current block
        if (currentBlock != null)
        {
            Destroy(currentBlock);
            currentBlock = null;
        }

        // Destroy platform
        if (currentPlatform != null)
        {
            Destroy(currentPlatform);
            currentPlatform = null;
        }
        
        // Destroy block dropper
        if (blockDropper != null)
        {
            Destroy(blockDropper.gameObject);
            blockDropper = null;
        }
        
        // Destroy any snow blocks (check if tag exists first)
        try 
        {
            GameObject[] snowBlocks = GameObject.FindGameObjectsWithTag("Snow");
            foreach (GameObject snow in snowBlocks)
            {
                Destroy(snow);
            }
        }
        catch (UnityException e)
        {
            Debug.LogWarning("Snow tag not found. Please add it in Edit -> Project Settings -> Tags and Layers");
        }
        
        // Destroy any power-ups (check if tag exists first)
        try 
        {
            GameObject[] powerUps = GameObject.FindGameObjectsWithTag("PowerUp");
            foreach (GameObject powerUp in powerUps)
            {
                Destroy(powerUp);
            }
        }
        catch (UnityException e)
        {
            Debug.LogWarning("PowerUp tag not found. Please add it in Edit -> Project Settings -> Tags and Layers");
        }
        
        // Destroy any boss projectiles (check if tag exists first)
        try
        {
            GameObject[] projectiles = GameObject.FindGameObjectsWithTag("EnemyProjectile");
            foreach (GameObject proj in projectiles)
            {
                Destroy(proj);
            }
        }
        catch (UnityException e)
        {
            Debug.LogWarning("EnemyProjectile tag not found. Please add it in Edit -> Project Settings -> Tags and Layers");
        }
        
        // Clean up boss system if it exists
        BossSystem bossSystem = FindObjectOfType<BossSystem>();
        if (bossSystem != null)
        {
            // Destroy boss sprite if it exists
            if (bossSystem.bossSprite != null)
            {
                Destroy(bossSystem.bossSprite);
            }
            Destroy(bossSystem.gameObject);
        }
        
        // Clean up snow dropper if it exists
        SnowDropper snowDropper = FindObjectOfType<SnowDropper>();
        if (snowDropper != null)
        {
            Destroy(snowDropper.gameObject);
        }
        
        // Reset camera if it exists
        if (cameraController != null)
        {
            cameraController.ResetCamera();
        }

        // Reset state variables
        isGameOver = false;
        isPaused = false;
        currentScore = 0;
        currentMaxHeight = 0f;
        blocksUsed = 0; // Reset blocks used counter
        
        // Hide game over panel if it exists
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        
        // Hide power-up text
        if (powerUpText != null)
        {
            powerUpText.text = "";
            powerUpText.fontSize = 36;
            powerUpText.color = Color.yellow;
        }
    }
    
    /// <summary>
    /// Reset all UI text to default values
    /// </summary>
    void ResetUIText()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: 0\nBlocks: 0";
        }
        
        if (heightText != null)
        {
            heightText.text = "Height: 0.0m";
        }
        
        if (powerUpText != null)
        {
            powerUpText.text = "";
            powerUpText.fontSize = 36; // Reset to default size
            powerUpText.color = Color.yellow; // Reset to default color
        }
        
        // We're not showing weather text anymore
        
        if (windStrengthText != null)
        {
            windStrengthText.text = "Wind: 0.0";
        }
    }
}