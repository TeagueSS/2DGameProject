using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Import TextMeshPro

public class GameManager : MonoBehaviour
{
    [Header("Game Objects")]
    // Our array of 'sprites' or blocks we drop 
    // It was originally voxels, but then the game looked bad 
    // so I changed it to a full 2D look.
    
    public GameObject[] voxelBlockPrefabs;

    [Header("Block Scale Settings")]
    [Tooltip("Global scale multiplier for all blocks")]
    // Scale multiplier to make the blocks scale actually seem fair in 
    // how far they have to stack 
    public float globalBlockScale = 0.5f;

    [Tooltip("Individual scale multipliers for each block prefab (matches voxelBlockPrefabs array)")]
    // Individual scale for each block type
    // This never ended up working, I just ended up using the global multiplier 
    public float[] blockPrefabScaleMultipliers;

    [Header("Platform Settings - NO PREFAB NEEDED")]
    [Tooltip("Platform dimensions (width, height, depth)")]
    // Spawn in our platform! 
    // I originally wanted this to be a prefab, but for some reason 
    // it wasn't taking so I hard coded the positoin 
    public Vector3 platformDimensions = new Vector3(30f, 1f, 10f);

    [Tooltip("Platform color")]
    // What color we want it to be, I made it gray to look like a road, 
    //but also not look weird in the clouds or the space scene 
    public Color platformColor = new Color(0.5f, 0.5f, 0.5f);

    [Tooltip("Platform material (optional)")]
    // This is un-used, I wanted to add a texture but it looked weird 
    public Material platformMaterial;

    [Header("Spawn Settings")]
    // Where we want our platform to spawn (Each level is changes)
    public Transform spawnPoint;
    // It's by default set to level 2, but this gets overwritten each level 
    public float spawnHeight = 10f;
    // All of our game settings 
    // Power up aren't used 

    [Header("Game Settings")]
    public float freezeDelay = 3f;
    public float despawnHeight = -20f;
    // Movement speed for current block
    public float moveSpeed = 5f; 
    public float rotationSpeed = 90f;

    [Header("Layer Settings")]
    // Layer for blocks before dropping
    public string previewLayer = "PreviewBlock"; 
    // Layer for blocks after dropping
    public string droppedLayer = "DroppedBlock";

    [Header("Power-Up Settings")]
    // Freeze and Glue power-ups are handled by PowerUpManager

    [Header("Weather System")]
    // Create our weather system! Originally was meant to do more than just wind / snow 
    // and add sound, but the sound was quite annoying.
    public WeatherSystem weatherSystem; 
    // Spawn our text mesh pro boxes in 

