using UnityEngine;
using UnityEngine.Splines;

// We use this namespace to access the new Input System
using UnityEngine.InputSystem; 

public class TrackCameraController : MonoBehaviour
{
    private Vector3 targetCenterPosition = Vector3.zero;
    
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

        // set the center the camera will focus on to the center of the bounds of the spline
        Bounds splineBounds = SplineUtility.GetBounds(splineContainer.Spline);
        targetCenterPosition = splineBounds.center;

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
        bool shouldUpdatePosition = false;

        if (!isInitialized || targetCenterPosition == null) return;

        if (Mouse.current != null)
        {
            if (Mouse.current.rightButton.isPressed)
            {
                // read the movement of the mouse from last frame
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();

                // update the yaw and pitch
                currentX += mouseDelta.x * rotationSpeed;
                currentY -= mouseDelta.y * rotationSpeed;

                currentY = Mathf.Clamp(currentY, minPitch, maxPitch);
                shouldUpdatePosition = true;
            }

            float speedMultiplier = 1.0f;
            float scrollY = Mouse.current.scroll.ReadValue().y;

            // normalize scrollY because different mice return different scroll values
            if (Mathf.Abs(scrollY) > 0)
            {
                // increase scroll speed if shift is held
                if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
                {
                    speedMultiplier = 3.0f;
                }
                float scrollDirection = Mathf.Sign(scrollY); 
                // clamping the distance to prevent zooming in too close or far
                currentDistance = Mathf.Clamp(currentDistance - (scrollDirection * scrollSpeed * speedMultiplier), 5f, defaultDistance * 1.5f);
                shouldUpdatePosition = true;
            }
        }
        // only run the update if the camera was moved to reduce redundancy
        if (shouldUpdatePosition){UpdatePosition();}
    }

    // moves the camera to be the given rotation and direction away from the targeted center
    // then sets the camera to look at the targeted center
    private void UpdatePosition()
    {
        // spherical coordinates conversion
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 direction = new Vector3(0, 0, -currentDistance);
        
        transform.position = targetCenterPosition + (rotation * direction);

        // unitys built in method to make camera look at given point
        transform.LookAt(targetCenterPosition);
    }
}