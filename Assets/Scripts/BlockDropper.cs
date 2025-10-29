using UnityEngine;

public class BlockDropper : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float smoothTime = 0.1f;
    public Vector2 movementBounds = new Vector2(10f, 10f); // X and Z boundaries
    
    [Header("Visual Settings")]
    public bool showDropPreview = true;
    public LineRenderer dropLine;
    public GameObject previewGhost;
    
    [Header("Components")]
    private GameManager gameManager;
    private Vector3 velocity = Vector3.zero;
    private Vector3 targetPosition;
    
    [Header("Animation")]
    public float bobAmount = 0.2f;
    public float bobSpeed = 2f;
    private float bobTimer = 0f;
    private Vector3 startPosition;
    
    void Awake()
    {
        startPosition = transform.position;
        targetPosition = transform.position;
        
        // Create drop line for preview if it doesn't exist
        if (dropLine == null)
        {
            GameObject lineObj = new GameObject("DropLine");
            lineObj.transform.SetParent(transform);
            dropLine = lineObj.AddComponent<LineRenderer>();
            SetupDropLine();
        }
    }
    
    public void Initialize(GameManager manager)
    {
        gameManager = manager;
    }
    
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
    
    void SetupDropLine()
    {
        if (dropLine != null)
        {
            dropLine.startWidth = 0.05f;
            dropLine.endWidth = 0.05f;
            dropLine.material = new Material(Shader.Find("Sprites/Default"));
            dropLine.startColor = new Color(1f, 1f, 1f, 0.3f);
            dropLine.endColor = new Color(1f, 1f, 1f, 0.1f);
            dropLine.positionCount = 2;
        }
    }
    
    void UpdateDropPreview()
    {
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
    
    public void SetPreviewGhost(GameObject ghost)
    {
        if (previewGhost != null)
        {
            Destroy(previewGhost);
        }
        
        previewGhost = ghost;
        
        if (previewGhost != null)
        {
            // Make it transparent
            MeshRenderer renderer = previewGhost.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
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
        
        Vector3 size = new Vector3(movementBounds.x * 2, 0.1f, movementBounds.y * 2);
        Gizmos.DrawWireCube(center, size);
    }
}