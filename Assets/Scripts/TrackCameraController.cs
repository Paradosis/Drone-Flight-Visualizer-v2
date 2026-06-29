using UnityEngine;
using UnityEngine.Splines;

// We use this namespace to access the new Input System
using UnityEngine.InputSystem; 

public class TrackCameraController : MonoBehaviour
{
    private Transform targetCenterTransform;
    
    [Header("Orbit Settings")]
    [SerializeField] private float rotationSpeed = 0.2f;
    [SerializeField] private float scrollSpeed = 0.5f;
    [SerializeField] private float defaultDistance = 25f;
    [SerializeField] private float minPitch = 10f;  
    [SerializeField] private float maxPitch = 85f;  

    private float currentX = 0.0f; 
    private float currentY = 45.0f; 
    private float currentDistance;

    private bool isInitialized = false;

    // sets up the cameras default position
    public void InitializeTarget(SplineContainer splineContainer, float paddingMultiplier)
    {
        if (targetCenterTransform == null)
        {
            GameObject dummy = new GameObject("CameraTargetCenter");
            targetCenterTransform = dummy.transform;
        }

        // set the center the camera will focus on to the center of the bounds of the spline
        Bounds splineBounds = SplineUtility.GetBounds(splineContainer.Spline);
        targetCenterTransform.position = splineBounds.center;

        float objectSize = Mathf.Max(splineBounds.size.x, splineBounds.size.z);
        float cameraFOV = GetComponent<Camera>().fieldOfView;

        // dividing camFOV by 2 and object size by 2 gets us the angle and opposite side of a right triangle
        // tangent of the camFOV/2 would get us opposite/adjacent
        // this is equivalent to (objectSize/2)/distance
        // so (objectSize/2)/Tan(camFOV/2) gets us the distance from the camera to the center target
        // all of this ensures that the inital camera distance can fully see the entire spline path
        defaultDistance = (objectSize / 2f) / Mathf.Tan(cameraFOV * 0.5f * Mathf.Deg2Rad);
        defaultDistance *= paddingMultiplier;
        
        currentDistance = defaultDistance;

        currentX = 0f;
        currentY = 45f;

        isInitialized = true;
        UpdatePosition();
    }

    void LateUpdate()
    {
        bool moved = false;
        bool zoomed = false;

        if (!isInitialized || targetCenterTransform == null) return;

        // check if the rmb is currently held down
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            // read the movement of the mouse from last frame
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            // update the yaw and pitch
            currentX += mouseDelta.x * rotationSpeed;
            currentY -= mouseDelta.y * rotationSpeed;

            currentY = Mathf.Clamp(currentY, minPitch, maxPitch);
            moved = true;
        }

        // read the scroll wheel vector for zooming
        if (Mouse.current != null)
        {
            float shiftMult = 1.0f;
            float scrollY = Mouse.current.scroll.ReadValue().y;

            // increase scroll speed if shift is held
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
            {
                shiftMult = 3.0f;
            }
            
            // normalize scrollY because different mice return different scroll values
            if (Mathf.Abs(scrollY) > 0)
            {
                float scrollDirection = Mathf.Sign(scrollY); 
                // clamping the distance to prevent zooming in too close or far
                currentDistance = Mathf.Clamp(currentDistance - (scrollDirection * scrollSpeed * shiftMult * Time.deltaTime), 5f, defaultDistance * 1.5f);
                zoomed = true;
            }
        }
        // only run the update if the camera was moved to reduce redundancy
        if (moved || zoomed){UpdatePosition();}
    }

    // moves the camera to be the given rotation and direction away from the targeted center
    // then sets the camera to look at the targeted center
    private void UpdatePosition()
    {
        // spherical coordinates conversion
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 direction = new Vector3(0, 0, -currentDistance);
        
        transform.position = targetCenterTransform.position + (rotation * direction);

        // unitys built in method to make camera look at given point
        transform.LookAt(targetCenterTransform.position);
    }
}