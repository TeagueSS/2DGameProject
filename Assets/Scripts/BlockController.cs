using System.Collections;
using UnityEngine;

public class BlockController : MonoBehaviour
{
    [Header("Block Settings")]
    public bool isFrozen = false;
    public bool isDropped = false;
    public float freezeDelay = 3f;
    
    [Header("Components")]
    private Rigidbody rb;
    private Collider blockCollider;
    private GameManager gameManager;
    private SpriteRenderer spriteRenderer; // Changed from MeshRenderer
    
    [Header("Movement Detection")]
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float stillTimer = 0f;
    private float movementThreshold = 0.01f;
    private float rotationThreshold = 0.01f;
    
    [Header("Effects")]
    private Color originalColor;
    private Color frozenColor;
    
    // Weather system reference
    private WeatherSystem weatherSystem;
    
    // Store the block's scale for later use
    private float blockScale = 1f;
    
    void Awake()
    {
        // Get or add required components
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        blockCollider = GetComponent<Collider>();
        if (blockCollider == null)
        {
            // Add box collider if no collider exists
            blockCollider = gameObject.AddComponent<BoxCollider>();
        }
        
        // Get SpriteRenderer instead of MeshRenderer
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }
    
    public void Initialize(GameManager manager, float freezeTime)
    {
        gameManager = manager;
        freezeDelay = freezeTime;
        
        // Get weather system reference
        weatherSystem = FindObjectOfType<WeatherSystem>();
        
        // Apply weather effects to freeze delay
        if (weatherSystem != null)
        {
            freezeDelay *= weatherSystem.GetFreezeDelayMultiplier();
        }
        
        // Check if glue power-up is active
        PowerUpManager powerUpManager = FindObjectOfType<PowerUpManager>();
        if (powerUpManager != null && powerUpManager.IsGlueActive())
        {
            powerUpManager.ApplyGlueToBlock(gameObject);
        }
        
        // Set up rigidbody
        rb.mass = 1f;
        rb.drag = 0.5f;
        rb.angularDrag = 0.5f;
        rb.useGravity = false; // Start with no gravity until dropped
        rb.isKinematic = true; // Start kinematic for grabbing
        
        // CRITICAL: Lock Z-axis and rotations for 2D sprite physics
        rb.constraints = RigidbodyConstraints.FreezePositionZ | 
                        RigidbodyConstraints.FreezeRotationX | 
                        RigidbodyConstraints.FreezeRotationY | 
                        RigidbodyConstraints.FreezeRotationZ;
        
        // Initialize position tracking
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        
        // Set tag for identification
        if (!gameObject.CompareTag("Block"))
        {
            gameObject.tag = "Block";
        }
        
        // Store the block's current scale for later use
        blockScale = transform.localScale.x; // Assuming uniform scale
    }
    
    void Update()
    {
        if (!isFrozen && isDropped)
        {
            CheckMovement();
        }
        
        // Visual feedback for when block is grabbed
        if (!isDropped && spriteRenderer != null)
        {
            // Pulse effect while being held
            float pulse = Mathf.Sin(Time.time * 3f) * 0.1f + 1f;
            transform.localScale = Vector3.one * (blockScale * pulse);
        }
    }
    
    void CheckMovement()
    {
        // Calculate movement delta (only X and Y since Z is locked)
        float positionDelta = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.y),
            new Vector2(lastPosition.x, lastPosition.y)
        );
        
        // Rotation is locked, so we only check velocity
        bool isMoving = positionDelta > movementThreshold ||
                       rb.velocity.magnitude > 0.1f;
        
        if (!isMoving)
        {
            stillTimer += Time.deltaTime;
            
            if (stillTimer >= freezeDelay)
            {
                FreezeBlock();
            }
        }
        else
        {
            stillTimer = 0f;
            lastPosition = transform.position;
        }
    }
    
    public void StartDrop()
    {
        if (!isDropped)
        {
            isDropped = true;
            rb.isKinematic = false;
            rb.useGravity = true;
            
            // Reset scale to normal
            transform.localScale = Vector3.one * blockScale;
            
            // Add a small downward force to ensure it starts falling
            rb.AddForce(Vector3.down * 2f, ForceMode.Impulse);
        }
    }
    
    void FreezeBlock()
    {
        if (!isFrozen)
        {
            isFrozen = true;
            
            // Make the block static
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // Visual feedback for frozen state
            StartCoroutine(FreezeEffect());
            
            // Notify game manager
            if (gameManager != null)
            {
                gameManager.OnBlockFrozen(gameObject);
            }
        }
    }
    
    /// <summary>
    /// Instantly freezes the block (used by Freeze power-up)
    /// </summary>
    public void FreezeInstantly()
    {
        stillTimer = freezeDelay; // Set timer to max to trigger immediate freeze
        FreezeBlock();
    }
    
    IEnumerator FreezeEffect()
    {
        // Create frozen color effect for sprite
        if (spriteRenderer != null)
        {
            // Create a frozen color (cyan tinted)
            frozenColor = Color.Lerp(originalColor, Color.cyan, 0.4f);
            
            // Animate the freeze
            float duration = 0.5f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                spriteRenderer.color = Color.Lerp(originalColor, frozenColor, t);
                
                // Add a scale effect
                float scaleMultiplier = 1f + Mathf.Sin(t * Mathf.PI) * 0.1f;
                transform.localScale = Vector3.one * (blockScale * scaleMultiplier);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            spriteRenderer.color = frozenColor;
            transform.localScale = Vector3.one * blockScale;
        }
    }
    
    public bool IsStable()
    {
        // For 2D sprites that don't rotate, always stable unless falling
        return rb.velocity.magnitude < 0.1f;
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (isDropped && !isFrozen)
        {
            // Play impact sound if you have one
            // AudioManager.Instance.PlayImpact();
            
            // Reset still timer when hitting something
            stillTimer = 0f;
        }
    }
    
    // Public methods for power-ups
    public void ApplyExplosionForce(Vector3 explosionPosition, float force, float radius)
    {
        if (!isFrozen && rb != null)
        {
            // Only apply force in X and Y directions for 2D
            Vector3 direction = transform.position - explosionPosition;
            direction.z = 0; // Remove Z component
            direction.Normalize();
            
            float distance = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(explosionPosition.x, explosionPosition.y)
            );
            
            if (distance < radius)
            {
                float adjustedForce = force * (1 - distance / radius);
                rb.AddForce(direction * adjustedForce, ForceMode.Impulse);
            }
        }
    }
    
    public void ApplyMagneticForce(Vector3 targetPosition, float strength)
    {
        if (!isFrozen && rb != null)
        {
            // Only apply force in X and Y for 2D
            Vector3 direction = targetPosition - transform.position;
            direction.z = 0; // Remove Z component
            direction.Normalize();
            rb.AddForce(direction * strength);
        }
    }
}