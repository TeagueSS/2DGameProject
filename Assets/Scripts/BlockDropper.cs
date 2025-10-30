using UnityEngine;


// Our actual block dropper!!!
// Built the same way as apple picker 
public class BlockDropper : MonoBehaviour
{
    // All of our posiiton and movement settings
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float smoothTime = 0.1f;
    // Limiting to X and Z as with y it would be too easy (They could move down)
    public Vector2 movementBounds = new Vector2(10f, 10f);

    [Header("Visual Settings")]
    // Wether or not we want it to show a drop line,
    // I like it for making it easier to see wehre things should land 
    public bool showDropPreview = true;
    public LineRenderer dropLine;
    public GameObject previewGhost;

    [Header("Components")]
    //All of the variables we make external to the game manager 
    private GameManager gameManager;
    private Vector3 velocity = Vector3.zero;
    private Vector3 targetPosition;

    [Header("Animation")]
    public float bobAmount = 0.2f;
    public float bobSpeed = 2f;
    private float bobTimer = 0f;
    // Where we want our block dropper to start for each level.
    private Vector3 startPosition;

    // Spawn in our block dropper 
    void Awake()
    {
        // Set all of our positions 
        startPosition = transform.position;
        targetPosition = transform.position;

        // Create drop line for preview if it doesn't exist
        if (dropLine == null)
        {
            // And create our line if it's enabled 
            GameObject lineObj = new GameObject("DropLine");
            lineObj.transform.SetParent(transform);
            dropLine = lineObj.AddComponent<LineRenderer>();
            SetupDropLine();
        }
    }

    // Spawn in our game manager 
    public void Initialize(GameManager manager)
    {
        gameManager = manager;
    }

    // Update method, updates our positon,
    // where we want to go, and what blocks we're holding
    void Update()
    {
        // Bobbing animation
        bobTimer += Time.deltaTime * bobSpeed;
        float bobOffset = Mathf.Sin(bobTimer) * bobAmount;

        // Update position with bobbing
        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            smoothTime
        );
        // Where we want to get to 
        smoothedPosition.y = startPosition.y + bobOffset;
        transform.position = smoothedPosition;

        // Update drop preview
        if (showDropPreview)
        {
            UpdateDropPreview();
        }

        // Rotate slowly for visual effect
        transform.Rotate(Vector3.up, 10f * Time.deltaTime);
    }

    // Move our block dropper 
    // this was more meant for animations between levels
    // but this never ended up working
    public void Move(float horizontal, float vertical, float speed)
    {
        // Calculate new position
        Vector3 movement = new Vector3(horizontal, 0, vertical) * speed * Time.deltaTime;
        Vector3 newPosition = targetPosition + movement;

        // Apply boundaries
        newPosition.x = Mathf.Clamp(newPosition.x, -movementBounds.x, movementBounds.x);
        newPosition.z = Mathf.Clamp(newPosition.z, -movementBounds.y, movementBounds.y);
        newPosition.y = startPosition.y; // Keep Y position constant

        targetPosition = newPosition;
    }

    // Load our drop line in 
    void SetupDropLine()
    {
        if (dropLine != null)
        {
            // And create a line falling from our block dropper 
            dropLine.startWidth = 0.05f;
            dropLine.endWidth = 0.05f;
            dropLine.material = new Material(Shader.Find("Sprites/Default"));
            dropLine.startColor = new Color(1f, 1f, 1f, 0.3f);
            dropLine.endColor = new Color(1f, 1f, 1f, 0.1f);
            dropLine.positionCount = 2;
        }
    }

    // Update our line as we move 
    void UpdateDropPreview()
    {
        // Only render if our line is enabled 
        if (dropLine != null)
        {
            // Cast ray down to show where block will land
            RaycastHit hit;
            Vector3 startPos = transform.position - Vector3.up * 0.5f;

            if (Physics.Raycast(startPos, Vector3.down, out hit, 100f))
            {
                dropLine.SetPosition(0, startPos);
                dropLine.SetPosition(1, hit.point);

                // Update preview ghost if exists
                if (previewGhost != null)
                {
                    previewGhost.transform.position = hit.point + Vector3.up * 0.25f;
                }
            }
            else
            {
                // Draw line to bottom of play area
                dropLine.SetPosition(0, startPos);
                dropLine.SetPosition(1, startPos + Vector3.down * 20f);
            }
        }
    }

    // Our preview of the blocks we are going to drop 
    // no longer used, i had this when the game was 3d 
    public void SetPreviewGhost(GameObject ghost)
    {
        // Destory our ghost 
        if (previewGhost != null)
        {
            Destroy(previewGhost);
        }
        // Spawn in a new one 
        previewGhost = ghost;

        // Then set it to be transparent and able to drop blocks 
        if (previewGhost != null)
        {
            // Make it transparent
            MeshRenderer renderer = previewGhost.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                // All of the materials for our dropper 
                Material ghostMat = new Material(renderer.material);
                Color color = ghostMat.color;
                color.a = 0.3f;
                ghostMat.color = color;
                renderer.material = ghostMat;
            }

            // Disable physics
            Rigidbody rb = previewGhost.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            // Collider if we want it to hit stuff 
            // again not used in the 2D version 
            Collider col = previewGhost.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw movement boundaries in editor
        Gizmos.color = Color.yellow;
        Vector3 center = Application.isPlaying ? targetPosition : transform.position;
        center.y = 0;
        // Spawn in our cube 
        Vector3 size = new Vector3(movementBounds.x * 2, 0.1f, movementBounds.y * 2);
        Gizmos.DrawWireCube(center, size);
    }
}