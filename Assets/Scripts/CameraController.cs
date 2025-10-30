using UnityEngine;


// Meant to move our camera into the right positon on game start -> 
public class CameraController : MonoBehaviour
{
    // The base platform or focus point
    [Header("Camera Settings")] public Transform target; 
    // Set camera to orthographic (2D)
    public bool isOrthographic = true; 
    // Size of orthographic camera
    public float orthographicSize = 10f;

    [Header("Position Settings")]
    // Default camera position
    public Vector3 defaultPosition = new Vector3(0, 10, 0); 
    // Default Z distance from tower

    public float defaultZ = -15f; 
    [Header("Dynamic Following")]
    public bool followHeight = true;
    // How much above the highest block to position camera
    public float heightOffset = 10f; 
    public float smoothSpeed = 5f;
    public float minY = 5f;
    public float maxY = 100f;

    [Header("Shake Settings")]
    public float shakeAmount = 0.1f;
    public float shakeDuration = 0.2f;
    private float currentShakeTime = 0f;
    // Pointers to our game manager, our camera, and our target height 

    private GameManager gameManager;
    private Camera cam;
    private float targetHeight;
    // Velocity was for loading in animations to 
    // scroll up the tower, but it never ended up working.
    private Vector3 currentVelocity;

    // Load our camera into our scene 
    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        cam = GetComponent<Camera>();

        if (cam == null)
        {
            cam = gameObject.AddComponent<Camera>();
        }

        // Set camera to orthographic for 2D gameplay
        cam.orthographic = isOrthographic;
        cam.orthographicSize = orthographicSize;

        // Set initial position
        if (target == null)
        {
            GameObject platform = GameObject.Find("Base_Platform");
            if (platform != null)
            {
                target = platform.transform;
            }
        }

        // Initialize to default position
        transform.position = new Vector3(defaultPosition.x, defaultPosition.y, defaultZ);
        targetHeight = minY;
    }

    void LateUpdate()
    {
        if (target == null) return;

        HandleCameraMovement();
        HandleCameraShake();
    }

    void HandleCameraMovement()
    {
        // Calculate target position based on highest block height
        if (followHeight && gameManager != null)
        {
            float maxBlockHeight = gameManager.currentMaxHeight;
            targetHeight = Mathf.Lerp(targetHeight, maxBlockHeight + heightOffset, smoothSpeed * Time.deltaTime);
            targetHeight = Mathf.Clamp(targetHeight, minY, maxY);
        }

        // Calculate desired position - centered on tower with fixed Z
        Vector3 desiredPosition = new Vector3(
            target.position.x + defaultPosition.x,
            targetHeight,
            target.position.z + defaultZ
        );

        // Smooth movement
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, 1f / smoothSpeed);

        // Look at the center of the tower at current height
        Vector3 lookTarget = new Vector3(target.position.x, targetHeight - heightOffset * 0.5f, target.position.z);
        transform.LookAt(lookTarget);
    }

    void HandleCameraShake()
    {
        if (currentShakeTime > 0)
        {
            Vector3 shakeOffset = Random.insideUnitSphere * shakeAmount;
            shakeOffset.z = 0; // No Z shake for 2D game
            transform.position += shakeOffset;
            currentShakeTime -= Time.deltaTime;
        }
    }

    // Shake our camera, never got this working sadly 
    public void TriggerShake(float duration = -1f, float amount = -1f)
    {
        // Move our camera around for our duration 
        if (duration > 0) shakeDuration = duration;
        if (amount > 0) shakeAmount = amount;
        currentShakeTime = shakeDuration;
    }

    // Set where we want our camera to smoothy move to 
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void ResetCamera()
    {
        targetHeight = minY;
        transform.position = new Vector3(defaultPosition.x, defaultPosition.y, defaultZ);
    }

    /// <summary>
    /// Reset camera to a specific height (used when starting at elevated positions)
    /// </summary>
    public void ResetCameraToHeight(float startHeight)
    {
        targetHeight = startHeight + heightOffset;
        transform.position = new Vector3(defaultPosition.x, startHeight + heightOffset, defaultZ);

        // Update minY if start height is higher
        if (startHeight > minY)
        {
            minY = startHeight;
        }
    }
}