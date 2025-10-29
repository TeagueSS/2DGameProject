using System.Collections;
using UnityEngine;

/// <summary>
/// SnowDropper system for Level 4 - SPRITE VERSION
/// Spawns small snow sprites that fall from above and are affected by wind
/// Creates a challenging obstacle where snow accumulates and can knock down the tower
/// </summary>
public class SnowDropper : MonoBehaviour
{
    [Header("Snow Settings")]
    [Tooltip("How often snow blocks drop (in seconds)")]
    public float dropInterval = 2f;
    
    [Tooltip("Size of snow sprites in world units")]
    public Vector2 snowSpriteSize = new Vector2(0.4f, 0.4f);
    
    [Tooltip("Mass of snow blocks")]
    public float snowBlockMass = 0.2f;
    
    [Tooltip("How long snow blocks last before being destroyed")]
    public float snowLifetime = 20f;
    
    [Header("Sprite Settings")]
    [Tooltip("Sprite to use for snow blocks")]
    public Sprite snowSprite;
    
    [Tooltip("Collider depth for snow blocks")]
    public float snowColliderDepth = 0.15f;
    
    [Header("Spawn Area")]
    [Tooltip("Radius around tower where snow can spawn")]
    public float spawnRadius = 8f;
    
    [Tooltip("Height above tower where snow spawns")]
    public float spawnHeightOffset = 15f;
    
    [Tooltip("Minimum height above camera to spawn snow")]
    public float minSpawnHeightAboveCamera = 10f;
    
    [Header("Wind Integration")]
    [Tooltip("How much wind affects snow blocks (multiplier)")]
    public float windSensitivity = 2f;
    
    [Header("Visual Settings")]
    public Color snowColor = new Color(0.95f, 0.95f, 1f); // Slightly blue-white
    
    [Header("Particle Effects")]
    [Tooltip("Particle effect when snow block hits something")]
    public GameObject snowImpactEffect;
    
    [Header("Difficulty Scaling")]
    [Tooltip("How much faster snow drops as tower gets higher")]
    public float dropIntervalScaling = 0.95f; // Multiplier per 10m height
    
    // References
    private GameManager gameManager;
    private WeatherSystem weatherSystem;
    private float nextDropTime;
    private int snowBlockCount = 0;
    private Camera mainCamera;
    
    void Start()
    {
        mainCamera = Camera.main;
        nextDropTime = Time.time + dropInterval;
    }
    
    public void Initialize(GameManager manager)
    {
        gameManager = manager;
        weatherSystem = FindObjectOfType<WeatherSystem>();
        
        Debug.Log("SnowDropper initialized - Sprite snow blocks will fall from above!");
    }
    
    void Update()
    {
        if (gameManager == null || gameManager.isGameOver) return;
        
        // Check if it's time to drop more snow
        if (Time.time >= nextDropTime)
        {
            DropSnowBlock();
            
            // Calculate next drop time with difficulty scaling
            float scaledInterval = dropInterval * Mathf.Pow(dropIntervalScaling, gameManager.currentMaxHeight / 10f);
            scaledInterval = Mathf.Max(scaledInterval, 0.5f); // Minimum 0.5 second interval
            nextDropTime = Time.time + scaledInterval;
        }
    }
    
    /// <summary>
    /// Drops a single snow sprite from above the tower
    /// </summary>
    void DropSnowBlock()
    {
        // Calculate spawn position
        Vector3 spawnPosition = CalculateSpawnPosition();
        
        // Create snow block as sprite
        GameObject snowBlock = CreateSnowSprite(spawnPosition);
        
        // Apply initial velocity (slight downward boost)
        Rigidbody rb = snowBlock.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.down * 2f; // Start falling
            
            // Add random horizontal velocity (X only, since Z is locked)
            float randomX = Random.Range(-1f, 1f);
            rb.velocity += new Vector3(randomX, 0f, 0f);
        }
        
        // Destroy after lifetime
        Destroy(snowBlock, snowLifetime);
        
