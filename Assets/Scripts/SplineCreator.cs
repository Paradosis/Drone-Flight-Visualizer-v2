using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

using Random = UnityEngine.Random;

public class SplineCreator : MonoBehaviour
{
    private List<Vector3> splinePoints = new List<Vector3>();
    private List<Quaternion> splineRotations = new List<Quaternion>();
    
    private SplineContainer splineContainer;
    private GameObject followerDrone;
    private TrailRenderer droneTrail;

    public Color trackColor { get; private set; } = Color.white;

    [SerializeField] private bool extrudeSpline = true;
    [SerializeField] private bool displayNodes = true;

    [SerializeField] private bool closedTrack = false;
    private float totalLapTimeDuration = 0f;

    // Unpacks raw position and rotation frames from JSON file
    public void InitializeAndBuild(DroneData droneData)
    {
        splinePoints.Clear();
        splineRotations.Clear();

        totalLapTimeDuration = (float)droneData.Data[0].LapTime / 200f;

        trackColor = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);

        foreach (LapData currentLap in droneData.Data)
        {
            foreach (TelemetryFrame currentFrame in currentLap.Data)
            {
                splinePoints.Add(currentFrame.DronePosition.ToVector3());
                splineRotations.Add(currentFrame.DroneRotation.ToQuaternion());
            }
        }
        
        // checks to see if drone has data at time 0
        if (droneData.Data[0].Data[0].TimeFrame != 0f && splinePoints.Count >= 2)
        {
            Debug.Log("Drone is missing data at time 0"); // this is not a bad thing
            // adds a generated starting point at time 0 since the data starts at 1 second
            // this is done by mirroring the second data point over the first
            Vector3 firstPos = splinePoints[0];
            Vector3 secondPos = splinePoints[1];
            
            // Formula: NewStart = First - (Second - First) -> 2 * First - Second
            Vector3 mirroredStartPos = (2f * firstPos) - secondPos;

            Quaternion firstRot = splineRotations[0];
            Quaternion secondRot = splineRotations[1];
            
            // For rotations, invert the relative rotation difference and apply it backwards
            Quaternion relativeRotation = Quaternion.Inverse(firstRot) * secondRot;
            Quaternion mirroredStartRot = firstRot * Quaternion.Inverse(relativeRotation);

            // Insert new points at beginning of lists to create a "time 0" point
            splinePoints.Insert(0, mirroredStartPos);
            splineRotations.Insert(0, mirroredStartRot);
        }