    [Header("UI - Auto-Generated TextMeshPro")]
    [Tooltip("These are spawned in but you can drag if you want it to look a certian way.")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI heightText;
    public TextMeshProUGUI powerUpText;
    // Keeping variable but won't use it
    public TextMeshProUGUI weatherText; 
    // Display current wind strength
    public TextMeshProUGUI windStrengthText; 
    public GameObject gameOverPanel;
    // Button to pause and return to menu
    // In the context of when you're playing theyre the same 
    public Button pauseButton;
    private Canvas mainCanvas;

    [Header("Game State")]
    public int currentScore = 0;
    public float currentMaxHeight = 0f;
    public bool isGameOver = false;
    public bool isPaused = false;
    // Track number of blocks/cars used
    // We use this to track calculate our score 
    // the more blocks used the less points you get 
    public int blocksUsed = 0;

    [Header("Level Settings")]
    // Current level configuration
    // This is the real meat and potatoes and where we assign our stuff 
    public LevelConfig currentLevel; 
    // Height needed to win
    public float targetHeight = 10f; 
    // Boss mode for the last level 
    public bool isLevelMode = false; 

    // Private variables
    private GameObject currentPlatform;
    // Made public for PowerUpManager access
    public GameObject currentBlock; 
    private List<GameObject> activeBlocks = new List<GameObject>();
    // X and Y boundaries for 2D movement
    private Vector2 movementBounds = new Vector2(10f, 10f); 
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

    // Update all of our objects 
    void Update()
    {
        if (!isGameOver && !isPaused)
        {
            HandleInput();
            // Keep current block at camera height
            UpdateCurrentBlockPosition(); 
            UpdateHeight();
            // Score is based on max height
            // and how many cars have been placed on the current level 
            UpdateScore(); 
            CheckFallenBlocks();
            UpdateUI();
            
            // Check for level completion
            if (isLevelMode)
            {
                // And double check they haven't gone high enough to end the game
                CheckLevelVictory();
            }
        }
    }


    void InitializeLayers()
    {
    }

    // Loadin all the things in our game 
    void InitializeGame()
    {
        // Find camera controller
        cameraController = Camera.main.GetComponent<CameraController>();

        // Find or create canvas
        mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            // And our menu, our canvas and everything else we need to render 
            GameObject canvasObj = new GameObject("MainCanvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create UI elements if they don't exist
        CreateUIElements();

        // Find or add weather system
        // This should be a dragged prefab 
        if (weatherSystem == null)
        {
            // In which case it goes here 
            weatherSystem = FindObjectOfType<WeatherSystem>();
            // But if they haven't created it add it so we can start the game 
            if (weatherSystem == null)
            {
                weatherSystem = gameObject.AddComponent<WeatherSystem>();
            }
        }

        // Link weather system UI (but we won't show weather text)
        if (weatherSystem != null && weatherText != null)
        {
            //
            weatherSystem.weatherText = weatherText;
        }

        // ALWAYS create platform procedurally - no prefab needed
        CreatePlatform();

        // Set up spawn point if not assigned
        if (spawnPoint == null)
        {
            // And spawn in our spawn point so all of our objects know where to go 
            GameObject spawnObj = new GameObject("SpawnPoint");
            spawnPoint = spawnObj.transform;
            spawnPoint.position = new Vector3(0, spawnHeight, 0);
        }

        // Find or create block dropper
        blockDropper = FindObjectOfType<BlockDropper>();
        if (blockDropper == null)
        {
            // Spawn in our block dropper 
            // And position it based on everything around it 
            GameObject dropperObj = new GameObject("BlockDropper");
            dropperObj.transform.position = spawnPoint.position;
            blockDropper = dropperObj.AddComponent<BlockDropper>();
        }
        //Initalize it to where it should be based on the level 
        blockDropper.Initialize(this);

        // Reset camera at game start
        if (cameraController != null)
        {
            // And reset our camera 
            cameraController.ResetCamera();
        }

        // Initialize scale array if not set
        if (blockPrefabScaleMultipliers == null || blockPrefabScaleMultipliers.Length != voxelBlockPrefabs.Length)
        {
            //Multipliers for how large we want the blocks we drop to be 
            // I never got this working but the idea is still sound 
            blockPrefabScaleMultipliers = new float[voxelBlockPrefabs.Length];
            // Loop through all of our block
            for (int i = 0; i < blockPrefabScaleMultipliers.Length; i++)
            {
                // And  increment their size 
                blockPrefabScaleMultipliers[i] = 1f; 
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
        // Create a cube so we can stretch it 
        currentPlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        currentPlatform.name = "Base_Platform";
        currentPlatform.transform.position = Vector3.zero;
        currentPlatform.transform.localScale = platformDimensions;

        // Set platform material/color
        // Se the material and make it gray 
        MeshRenderer renderer = currentPlatform.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            // make sure it actually exists and that the color actually exists
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

    // Load in all of our UI elements to display the score 
    void CreateUIElements()
    {
        // Create score text (top left with black background)
        if (scoreText == null)
        {
            GameObject scoreObj = new GameObject("ScoreText");
            scoreObj.transform.SetParent(mainCanvas.transform, false);

            // Create background panel for score
            // this is a gray block so it's easier to see 
            Image bgImage = scoreObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f);
            
            // Create our score 
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
            
            // Set our text block 
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
        // This is the menu button and allows the player to go back 
        if (pauseButton == null)
        {
            // Create our pause button 
            GameObject pauseObj = new GameObject("PauseButton");
            pauseObj.transform.SetParent(mainCanvas.transform, false);
            // Add a button image 
            Image btnImg = pauseObj.AddComponent<Image>();
            btnImg.color = Color.white;
            // And add a listener to it 
            pauseButton = pauseObj.AddComponent<Button>();
            pauseButton.onClick.AddListener(PauseAndMenu);

            RectTransform pauseRect = pauseObj.GetComponent<RectTransform>();
            // Move the button to the top center 
            pauseRect.anchorMin = new Vector2(0.5f, 1);
            pauseRect.anchorMax = new Vector2(0.5f, 1);
            pauseRect.pivot = new Vector2(0.5f, 1);
            pauseRect.sizeDelta = new Vector2(120, 40);
            // Move it 10 pixels down from top so it doesn't clip 
            pauseRect.anchoredPosition = new Vector2(0, -10); 

            // Create button text
            GameObject btnText = new GameObject("Text");
            btnText.transform.SetParent(pauseObj.transform, false);

            // Load in our text mesh for the button 
            TextMeshProUGUI buttonText = btnText.AddComponent<TextMeshProUGUI>();
            buttonText.text = "MENU";
            buttonText.fontSize = 20;
            buttonText.color = Color.black;
            buttonText.alignment = TextAlignmentOptions.Center;

            //Move our backround in place 
            RectTransform btnTextRect = btnText.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;
        }
    }

    // Handle the movement of the block dropper,
    // dropping blocks 
    // and the clicking of the Menu buttons 
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

    // Update our block based on where the player moves it 
    void UpdateCurrentBlockPosition()
    {
        // Keep current block at a height relative to camera
        if (currentBlock != null && !currentBlock.GetComponent<BlockController>()?.isDropped == true)
        {   
            // Initalize our camera if it doesn't exist
            if (cameraController != null)
            {
                Vector3 blockPos = currentBlock.transform.position;
                // Keep block 5 units above camera center
                blockPos.y = cameraController.transform.position.y + 5f;
                currentBlock.transform.position = blockPos;
            }
        }
    }

    // Call our block dropper to block 
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

    // Spawn the block we want to drop 
    void SpawnBlock()
    {
        if (voxelBlockPrefabs != null && voxelBlockPrefabs.Length > 0)
        {
            // Select a random block prefab
            // from our array 
            int randomIndex = Random.Range(0, voxelBlockPrefabs.Length);
            GameObject selectedPrefab = voxelBlockPrefabs[randomIndex];

            // Make sure we actually selected a block 
            if (selectedPrefab != null)
            {
                // Spawn the block at the spawn point
                // (Were our block dropper is)
                currentBlock = Instantiate(selectedPrefab, spawnPoint.position, Quaternion.identity);
                currentBlock.name = "VoxelBlock_" + Time.time;

                // Apply scale multiplier for this specific block type
                // Here they are all hard coded to 1 as it didn't work 
                float scaleMultiplier = 1f;
                if (blockPrefabScaleMultipliers != null && randomIndex < blockPrefabScaleMultipliers.Length)
                {
                    scaleMultiplier = blockPrefabScaleMultipliers[randomIndex];
                }

                // Transform it based on the GUI and the predefined 
                currentBlock.transform.localScale = Vector3.one * globalBlockScale * scaleMultiplier;

                // Add and initialize BlockController
                BlockController bc = currentBlock.GetComponent<BlockController>();
                if (bc == null)
                {
                    // Add our block controller 
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
            // Debug when I couldnt place before 
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

    // Check our blocks actually fell 
    void CheckFallenBlocks()
    {
        // Check we have active clocks 
        for (int i = activeBlocks.Count - 1; i >= 0; i--)
        {
            // if they don't exist remove them from the array 
            if (activeBlocks[i] == null)
            {
                activeBlocks.RemoveAt(i);
                continue;
            }
            // If they fell to far remove them 
            if (activeBlocks[i].transform.position.y < despawnHeight)
            {
                Destroy(activeBlocks[i]);
                activeBlocks.RemoveAt(i);
            }
        }
    }

    // Update our max height (If our blocks were properly frozen)
    void UpdateHeight()
    {
        // Track the maximum height reached
        foreach (GameObject block in activeBlocks)
        {
            // Check it's still valid 
            if (block != null)
            {
                // get our block controller 
                BlockController bc = block.GetComponent<BlockController>();
                // Make sure it's not null 
                if (bc != null && bc.isFrozen)
                {
                    // Set our height 
                    float height = block.transform.position.y;
                    if (height > currentMaxHeight)
                    {
                        currentMaxHeight = height;
                    }
                }
            }
        }
    }

    // Update the score in our text box 
    void UpdateScore()
    {
        //  Score based on height (10 points per meter) minus blocks used
        int heightScore = Mathf.RoundToInt(currentMaxHeight * 10);
        currentScore = heightScore - blocksUsed;
    }

    // Update all of our text boxes 
    void UpdateUI()
    {
        // make sure our text isn't null 
        if (scoreText != null)
        {
            // If it isn't then update it 
            scoreText.text = $"Score: {currentScore}\nBlocks: {blocksUsed}";
        }
        
        // If it is spawn in that text box 
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

    // Set pause to true
    public void PauseAndMenu()
    {
        // time to 0
        // and load our menu canvas 
        isPaused = true;
        Time.timeScale = 0f;
        ReturnToMenu();
    }

    // Add a game over pannel to tell them they won !!!
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