        snowBlockCount++;
    }
    
    /// <summary>
    /// Calculates a random spawn position above the tower
    /// </summary>
    Vector3 CalculateSpawnPosition()
    {
        // Spawn at current tower height + offset
        float spawnHeight = gameManager.currentMaxHeight + spawnHeightOffset;
        
        // Ensure spawn is above camera view
        if (mainCamera != null)
        {
            float minHeight = mainCamera.transform.position.y + minSpawnHeightAboveCamera;
            spawnHeight = Mathf.Max(spawnHeight, minHeight);
        }
        
        // Random X position within spawn radius (Z is locked at 0 for 2D)
        float randomX = Random.Range(-spawnRadius, spawnRadius);
        
        return new Vector3(randomX, spawnHeight, 0f); // Z = 0 for 2D
    }
    
    /// <summary>
    /// Creates a snow sprite GameObject with physics
    /// </summary>
    GameObject CreateSnowSprite(Vector3 position)
    {
        // Create empty GameObject
        GameObject snowBlock = new GameObject($"SnowSprite_{snowBlockCount}");
        snowBlock.transform.position = position;
        
        // Add SpriteRenderer
        SpriteRenderer spriteRenderer = snowBlock.AddComponent<SpriteRenderer>();
        
        if (snowSprite != null)
        {
            spriteRenderer.sprite = snowSprite;
        }
        else
        {
            // Create a simple white square sprite if none provided
            spriteRenderer.sprite = CreateSimpleSnowSprite();
        }
        
        spriteRenderer.color = snowColor;
        spriteRenderer.sortingOrder = 1;
        
        // Scale to desired size
        snowBlock.transform.localScale = new Vector3(snowSpriteSize.x, snowSpriteSize.y, 1f);
        
        // Add BoxCollider (thin for 2D physics)
        BoxCollider boxCollider = snowBlock.AddComponent<BoxCollider>();
        boxCollider.size = new Vector3(1f, 1f, snowColliderDepth);
        
        // Add Rigidbody with constraints
        Rigidbody rb = snowBlock.AddComponent<Rigidbody>();
        rb.mass = snowBlockMass;
        rb.useGravity = true;
        rb.drag = 0.1f; // Some air resistance
        rb.angularDrag = 0.5f;
        
        // CRITICAL: Lock Z position and all rotations for 2D physics
        rb.constraints = RigidbodyConstraints.FreezePositionZ | 
                        RigidbodyConstraints.FreezeRotationX | 
                        RigidbodyConstraints.FreezeRotationY | 
                        RigidbodyConstraints.FreezeRotationZ;
        
        // Add snow block component for wind interaction
        SnowBlock snowComponent = snowBlock.AddComponent<SnowBlock>();
        snowComponent.Initialize(this, weatherSystem);
        
        // Set tag for identification
        snowBlock.tag = "Snow";
        
        return snowBlock;
    }
    
    /// <summary>
    /// Creates a simple white square sprite if no sprite is assigned
    /// </summary>
    Sprite CreateSimpleSnowSprite()
    {
        // Create a 16x16 white texture
        Texture2D texture = new Texture2D(16, 16);
        Color[] pixels = new Color[16 * 16];
        
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        
        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point; // Pixel art style
        texture.Apply();
        
        // Create sprite from texture
        return Sprite.Create(
            texture,
            new Rect(0, 0, 16, 16),
            new Vector2(0.5f, 0.5f),
            16f
        );
    }
    
    /// <summary>
    /// Gets the current difficulty multiplier based on tower height
    /// </summary>
    public float GetDifficultyMultiplier()
    {
        if (gameManager == null) return 1f;
        
        // Difficulty increases by 10% every 10 meters
        return 1f + (gameManager.currentMaxHeight / 100f);
    }
}

/// <summary>
/// Component attached to each snow sprite for wind interaction and collision effects
/// </summary>
public class SnowBlock : MonoBehaviour
{
    private SnowDropper snowDropper;
    private WeatherSystem weatherSystem;
    private Rigidbody rb;
    private bool hasHitSomething = false;
    
    public void Initialize(SnowDropper dropper, WeatherSystem weather)
    {
        snowDropper = dropper;
        weatherSystem = weather;
        rb = GetComponent<Rigidbody>();
    }
    
    void FixedUpdate()
    {
        // Apply wind force if weather system exists and wind is active
        if (weatherSystem != null && weatherSystem.IsWindActive() && rb != null)
        {
            Vector3 windDirection = weatherSystem.GetWindDirection();
            
            // Only apply wind in X direction for 2D (remove Z component)
            windDirection.z = 0;
            windDirection.Normalize();
            
            float windForce = weatherSystem.windForce * snowDropper.windSensitivity;
            
            // Apply wind force (only affects X movement)
            rb.AddForce(windDirection * windForce, ForceMode.Force);
            
            // Add some turbulence in X direction only
            float turbulence = Mathf.Sin(Time.time * 3f + transform.position.x) * 0.5f;
            rb.AddForce(Vector3.right * turbulence, ForceMode.Force);
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (hasHitSomething) return;
        
        hasHitSomething = true;
        
        // Create impact effect
        if (snowDropper != null && snowDropper.snowImpactEffect != null)
        {
            ContactPoint contact = collision.contacts[0];
            GameObject effect = Instantiate(
                snowDropper.snowImpactEffect,
                contact.point,
                Quaternion.LookRotation(contact.normal)
            );
            Destroy(effect, 2f);
        }
        
        // Reduce bounce on collision
        if (rb != null)
        {
            rb.velocity *= 0.5f;
        }
        
        // Play sound effect if available
        // AudioManager.Instance.PlaySnowImpact();
        
        // Visual feedback - make it slightly transparent after impact
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            Color color = renderer.color;
            color.a = 0.7f;
            renderer.color = color;
        }
    }
    
    void OnCollisionStay(Collision collision)
    {
        // Add friction when resting on surfaces
        if (rb != null && rb.velocity.magnitude < 0.5f)
        {
            rb.drag = 2f; // Increase drag when nearly stopped
        }
    }
}