        CreateSpline();
        CreateDrone();
    }

    // Constructs the spline path
    private void CreateSpline()
    {
        GameObject splineObj = new GameObject("ProceduralSpline");
        splineObj.transform.SetParent(this.transform);

        splineContainer = splineObj.AddComponent<SplineContainer>();
        Spline spline = splineContainer.Spline;

        // spline.Closed determines if the first and last nodes should be joined together to make a full loop
        spline.Closed = closedTrack;

        foreach (Vector3 pos in splinePoints)
        {
            spline.Add(new BezierKnot(pos), TangentMode.AutoSmooth);
        }

        // creates a sphere at each node to help visualize the control points of the spline
        if(displayNodes){
            for (int i = 0; i < spline.Count; i++)
            {
                GameObject nodeVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                nodeVisual.name = $"NodeVisual_{i}";
                nodeVisual.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                
                Vector3 worldPosition = splineObj.transform.TransformPoint(spline[i].Position);
                nodeVisual.transform.position = worldPosition;
                nodeVisual.transform.SetParent(splineObj.transform);
                
                Destroy(nodeVisual.GetComponent<Collider>());
            }
        }
        
        // creates a gray outline of the entire spline
        if (extrudeSpline)
        {
            SplineExtrude extruder = splineObj.AddComponent<SplineExtrude>();
            extruder.Container = splineContainer;
            extruder.Radius = 0.02f;
            extruder.Sides = 8;
            extruder.SegmentsPerUnit = 10;
            extruder.Rebuild();
            
            MeshRenderer meshRenderer = splineObj.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
                meshRenderer.material.color = Color.gray;
            }
        }
    }

    // Configures the visual mesh cube and handles continuous trail settings
    private void CreateDrone()
    {
        followerDrone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        followerDrone.name = "FollowerDrone";
        followerDrone.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        followerDrone.transform.SetParent(this.transform);

        MeshRenderer cubeRenderer = followerDrone.GetComponent<MeshRenderer>();
        if (cubeRenderer != null)
        {
            cubeRenderer.material = new Material(Shader.Find("Sprites/Default"))
            {
                color = trackColor
            };
        }

        // positioning the drone before rendering the trail to prevent artifact of trail showing the drone teleporting to starting positon
        if (splineContainer != null && splinePoints.Count > 0)
        {
            Vector3 localPos = splineContainer.EvaluatePosition(0f);
            followerDrone.transform.position = splineContainer.transform.TransformPoint(localPos);
            if (splineRotations.Count > 0) followerDrone.transform.rotation = splineRotations[0];
        }

        // trail settings
        droneTrail = followerDrone.AddComponent<TrailRenderer>();
        droneTrail.time = Mathf.Infinity;
        droneTrail.startWidth = 0.25f;
        droneTrail.endWidth = 0.25f;
        droneTrail.material = new Material(Shader.Find("Sprites/Default"));
        
        Gradient gradient = new Gradient();
        
        // sets the trail to have constant width
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(trackColor, 0.0f), new GradientColorKey(trackColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
        );
        droneTrail.colorGradient = gradient;

        EvaluatePositionAtTime(0f);
    }

    void Update()
    {
        // checks to ensure the drone and spline both are instantiated
        if (followerDrone != null && splineContainer != null && splinePoints.Count > 0)
        {
            EvaluatePositionAtTime(DroneMenuManager.GlobalTime);
        }
    }

    // Samples and updates the position and rotation targets along the spline based on the provided time
    private void EvaluatePositionAtTime(float globalTime)
    {
        // Wipe trails immediately if global timer reset occurred
        if (globalTime == 0f && droneTrail != null)
        {
            droneTrail.Clear();
        }

        int knotCount = splineContainer.Spline.Count;

        // if track is open, there is one less segment than there is nodes
        int totalSegments = splineContainer.Spline.Closed ? knotCount : knotCount - 1;
        
        // assumes each segment is 1 second long, which it is given the current json data
        float totalTrackDuration = totalSegments * 1.0f;
    
        float loopTime = globalTime % totalTrackDuration;
        
        // stop movement if track is not closed/looping
        if (globalTime > totalTrackDuration && !splineContainer.Spline.Closed)
        {
            return;
        }

        // Calculate segment steps (assuming each node connection takes exactly 1.0 seconds)
        int currentSegmentIndex = Mathf.FloorToInt(loopTime / 1.0f);

        // the t-value of the current segment of the spline, where t=0 is exactly node 1 and t=1 is node 2
        float segmentProgress = (loopTime % 1.0f) / 1.0f;

        // takes the fraction of the current segment compared to total segments
        // then lerps to find the more specific fraction to represent progress through current segment
        float startNormalized = (float)currentSegmentIndex / totalSegments;
        float endNormalized = (float)(currentSegmentIndex + 1) / totalSegments;
        float globalNormalizedTime = Mathf.Lerp(startNormalized, endNormalized, segmentProgress);

        // built in method in splines package to determine position of spline given t-value which is calculated above
        Vector3 localPos = splineContainer.EvaluatePosition(globalNormalizedTime);
        followerDrone.transform.position = splineContainer.transform.TransformPoint(localPos);


        Quaternion startRotation = splineRotations[currentSegmentIndex];
        Quaternion endRotation = (splineContainer.Spline.Closed && currentSegmentIndex == totalSegments - 1) 
            ? splineRotations[0] 
            : splineRotations[currentSegmentIndex + 1];

        followerDrone.transform.rotation = Quaternion.Slerp(startRotation, endRotation, segmentProgress);

        DrawHeading();
        DrawVelocity(globalNormalizedTime);
    }

    private void DrawHeading()
    {
        // calculate the starting point of the arrow (the center of the drone)
        Vector3 arrowStart = followerDrone.transform.position;

        // define the direction out of the negative Y axis
        // looking at raw data that seems to be the main heading direction
        float arrowLength = 2.0f;
        Vector3 arrowDirection = -followerDrone.transform.up * arrowLength;

        Debug.DrawRay(arrowStart, arrowDirection, Color.cyan);

        // drawing arrow head
        // create two tiny lines splitting off the tip of the arrow bending back toward the drone
        Vector3 arrowTip = arrowStart + arrowDirection;

        // because the forward heading is along negative Y, transform.up will have the arrow tip pointing backwards
        Vector3 rightSideHead = (followerDrone.transform.forward + followerDrone.transform.up) * 0.3f;
        Vector3 leftSideHead = (-followerDrone.transform.forward + followerDrone.transform.up) * 0.3f;

        Debug.DrawRay(arrowTip, rightSideHead, Color.cyan);
        Debug.DrawRay(arrowTip, leftSideHead, Color.cyan);
    }
    
    public void DrawVelocity(float t)
    {
        float lengthMult = 5.0f;
        Vector3 localPos = splineContainer.EvaluatePosition(t);
        // rudimentary derivative
        Vector3 updPos = splineContainer.EvaluatePosition(t + 0.001f);
        Vector3 velocity = (updPos - localPos) * lengthMult;

        // calculate the starting point of the arrow (the center of the drone)
        Vector3 arrowStart = followerDrone.transform.position;

        Debug.DrawRay(arrowStart, velocity, Color.red);

        Vector3 arrowTip = arrowStart + velocity;

        // get the direction the velocity is pointing and invert it (pointing back towards start)
        Vector3 backwardDirection = -velocity.normalized;

        // find a "right" vector perpendicular to our velocity direction
        // use Vector3.up as a temporary hint to find a reliable cross-product
        // this is done to prevent a possible error if the world up or forward direction is perfectly in line with the velocity
        Vector3 crossHint = Mathf.Abs(Vector3.Dot(backwardDirection, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
        Vector3 rightDirection = Vector3.Cross(backwardDirection, crossHint).normalized;

        // scale the fins based on the length of the velocity vector
        // using a fraction (like 15%) of the total velocity length keeps it proportional
        float arrowHeadLength = velocity.magnitude * 0.15f; 

        // combine backward and outward directions, then scale
        // angling them 45 degrees back (equal parts backward and right/left)
        Vector3 rightSideHead = (backwardDirection + rightDirection).normalized * arrowHeadLength;
        Vector3 leftSideHead = (backwardDirection - rightDirection).normalized * arrowHeadLength;

        Debug.DrawRay(arrowTip, rightSideHead, Color.red);
        Debug.DrawRay(arrowTip, leftSideHead, Color.red);
    }
    public float GetTotalDuration(){return totalLapTimeDuration;}
}