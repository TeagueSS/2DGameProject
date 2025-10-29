using UnityEngine;
using System.Collections;

/// <summary>
/// Boss system for Level 5
/// Enemy AI that throws blocks at the player's tower to knock it down
/// </summary>
public class BossSystem : MonoBehaviour
{
    [Header("Boss Settings")]
    public GameObject bossSprite;
    public Sprite bossImage;
    public Vector3 bossPosition = new Vector3(12f, 20f, 0f);
    
    [Header("Attack Settings")]
    public GameObject throwableBlockPrefab;
    public float throwInterval = 3f;
    public float throwForce = 10f;
    public float throwArcHeight = 2f;
    
    [Header("Targeting")]
    public float targetHeightOffset = 5f; // Aim slightly above tower
    public float accuracy = 0.8f; // 0-1, how accurate the boss is
    
    private GameManager gameManager;
    private float nextThrowTime;
    private SpriteRenderer bossRenderer;
    
    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        
        // Create boss visual
        CreateBoss();
        
        nextThrowTime = Time.time + throwInterval;
    }
    
    void CreateBoss()
    {
        if (bossSprite == null)
        {
            bossSprite = new GameObject("Boss");
            bossSprite.transform.position = bossPosition;
            
            bossRenderer = bossSprite.AddComponent<SpriteRenderer>();
            bossRenderer.sprite = bossImage;
            bossRenderer.sortingOrder = 10;
            
            // Scale boss
            bossSprite.transform.localScale = Vector3.one * 3f;
        }
    }
    
    void Update()
    {
        if (gameManager != null && !gameManager.isGameOver)
        {
            // Update boss position to follow camera height
            UpdateBossPosition();
            
            // Throw blocks at intervals
            if (Time.time >= nextThrowTime)
            {
                ThrowBlockAtTower();
                nextThrowTime = Time.time + throwInterval;
            }
        }
    }
    
    void UpdateBossPosition()
    {
        // Keep boss at side of screen, following camera height
        CameraController cam = FindObjectOfType<CameraController>();
        if (cam != null)
        {
            Vector3 newPos = bossPosition;
            newPos.y = cam.transform.position.y + 5f; // Slightly above camera center
            bossSprite.transform.position = newPos;
        }
    }
    
    void ThrowBlockAtTower()
    {
        if (gameManager == null) return;
        
        // Find target (highest block or random block)
        GameObject targetBlock = FindTargetBlock();
        
        Vector3 targetPos;
        if (targetBlock != null)
        {
            targetPos = targetBlock.transform.position;
            // Add some inaccuracy
            targetPos.x += Random.Range(-accuracy * 2f, accuracy * 2f);
            targetPos.y += Random.Range(-accuracy, accuracy);
        }
        else
        {
            // Aim at tower center
            targetPos = new Vector3(0, gameManager.currentMaxHeight + targetHeightOffset, 0);
        }
        
        // Create projectile
        GameObject projectile = CreateProjectile();
        projectile.transform.position = bossSprite.transform.position;
        
        // Calculate throw velocity
        Vector3 throwVelocity = CalculateThrowVelocity(
            bossSprite.transform.position,
            targetPos
        );
        
        // Apply velocity
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = throwVelocity;
            rb.angularVelocity = Random.insideUnitSphere * 5f; // Spin
        }
        
        // Animate boss throw
        StartCoroutine(BossThrowAnimation());
        
        // Destroy projectile after time
        Destroy(projectile, 10f);
    }
    
    GameObject CreateProjectile()
    {
        GameObject projectile;
        
        if (throwableBlockPrefab != null)
        {
            projectile = Instantiate(throwableBlockPrefab);
        }
        else
        {
            // Create simple cube projectile
            projectile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            projectile.transform.localScale = Vector3.one * 0.5f;
            projectile.GetComponent<Renderer>().material.color = Color.red;
        }
        
        // Add rigidbody if not present
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = projectile.AddComponent<Rigidbody>();
        }
        rb.mass = 1f;
        rb.useGravity = true;
        
        // Tag as enemy projectile
        projectile.tag = "EnemyProjectile";
        
        return projectile;
    }
    
    Vector3 CalculateThrowVelocity(Vector3 origin, Vector3 target)
    {
        // Physics calculation for projectile arc
        Vector3 direction = target - origin;
        float horizontalDist = new Vector2(direction.x, direction.z).magnitude;
        float verticalDist = direction.y;
        
        // Calculate required velocity for arc
        float gravity = Mathf.Abs(Physics.gravity.y);
        float angle = 45f * Mathf.Deg2Rad; // 45 degree angle
        
        float velocity = Mathf.Sqrt((horizontalDist * gravity) / Mathf.Sin(2 * angle));
        
        Vector3 velocityVector = direction.normalized * velocity;
        velocityVector.y = velocity * Mathf.Sin(angle);
        
        return velocityVector;
    }
    
    GameObject FindTargetBlock()
    {
        if (gameManager == null) return null;
        
        // Find highest frozen block
        float maxHeight = 0f;
        GameObject highestBlock = null;
        
        BlockController[] allBlocks = FindObjectsOfType<BlockController>();
        foreach (BlockController bc in allBlocks)
        {
            if (bc.isFrozen && bc.transform.position.y > maxHeight)
            {
                maxHeight = bc.transform.position.y;
                highestBlock = bc.gameObject;
            }
        }
        
        return highestBlock;
    }
    
    IEnumerator BossThrowAnimation()
    {
        if (bossRenderer == null) yield break;
        
        // Quick shake/flash animation
        Vector3 originalScale = bossSprite.transform.localScale;
        
        for (int i = 0; i < 3; i++)
        {
            bossSprite.transform.localScale = originalScale * 1.1f;
            yield return new WaitForSeconds(0.05f);
            bossSprite.transform.localScale = originalScale;
            yield return new WaitForSeconds(0.05f);
        }
    }
}