using System.Collections;
using UnityEngine;

// Power-up types available in the game
public enum PowerUpType
{
    Freeze,  // Instantly freezes the current block in place
    Glue     // Makes the next block stick better to the surface it lands on
}

public class PowerUpManager : MonoBehaviour
{
    [Header("Power-Up Settings")]
    public GameObject[] powerUpPrefabs; // Array for Freeze and Glue prefabs (assign in inspector)
    public float spawnInterval = 20f; // Time between power-up spawns
    public float powerUpLifetime = 15f; // How long power-ups stay before disappearing
    public float spawnRadius = 8f; // Radius from center where power-ups can spawn
    public float spawnHeight = 5f; // Height at which power-ups spawn
    
    [Header("Power-Up Effects")]
    public GameObject powerUpEffectPrefab; // Particle effect when collected
    public AudioClip freezeSound; // Sound for freeze power-up
    public AudioClip glueSound; // Sound for glue power-up
    
    [Header("Glue Settings")]
    public float glueStickinessMultiplier = 3f; // How much more friction glued blocks have
    public float glueDuration = 10f; // How long glue effect lasts
    
    // References
    private GameManager gameManager;
    private float nextSpawnTime;
    private bool isGlueActive = false;
    
    void Start()
    {
        // Get reference to GameManager
        gameManager = GetComponent<GameManager>();
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }
        
        // Set first spawn time
        nextSpawnTime = Time.time + spawnInterval;
    }
    
    void Update()
    {
        // Check if it's time to spawn a new power-up
        if (Time.time >= nextSpawnTime && !gameManager.isGameOver)
        {
            SpawnRandomPowerUp();
            nextSpawnTime = Time.time + spawnInterval;
        }
    }
    
    /// <summary>
    /// Spawns a random power-up (Freeze or Glue) at a random position
    /// </summary>
    void SpawnRandomPowerUp()
    {
        // Randomly choose between Freeze (0) and Glue (1)
        PowerUpType type = (PowerUpType)Random.Range(0, 2);
        
        // Calculate random spawn position within radius (X only for 2D, Z=0)
        Vector3 spawnPosition = new Vector3(
            Random.Range(-spawnRadius, spawnRadius),
            spawnHeight,
            0f // Z locked at 0 for 2D
        );
        
        // Create the power-up object
        GameObject powerUp = CreatePowerUpObject(type, spawnPosition);
        
        // Destroy after lifetime expires
        Destroy(powerUp, powerUpLifetime);
    }
    
    /// <summary>
    /// Creates a power-up GameObject with appropriate visuals and components
    /// </summary>
    GameObject CreatePowerUpObject(PowerUpType type, Vector3 position)
    {
        GameObject powerUp;
        
        // Use prefab if available, otherwise create sprite
        if (powerUpPrefabs != null && powerUpPrefabs.Length > (int)type && powerUpPrefabs[(int)type] != null)
        {
            powerUp = Instantiate(powerUpPrefabs[(int)type], position, Quaternion.identity);
        }
        else
        {
            // Create a sprite-based power-up
            powerUp = new GameObject($"PowerUp_{type}");
            powerUp.transform.position = position;
            
            // Add SpriteRenderer
            SpriteRenderer spriteRenderer = powerUp.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreatePowerUpSprite();
            spriteRenderer.sortingOrder = 10;
            
            // Color based on type
            switch (type)
            {
                case PowerUpType.Freeze:
                    spriteRenderer.color = Color.cyan; // Ice blue for freeze
                    break;
                case PowerUpType.Glue:
                    spriteRenderer.color = Color.yellow; // Yellow for glue
                    break;
            }
            
            // Scale
            powerUp.transform.localScale = Vector3.one * 0.5f;
            
            // Add collider (thin for 2D)
            BoxCollider collider = powerUp.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 1f, 0.2f);
            collider.isTrigger = true;
        }
        
        // Add power-up component
        PowerUpItem item = powerUp.AddComponent<PowerUpItem>();
        item.Initialize(type, gameManager, this);
        
        // Add floating animation for visual appeal
        powerUp.AddComponent<FloatingAnimation>();
        
        return powerUp;
    }
    
    /// <summary>
    /// Creates a simple sprite for power-ups
    /// </summary>
    Sprite CreatePowerUpSprite()
    {
        Texture2D texture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        
        // Create a star/diamond shape
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float distFromCenter = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                if (distFromCenter < 12f)
                {
                    pixels[y * 32 + x] = Color.white;
                }
                else
                {
                    pixels[y * 32 + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point; // Pixel art style
        texture.Apply();
        
        return Sprite.Create(
            texture,
            new Rect(0, 0, 32, 32),
            new Vector2(0.5f, 0.5f),
            32f
        );
    }
    
    /// <summary>
    /// Called when a power-up is collected by a block
    /// </summary>
    public void OnPowerUpCollected(PowerUpType type)
    {
        // Play appropriate sound effect
        AudioSource audio = GetComponent<AudioSource>();
        if (audio == null)
        {
            audio = gameObject.AddComponent<AudioSource>();
        }
        
        // Activate power-up based on type
        switch (type)
        {
            case PowerUpType.Freeze:
                ActivateFreezePowerUp(audio);
                break;
            case PowerUpType.Glue:
                ActivateGluePowerUp(audio);
                break;
        }
    }
    
    /// <summary>
    /// Activates the Freeze power-up - instantly freezes the current block
    /// </summary>
    void ActivateFreezePowerUp(AudioSource audio)
    {
        // Play freeze sound
        if (freezeSound != null)
        {
            audio.PlayOneShot(freezeSound);
        }
        
        // Freeze the current block immediately if it exists
        if (gameManager.currentBlock != null)
        {
            BlockController blockController = gameManager.currentBlock.GetComponent<BlockController>();
            if (blockController != null && !blockController.isFrozen)
            {
                blockController.FreezeInstantly();
                gameManager.powerUpText.text = "FREEZE! Block locked in place!";
                StartCoroutine(ClearText(2f));
            }
        }
        else
        {
            gameManager.powerUpText.text = "No block to freeze!";
            StartCoroutine(ClearText(1.5f));
        }
    }
    
    /// <summary>
    /// Activates the Glue power-up - makes next blocks stick better
    /// </summary>
    void ActivateGluePowerUp(AudioSource audio)
    {
        // Play glue sound
        if (glueSound != null)
        {
            audio.PlayOneShot(glueSound);
        }
        
        // Activate glue effect
        if (!isGlueActive)
        {
            StartCoroutine(GlueEffectCoroutine());
        }
        else
        {
            // Extend glue duration if already active
            gameManager.powerUpText.text = "GLUE extended!";
            StartCoroutine(ClearText(1.5f));
        }
    }
    
    /// <summary>
    /// Coroutine that handles the glue effect duration
    /// </summary>
    IEnumerator GlueEffectCoroutine()
    {
        isGlueActive = true;
        gameManager.powerUpText.text = "GLUE ACTIVE! Blocks stick better!";
        
        // Apply glue to current block if it exists
        if (gameManager.currentBlock != null)
        {
            ApplyGlueToBlock(gameManager.currentBlock);
        }
        
        yield return new WaitForSeconds(2f);
        gameManager.powerUpText.text = "";
        
        // Wait for duration
        yield return new WaitForSeconds(glueDuration - 2f);
        
        isGlueActive = false;
    }
    
    /// <summary>
    /// Applies glue effect to a block (increases friction)
    /// </summary>
    public void ApplyGlueToBlock(GameObject block)
    {
        if (!isGlueActive) return;
        
        Rigidbody rb = block.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Increase friction to make it stick better
            rb.drag *= glueStickinessMultiplier;
            rb.angularDrag *= glueStickinessMultiplier;
            
            // Visual feedback - make sprite slightly yellow tinted
            SpriteRenderer renderer = block.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = block.GetComponentInChildren<SpriteRenderer>();
            }
            
            if (renderer != null)
            {
                renderer.color = Color.Lerp(renderer.color, Color.yellow, 0.2f);
            }
        }
    }
    
    /// <summary>
    /// Check if glue is currently active
    /// </summary>
    public bool IsGlueActive()
    {
        return isGlueActive;
    }
    
    /// <summary>
    /// Clears the power-up text after a delay
    /// </summary>
    IEnumerator ClearText(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!isGlueActive) // Don't clear if glue is still active
        {
            gameManager.powerUpText.text = "";
        }
    }
}

