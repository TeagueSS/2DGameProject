using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Import TextMeshPro namespace

/// <summary>
/// Weather types that can occur at different height sections
/// </summary>
public enum WeatherType
{
    Sunny, // Normal conditions
    Rainy, // Darker background, slower block freezing
    Windy // Pushes blocks left or right
}

/// <summary>
/// Manages weather effects that change every 25 height units
/// Creates dynamic weather zones as players build higher
/// UPDATED: Fixed wind application to work with new layer system
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    [Header("Weather Section Settings")]
    [Tooltip("Height interval for weather changes (every X units)")]
    public float sectionHeight = 25f; // Weather changes every 25 height units
    
    [Tooltip("Chance of wind occurring in a section (0-1)")]
    public float windChance = 1.0f; // 20% chance of wind
    
    [Tooltip("Chance of rain occurring in a section (0-1)")]
    public float rainChance = 0.3f; // 30% chance of rain
    // Sunny chance is automatic (remaining percentage)

    [Header("Wind Settings")]
    [Tooltip("Force applied to blocks when wind is active - ADJUSTABLE IN INSPECTOR")]
    [Range(0f, 20f)] // Slider range from 0 to 20
    public float windForce = 5f; // Increased default wind force (was 2f)
    
    [Tooltip("How often wind changes direction")]
    public float windChangeInterval = 5f; // Wind changes direction every 5 seconds
    
    [Tooltip("Visual sway amount for wind effect")]
    public float windSwayAmount = 0.1f;

    [Header("Rain Settings")]
    [Tooltip("How much longer blocks take to freeze in rain (multiplier)")]
    public float rainFreezeDelayMultiplier = 1.5f; // 50% longer freeze time
    
    [Tooltip("How dark the background gets during rain (0-1)")]
    public float rainDarkness = 0.3f; // 30% darker
    
    [Tooltip("Rain particle system prefab")]
    public GameObject rainParticlesPrefab;

    [Header("Background Image (Pixel Art)")]
    [Tooltip("Single background texture for all weather - drag your pixel art here")]
    public Texture2D backgroundTexture;
    
    [Header("Background Settings")]
    [Tooltip("Total image height in pixels (e.g., 5525)")]
    public float imageHeightPixels = 5525f;
    
    [Tooltip("Total image width in pixels (e.g., 550)")]
    public float imageWidthPixels = 550f;
    
    [Tooltip("Width of background in world units")]
    public float backgroundWidth = 25f;
    
    [Tooltip("Number of levels (determines how image is divided)")]
    public int numberOfLevels = 5;
    
    [Tooltip("Height range per level in world units")]
    public float levelHeightRange = 10f;

    [Header("Background Images (Pixel Art)")]
    [Tooltip("Background texture for sunny weather")]
    public Texture2D sunnyBackgroundTexture;
    
    [Tooltip("Background texture for rainy weather")]
    public Texture2D rainyBackgroundTexture;
    
    [Tooltip("Background texture for windy weather")]
    public Texture2D windyBackgroundTexture;
    
    [Header("Background Settings")]
    [Tooltip("Width of each background tile")]
    public float backgroundTileWidth = 20f;
    
    [Tooltip("Height of each background tile")]
    public float backgroundTileHeight = 100f;
    
    [Tooltip("Number of background tiles (recommended: 5)")]
    public int numberOfBackgroundTiles = 5;

    [Header("Audio Settings")]
    [Tooltip("Audio source for weather sounds")]
    public AudioSource weatherAudioSource;
    
    [Tooltip("Sound effect when weather changes")]
    public AudioClip weatherChangeSound;
    
    [Tooltip("Background ambient sound for sunny weather")]
    public AudioClip sunnyAmbient;
    
    [Tooltip("Background ambient sound for rain")]
    public AudioClip rainAmbient;
    
    [Tooltip("Background ambient sound for wind")]
    public AudioClip windAmbient;

    [Header("Visual Effects")]
    [Tooltip("TextMeshPro Text to show current weather")]
    public TextMeshProUGUI weatherText; // Changed to TextMeshProUGUI
    
    [Tooltip("Canvas group for darkening effect")]
    public CanvasGroup darkenOverlay; // Create a dark panel overlay

    [Header("Debug Info")]
    [SerializeField] private WeatherType currentWeather = WeatherType.Sunny;
    [SerializeField] private int currentSection = 0;
    [SerializeField] private float currentSectionStartHeight = 0f;
    [SerializeField] private bool isWindBlowingRight = true;

    // Weather sections dictionary - stores weather for each height section
    private Dictionary<int, WeatherType> weatherSections = new Dictionary<int, WeatherType>();

    // References
    private GameManager gameManager;
    private GameObject currentRainEffect;
    private List<GameObject> backgroundTiles = new List<GameObject>();

    void Start()
    {
        // Get reference to GameManager
        gameManager = FindObjectOfType<GameManager>();

        // Set up audio source if not assigned
        if (weatherAudioSource == null)
        {
            weatherAudioSource = gameObject.AddComponent<AudioSource>();
            weatherAudioSource.loop = true; // Background sounds should loop
            weatherAudioSource.volume = 0.3f; // Ambient volume
        }

        // Setup pixel art background
        SetupBackground();

        // Generate weather for first section (0-25)
        GenerateWeatherForSection(0);
        ApplyWeather(WeatherType.Sunny); // Start with sunny

        // Start wind direction changes
        StartCoroutine(WindDirectionChanger());
    }

    void Update()
    {
        if (gameManager != null)
        {
            // Check current height and update section if needed
            float currentHeight = gameManager.currentMaxHeight;
            int newSection = Mathf.FloorToInt(currentHeight / sectionHeight);

            // Check if we've entered a new weather section
            if (newSection != currentSection)
            {
                currentSection = newSection;
                currentSectionStartHeight = newSection * sectionHeight;

                // Generate weather for this section if it doesn't exist
                if (!weatherSections.ContainsKey(newSection))
                {
                    GenerateWeatherForSection(newSection);
                }

                // Apply the weather for this section
                WeatherType sectionWeather = weatherSections[newSection];
                if (sectionWeather != currentWeather)
                {
                    ChangeWeather(sectionWeather);
                }
            }
        }

        // Apply continuous wind effect if windy
        if (currentWeather == WeatherType.Windy)
        {
            ApplyWindToBlocks();
        }
    }

    /// <summary>
    /// Generates random weather for a specific height section
    /// </summary>
    void GenerateWeatherForSection(int section)
    {
        float random = Random.Range(0f, 1f);
        WeatherType weather;

        // Determine weather based on chances
        if (random < windChance)
        {
            weather = WeatherType.Windy;
            Debug.Log($"Section {section} (height {section * sectionHeight}-{(section + 1) * sectionHeight}): WINDY");
        }
        else if (random < windChance + rainChance)
        {
            weather = WeatherType.Rainy;
            Debug.Log($"Section {section} (height {section * sectionHeight}-{(section + 1) * sectionHeight}): RAINY");
        }
        else
        {
            weather = WeatherType.Sunny;
            Debug.Log($"Section {section} (height {section * sectionHeight}-{(section + 1) * sectionHeight}): SUNNY");
        }

        // Store the weather for this section
        weatherSections[section] = weather;
    }

    /// <summary>
    /// Changes the current weather with effects and sounds
    /// </summary>
    public void ChangeWeather(WeatherType newWeather)
    {
        Debug.Log($"Weather changing from {currentWeather} to {newWeather}");

        // Play weather change sound
        if (weatherChangeSound != null && weatherAudioSource != null)
        {
            weatherAudioSource.PlayOneShot(weatherChangeSound);
        }

        // Apply new weather
        currentWeather = newWeather;
        ApplyWeather(newWeather);

        // Update UI text
        UpdateWeatherUI();
    }

    /// <summary>
    /// Applies weather effects based on type
    /// </summary>
    void ApplyWeather(WeatherType weather)
    {
        switch (weather)
        {
            case WeatherType.Sunny:
                ApplySunnyWeather();
                break;
            case WeatherType.Rainy:
                ApplyRainyWeather();
                break;
            case WeatherType.Windy:
                ApplyWindyWeather();
                break;
        }
    }

    /// <summary>
    /// Applies sunny weather effects
    /// </summary>
    void ApplySunnyWeather()
    {
        // Remove darkness overlay
        if (darkenOverlay != null)
        {
            darkenOverlay.alpha = 0f;
        }

        // Play sunny ambient sound
        if (weatherAudioSource != null && sunnyAmbient != null)
        {
            weatherAudioSource.clip = sunnyAmbient;
            weatherAudioSource.Play();
        }

        // Remove rain particles if any
        if (currentRainEffect != null)
        {
            Destroy(currentRainEffect);
        }

        // Normal freeze times (no modification needed)
    }

    /// <summary>
    /// Applies rainy weather effects
    /// </summary>
    void ApplyRainyWeather()
    {
        // Darken the scene
        if (darkenOverlay != null)
        {
            darkenOverlay.alpha = rainDarkness;
        }

        // Play rain ambient sound
        if (weatherAudioSource != null && rainAmbient != null)
        {
            weatherAudioSource.clip = rainAmbient;
            weatherAudioSource.Play();
        }

        // Create rain particle effect
        if (rainParticlesPrefab != null && currentRainEffect == null)
        {
            currentRainEffect = Instantiate(rainParticlesPrefab);
            // Position it above the camera
            if (Camera.main != null)
            {
                currentRainEffect.transform.position = Camera.main.transform.position + Vector3.up * 10f;
                currentRainEffect.transform.SetParent(Camera.main.transform);
            }
        }

        // Blocks will check for rain when calculating freeze time
    }

    /// <summary>
    /// Applies windy weather effects
    /// </summary>
    void ApplyWindyWeather()
    {
        // Slight darkness for wind
        if (darkenOverlay != null)
        {
            darkenOverlay.alpha = 0.1f; // Slight darkening for wind
        }

        // Play wind ambient sound
        if (weatherAudioSource != null && windAmbient != null)
        {
            weatherAudioSource.clip = windAmbient;
            weatherAudioSource.Play();
        }

        // Remove rain particles if any
        if (currentRainEffect != null)
        {
            Destroy(currentRainEffect);
        }
    }

    /// <summary>
    /// Applies wind force to all active non-frozen blocks
    /// FIXED: Now works with the layer system and finds all blocks properly
    /// </summary>
    void ApplyWindToBlocks()
    {
        if (gameManager == null) return;

        // Apply wind to all blocks from GameManager's active blocks list
        // This is more reliable than using tags
        List<GameObject> blocksToAffect = new List<GameObject>();

        // Get all GameObjects with Rigidbody components (blocks)
        Rigidbody[] allRigidbodies = FindObjectsOfType<Rigidbody>();
        
        foreach (Rigidbody rb in allRigidbodies)
        {
            // Check if this object has a BlockController
            BlockController controller = rb.GetComponent<BlockController>();
            if (controller != null)
            {
                // Only apply wind to non-frozen, dropped blocks
                if (controller.isDropped && !controller.isFrozen)
                {
                    ApplyWindToBlock(rb.gameObject);
                }
            }
        }
    }

    /// <summary>
    /// Applies wind force to a specific block
    /// FIXED: Now works regardless of kinematic state
    /// </summary>
    void ApplyWindToBlock(GameObject block)
    {
        Rigidbody rb = block.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) // Only apply to non-kinematic (dropped) blocks
        {
            // Apply constant horizontal force (stronger now with adjustable windForce)
            Vector3 windDirection = isWindBlowingRight ? Vector3.right : Vector3.left;
            rb.AddForce(windDirection * windForce, ForceMode.Force);
            
            // Add slight sway for visual effect
            float sway = Mathf.Sin(Time.time * 2f) * windSwayAmount;
            rb.AddForce(Vector3.forward * sway, ForceMode.Force);
        }
    }

    /// <summary>
    /// Coroutine that changes wind direction periodically
    /// </summary>
    IEnumerator WindDirectionChanger()
    {
        while (true)
        {
            yield return new WaitForSeconds(windChangeInterval);

            // Only change direction if currently windy
            if (currentWeather == WeatherType.Windy)
            {
                isWindBlowingRight = !isWindBlowingRight;
                Debug.Log($"Wind direction changed: blowing {(isWindBlowingRight ? "RIGHT" : "LEFT")}");

                // Update UI to show wind direction
                UpdateWeatherUI();
            }
        }
    }

    /// <summary>
    /// Updates the weather UI text
    /// </summary>
    void UpdateWeatherUI()
    {
        if (weatherText != null)
        {
            string heightRange = $"Height {currentSection * sectionHeight}-{(currentSection + 1) * sectionHeight}m: ";
            switch (currentWeather)
            {
                case WeatherType.Sunny:
                    weatherText.text = heightRange + "â˜€ï¸ SUNNY";
                    weatherText.color = Color.yellow;
                    break;
                case WeatherType.Rainy:
                    weatherText.text = heightRange + "ðŸŒ§ï¸ RAINY (Slower freezing)";
                    weatherText.color = Color.blue;
                    break;
                case WeatherType.Windy:
                    string arrow = isWindBlowingRight ? "â†’" : "â†";
                    weatherText.text = heightRange + $"ðŸ’¨ WINDY {arrow} (Force: {windForce:F1})";
                    weatherText.color = Color.cyan;
                    break;
            }
        }
    }

    /// <summary>
    /// Gets the current weather type
    /// </summary>
    public WeatherType GetCurrentWeather()
    {
        return currentWeather;
    }

    /// <summary>
    /// Gets the freeze delay multiplier for current weather
    /// </summary>
    public float GetFreezeDelayMultiplier()
    {
        if (currentWeather == WeatherType.Rainy)
        {
            return rainFreezeDelayMultiplier;
        }
        return 1f; // Normal freeze time
    }

    /// <summary>
    /// Checks if wind is currently active
    /// </summary>
    public bool IsWindActive()
    {
        return currentWeather == WeatherType.Windy;
    }

    /// <summary>
    /// Gets the current wind direction
    /// </summary>
    public Vector3 GetWindDirection()
    {
        if (currentWeather == WeatherType.Windy)
        {
            return isWindBlowingRight ? Vector3.right : Vector3.left;
        }
        return Vector3.zero;
    }

    /// <summary>
    /// Preview weather for debugging - shows all generated sections
    /// </summary>
    [ContextMenu("Preview All Weather Sections")]
    void PreviewAllWeatherSections()
    {
        string preview = "Weather Preview:\n";
        for (int i = 0; i <= 10; i++) // Preview first 10 sections
        {
            if (!weatherSections.ContainsKey(i))
            {
                GenerateWeatherForSection(i);
            }
            preview += $"Section {i} ({i * sectionHeight}m-{(i + 1) * sectionHeight}m): {weatherSections[i]}\n";
        }
        Debug.Log(preview);
    }

    /// <summary>
    /// Sets up the pixel art background as static sections
    /// Each section shows a portion of the tall background image
    /// </summary>
    void SetupBackground()
    {
        // Clean up old background tiles
        foreach (GameObject tile in backgroundTiles)
        {
            if (tile != null) Destroy(tile);
        }
        backgroundTiles.Clear();

        if (backgroundTexture == null)
        {
            Debug.LogWarning("No background texture assigned to WeatherSystem!");
            return;
        }

        // Fix texture import settings for pixel art
        backgroundTexture.filterMode = FilterMode.Point;
        backgroundTexture.wrapMode = TextureWrapMode.Clamp;

        // Calculate height per level section in pixels
        float pixelsPerLevel = imageHeightPixels / numberOfLevels;
        
        // Calculate aspect ratio for proper scaling
        float imageAspectRatio = imageWidthPixels / imageHeightPixels;
        
        // Create background sections for each level + 1 extra for safety
        // We'll create overlapping sections so camera never sees empty space
        for (int level = 0; level < numberOfLevels; level++)
        {
            // Create section showing this level's portion + next level's portion
            CreateBackgroundSection(level, pixelsPerLevel, imageAspectRatio);
        }
        
        Debug.Log($"Background setup complete! Created {backgroundTiles.Count} static sections (3 horizontal copies x {numberOfLevels} levels).");
    }

    /// <summary>
    /// Creates a background section for a specific level
    /// </summary>
    void CreateBackgroundSection(int levelIndex, float pixelsPerLevel, float imageAspectRatio)
    {
        // Calculate world position for this section
        // Each level starts at levelIndex * levelHeightRange
        float startHeight = levelIndex * levelHeightRange;
        
        // Each section covers 2 levels worth of height (current + next for safety)
        float sectionWorldHeight = levelHeightRange * 2f;
        float centerHeight = startHeight + (sectionWorldHeight / 2f);
        
        // Calculate UV coordinates for this section
        float uvStart = (float)levelIndex / numberOfLevels;
        float uvEnd = Mathf.Min((float)(levelIndex + 2) / numberOfLevels, 1f);
        float uvPortionShown = uvEnd - uvStart; // How much of the image vertically
        
        // Calculate proper width based on image aspect ratio to prevent squishing
        // sectionWorldHeight represents uvPortionShown of the full image height
        // So full image height would be: sectionWorldHeight / uvPortionShown
        float fullImageWorldHeight = sectionWorldHeight / uvPortionShown;
        float sectionWorldWidth = fullImageWorldHeight * imageAspectRatio;
        
        // Create 3 copies: left, center, right for horizontal repetition
        for (int xOffset = -3; xOffset <= 3; xOffset++)
        {
            GameObject bgSection = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgSection.name = $"BackgroundSection_Level{levelIndex + 1}_{(xOffset == -1 ? "Left" : xOffset == 0 ? "Center" : "Right")}";
            
            // Position section in world space (STATIC - doesn't move)
            float xPosition = xOffset * sectionWorldWidth;
            bgSection.transform.position = new Vector3(xPosition, centerHeight, 10);
            bgSection.transform.localScale = new Vector3(sectionWorldWidth, sectionWorldHeight, 1);
            
            // Remove collider
            Collider col = bgSection.GetComponent<Collider>();
            if (col != null) Destroy(col);
            
            // Setup material and UV mapping
            MeshRenderer renderer = bgSection.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.mainTexture = backgroundTexture;
            mat.mainTexture.filterMode = FilterMode.Point;
            
            // Apply UV mapping to show only this section
            SetupUVMapping(bgSection, uvStart, uvEnd);
            
            renderer.material = mat;
            renderer.sortingOrder = -100; // Render behind everything
            
            backgroundTiles.Add(bgSection);
        }
    }

    /// <summary>
    /// Sets up UV coordinates to show a specific vertical section of the texture
    /// </summary>
    void SetupUVMapping(GameObject quad, float uvYStart, float uvYEnd)
    {
        MeshFilter meshFilter = quad.GetComponent<MeshFilter>();
        if (meshFilter == null) return;
        
        Mesh mesh = meshFilter.mesh;
        Vector2[] uvs = new Vector2[4];
        
        // UV coordinates for a quad showing a vertical slice
        // Bottom-left
        uvs[0] = new Vector2(0, uvYStart);
        // Bottom-right
        uvs[1] = new Vector2(1, uvYStart);
        // Top-left
        uvs[2] = new Vector2(0, uvYEnd);
        // Top-right  
        uvs[3] = new Vector2(1, uvYEnd);
        
        mesh.uv = uvs;
    }

    /// <summary>
    /// Updates background position to follow camera (NO LONGER NEEDED - backgrounds are static)
    /// Keeping method for backward compatibility but it does nothing
    /// </summary>
    public void UpdateBackgroundPosition(float cameraY)
    {
        // Backgrounds are now static in world space
        // This method kept for compatibility but does nothing
    }

    /// <summary>
    /// Force a specific weather type for testing
    /// </summary>
    [ContextMenu("Force Windy Weather")]
    public void ForceWindyWeather()
    {
        ChangeWeather(WeatherType.Windy);
        Debug.Log("Forced windy weather for testing!");
    }
}