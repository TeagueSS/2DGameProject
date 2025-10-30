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
        // Start with no gravity until dropped
        rb.useGravity = false; 
        // Start kinematic for grabbing
        rb.isKinematic = true;

        // Lock Z-axis and rotations for 2D sprite physics
        // (this is how I make 3d look 2D)
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
        // Assuming uniform scale
        blockScale = transform.localScale.x; 
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

        // This allows us to freeze it 
        // ORGIINAlLY used in the 3d version
        // no longer applicable 
        if (!isMoving)
        {
            // Here we update the timer as needed 
            stillTimer += Time.deltaTime;
            // And call freeze if we're supposed to 
            if (stillTimer >= freezeDelay)
            {
                FreezeBlock();
            }
        }
        else
        {
            // otherwise just update our time to check again 
            stillTimer = 0f;
            lastPosition = transform.position;
        }
    }
    
    // Drop block method 
    public void StartDrop()
    {
        // Check if our block has already been dropped 
        if (!isDropped)
        {
            // Mark all of our values so it falls properly
            // I'm hard coding these so they always work regardelss of what 
            // the sprite has attached to it 
            isDropped = true;
            rb.isKinematic = false;
            rb.useGravity = true;

            // Reset scale to normal
            transform.localScale = Vector3.one * blockScale;

            // Add a small downward force to ensure it starts falling
            rb.AddForce(Vector3.down * 2f, ForceMode.Impulse);
        }
    }
    
    // Freeze our block power up. No longer used 
    void FreezeBlock()
    {
        // Make sure it isn't alredy frozen before we update 
        if (!isFrozen)
        {
            // if not then update our bool 
            isFrozen = true;

            // And upate our values so it stays in place 
            // This un does what the method above it does 
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
        // Set timer to max to trigger immediate freeze
        stillTimer = freezeDelay; 
        FreezeBlock();
    }

    // Freeze effect for power ups 
    // no longer used as the game changed 
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
            
            // Check if we are still suppoed to be freezign 
            while (elapsed < duration)
            {
                // if yes then update our time 
                float t = elapsed / duration;
                spriteRenderer.color = Color.Lerp(originalColor, frozenColor, t);

                //Our object scale 
                float scaleMultiplier = 1f + Mathf.Sin(t * Mathf.PI) * 0.1f;
                transform.localScale = Vector3.one * (blockScale * scaleMultiplier);
                // And then increment our time 
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            spriteRenderer.color = frozenColor;
            transform.localScale = Vector3.one * blockScale;
        }
    }
    
    // Is Stable to check sprite positon 
   
    
    // Public methods for power-ups
    
    
    
}