/// <summary>
/// Component attached to each power-up item in the game
/// </summary>
public class PowerUpItem : MonoBehaviour
{
    public PowerUpType powerUpType;
    private GameManager gameManager;
    private PowerUpManager powerUpManager;
    
    /// <summary>
    /// Initialize the power-up item
    /// </summary>
    public void Initialize(PowerUpType type, GameManager gm, PowerUpManager pm)
    {
        powerUpType = type;
        gameManager = gm;
        powerUpManager = pm;
        
        // Make it a trigger collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        
        // Add tag for identification
        gameObject.tag = "PowerUp";
    }
    
    /// <summary>
    /// Called when something enters the trigger
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        // Check if hit by a block
        if (other.CompareTag("Block") || other.name.Contains("Vox"))
        {
            // Activate power-up
            powerUpManager.OnPowerUpCollected(powerUpType);
            
            // Create collection effect if prefab exists
            if (powerUpManager.powerUpEffectPrefab != null)
            {
                Instantiate(powerUpManager.powerUpEffectPrefab, transform.position, Quaternion.identity);
            }
            
            // Destroy this power-up
            Destroy(gameObject);
        }
    }
}

/// <summary>
/// Simple floating animation for power-ups to make them more noticeable
/// </summary>
public class FloatingAnimation : MonoBehaviour
{
    public float bobSpeed = 2f; // Speed of up/down movement
    public float bobAmount = 0.5f; // Amount of up/down movement
    public float rotationSpeed = 50f; // Speed of rotation (disabled for sprites facing camera)
    
    private Vector3 startPosition;
    private float timer;
    
    void Start()
    {
        startPosition = transform.position;
    }
    
    void Update()
    {
        // Bob up and down using sine wave
        timer += Time.deltaTime;
        float newY = startPosition.y + Mathf.Sin(timer * bobSpeed) * bobAmount;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        
        // For sprites, we don't rotate in 3D space
        // Instead, could add a pulsing scale effect
        float scaleMultiplier = 1f + Mathf.Sin(timer * 3f) * 0.1f;
        transform.localScale = Vector3.one * 0.5f * scaleMultiplier;
    }
}