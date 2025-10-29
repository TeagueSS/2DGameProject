using UnityEngine;
using System.Collections;

/// <summary>
/// Boss system for Level 5
/// Enemy AI that throws blocks at the player's tower to knock it down
/// </summary>
public class BossSystem : MonoBehaviour
{
    // Define all of ur boss system levels 
    [Header("Boss Settings")]
    public GameObject bossSprite;
    // Our boss image to upload 
    public Sprite bossImage;
    // Our boss position 
    public Vector3 bossPosition = new Vector3(12f, 20f, 0f);

    // And all of our attack settings!!!!
    [Header("Attack Settings")]
    // Our throwable square they can hit us with 
    public GameObject throwableBlockPrefab;
    // Our throw interval (I set this to 3 as balances pretty well )
    public float throwInterval = 3f;
    // How hard they throw to hit our tower 
    public float throwForce = 30f;
    // and the arc shape we want
    // here I do .5 so they aim bellow and the arc keeps alot of it's momentum 
    public float throwArcHeight = .5f;


    // Targeting section so we can see wehre we aiik 
    [Header("Targeting")]
    // Aim slightly above tower
    public float targetHeightOffset = -10f;
    // 0-1, how accurate the boss is 
    public float accuracy = 1; 
    // Create a instance of our game manager
    private GameManager gameManager;
    // and a float to hold the next time we throw! 
    private float nextThrowTime;
    // Our renderer for our boss sprite 
    // i never got around to making this animate but the sprite is still there. 
    private SpriteRenderer bossRenderer;
    
    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        
        // Create boss visual
        CreateBoss();
        
        nextThrowTime = Time.time + throwInterval;
    }

    // load our boss in on scene 
    // we have this so we can add it and remove it if the user goes to the menu 
    void CreateBoss()
    {
        // Doubel check they actually don't  exist 
        if (bossSprite == null)
        {
            // if they don't then make them 
            bossSprite = new GameObject("Boss");
            bossSprite.transform.position = bossPosition;

            bossRenderer = bossSprite.AddComponent<SpriteRenderer>();
            bossRenderer.sprite = bossImage;
            bossRenderer.sortingOrder = 10;

            // Scale boss
            bossSprite.transform.localScale = Vector3.one * 3f;
        }
    }

    // Update allows us to have the boss match the camera
    // (So if we wanted to have it on different levels)
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

    // update the positon during the scene 
    // same as the previous method but without throwing 
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
    
    // Our throwing method! 
    void ThrowBlockAtTower()
    {
        // if we can't find our gameManager exit 
        if (gameManager == null) return;
        
        // Find our target block 
        GameObject targetBlock = FindTargetBlock();
        
        // and our target positon 
        Vector3 targetPos;
        // make sure it isn't null before actually thorwing 
        if (targetBlock != null)
        {
            // here we throw based on our accuracy 
            targetPos = targetBlock.transform.position;
            // Add we have some extra innacuracy 
            targetPos.x += Random.Range(-accuracy * 2f, accuracy * 2f);
            targetPos.y += Random.Range(-accuracy, accuracy);
        }
        else
        {
            // Aim at tower center
            targetPos = new Vector3(0, gameManager.currentMaxHeight + targetHeightOffset, 0);
        }
        
        // Create projectile at the boss's position 
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

    // Where we want to throw 
    // always aim for what they recently froze 
    // we can assume the blocks above this will be the weakest 
    GameObject FindTargetBlock()
    {
        if (gameManager == null) return null;
        
        // Find highest frozen block
        float maxHeight = 0f;
        GameObject highestBlock = null;
        
        // Loop through all of them to search 
        BlockController[] allBlocks = FindObjectsOfType<BlockController>();
        // And throw 
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

    // Thow animation doesn't actually do anything I ran out of time 
    // but the idea is still there. 
    IEnumerator BossThrowAnimation()
    {
        if (bossRenderer == null) yield break;
        
        // Quick shake/flash animation
        Vector3 originalScale = bossSprite.transform.localScale;
        
        // Loop 3 times for the animation! 
        for (int i = 0; i < 3; i++)
        {
            // And make it move 
            bossSprite.transform.localScale = originalScale * 1.1f;
            yield return new WaitForSeconds(0.05f);
            bossSprite.transform.localScale = originalScale;
            yield return new WaitForSeconds(0.05f);
        }
    }
}