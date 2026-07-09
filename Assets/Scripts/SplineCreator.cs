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

    [SerializeField] private bool extrudeSpline = false;
    [SerializeField] private bool displayNodes = true;

    [SerializeField] private bool closedTrack = false;
    private float totalLapTimeDuration = 0f;

    public void InitializeAndBuild(DroneData droneData)
    {
        splinePoints.Clear();
        splineRotations.Clear();

        totalLapTimeDuration = droneData.Laps[0].LapTime;

        trackColor = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);

        foreach (LapRuntimeData currentLap in droneData.Laps)
        {
            foreach (TelemetryRuntimeFrame currentFrame in currentLap.Telemetry)
            {
                splinePoints.Add(currentFrame.DronePosition);
                splineRotations.Add(currentFrame.DroneRotation);
            }
        }
        
        if (droneData.Laps[0].Telemetry[0].TimeFrame != 0f && splinePoints.Count >= 2)
        {
            Debug.Log("Drone is missing data at time 0"); // this is not a bad thing
            // adds a generated starting point at time 0 since the data starts at 1 second
            // this is done by mirroring the second data point over the first to approximate the time 0 starting point
            Vector3 firstPos = splinePoints[0];
            Vector3 secondPos = splinePoints[1];
            
            // Formula: NewStart = First - (Second - First) -> 2 * First - Second
            Vector3 mirroredStartPos = (2f * firstPos) - secondPos;

            Quaternion firstRot = splineRotations[0];
            Quaternion secondRot = splineRotations[1];
            
            // for rotations, invert the relative rotation difference and apply it backwards
            Quaternion relativeRotation = Quaternion.Inverse(firstRot) * secondRot;
            Quaternion mirroredStartRot = firstRot * Quaternion.Inverse(relativeRotation);

            splinePoints.Insert(0, mirroredStartPos);
            splineRotations.Insert(0, mirroredStartRot);
        }


        CreateSpline();
        CreateDrone();
    }

    private void CreateSpline()
    {
        GameObject splineObj = new GameObject("ProceduralSpline");
        splineObj.transform.SetParent(this.transform);

        splineContainer = splineObj.AddComponent<SplineContainer>();
        Spline spline = splineContainer.Spline;

        spline.Closed = closedTrack;

        foreach (Vector3 pos in splinePoints)
        {
            spline.Add(new BezierKnot(pos), TangentMode.AutoSmooth);
        }

        if(displayNodes)
        {
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

        if (splineContainer != null && splinePoints.Count > 0)
        {
            followerDrone.transform.position = GetWorldPositionAtNormalizedTime(0f);
            if (splineRotations.Count > 0) followerDrone.transform.rotation = splineRotations[0];
        }

        // trail settings
        droneTrail = followerDrone.AddComponent<TrailRenderer>();
        droneTrail.time = Mathf.Infinity;
        droneTrail.startWidth = 0.25f;
        droneTrail.endWidth = 0.25f;
        droneTrail.material = new Material(Shader.Find("Sprites/Default"));
        
        Gradient gradient = new Gradient();
        
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(trackColor, 0.0f), new GradientColorKey(trackColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
        );
        droneTrail.colorGradient = gradient;

        EvaluatePositionAtTime(0f);
    }

    void Update()
    {
        if (followerDrone != null && splineContainer != null && splinePoints.Count > 0)
        {
            EvaluatePositionAtTime(StateManager.GlobalTime);
        }
    }

    private void EvaluatePositionAtTime(float globalTime)
    {
        if (globalTime == 0f && droneTrail != null)
        {
            droneTrail.Clear();
        }

        int knotCount = splineContainer.Spline.Count;

        // if track is open, there is one less segment than there is nodes
        int totalSegments = splineContainer.Spline.Closed ? knotCount : knotCount - 1;
        
        float totalTrackDuration = totalSegments * 1.0f;// assumes the data is given in 1 second uniform intervals
    
        float loopTime = globalTime % totalTrackDuration;
        
        if (globalTime >= totalTrackDuration && !splineContainer.Spline.Closed)
        {
            return;
        }

        int currentSegmentIndex = Mathf.FloorToInt(loopTime / 1.0f);

        // the t-value of the current segment of the spline, where t=0 is exactly node 1 and t=1 is node 2
        float segmentProgress = (loopTime % 1.0f) / 1.0f;

        // takes the fraction of the current segment compared to total segments
        // then lerps to find the more specific fraction to represent progress through current segment
        float startNormalized = (float)currentSegmentIndex / totalSegments;
        float endNormalized = (float)(currentSegmentIndex + 1) / totalSegments;
        float globalNormalizedTime = Mathf.Lerp(startNormalized, endNormalized, segmentProgress);

        followerDrone.transform.position = GetWorldPositionAtNormalizedTime(globalNormalizedTime);

        Quaternion startRotation = splineRotations[currentSegmentIndex];
        Quaternion endRotation = (splineContainer.Spline.Closed && currentSegmentIndex == totalSegments - 1) 
            ? splineRotations[0] 
            : splineRotations[currentSegmentIndex + 1];

        followerDrone.transform.rotation = Quaternion.Slerp(startRotation, endRotation, segmentProgress);

        DrawHeading();
        DrawVelocity(globalNormalizedTime);
    }

    private void DrawDebugArrow(Vector3 start, Vector3 direction, Color color, float headSize = 0.3f)
    {
        Debug.DrawRay(start, direction, color);
        
        Vector3 tip = start + direction;
        Vector3 backwardDir = -direction.normalized;
        
        // determine a reliable side axis depending on vertical alignment
        Vector3 crossHint = Mathf.Abs(Vector3.Dot(backwardDir, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
        Vector3 rightDir = Vector3.Cross(backwardDir, crossHint).normalized;

        Vector3 rightHead = (backwardDir + rightDir).normalized * headSize;
        Vector3 leftHead = (backwardDir - rightDir).normalized * headSize;

        Debug.DrawRay(tip, rightHead, color);
        Debug.DrawRay(tip, leftHead, color);
    }

    private void DrawHeading()
    {
        Vector3 arrowStart = followerDrone.transform.position;

        // define the direction of heading out of the local negative Y axis
        // looking at raw data that seems to be where heading is pointed
        float arrowLength = 2.0f;
        Vector3 arrowDirection = -followerDrone.transform.up * arrowLength;

        DrawDebugArrow(arrowStart, arrowDirection, Color.cyan);
    }
    
    public void DrawVelocity(float t)
    {
        float lengthMult = 5.0f;
        Vector3 localPos = splineContainer.EvaluatePosition(t);

        // heuristic derivative
        Vector3 updPos = splineContainer.EvaluatePosition(t + 0.001f);
        Vector3 velocity = (updPos - localPos) * lengthMult;

        Vector3 arrowStart = followerDrone.transform.position;
        Vector3 arrowDirection = velocity.normalized;

        DrawDebugArrow(arrowStart, arrowDirection, Color.red);
    }
    
    public float GetTotalDuration(){return totalLapTimeDuration;}

    private Vector3 GetWorldPositionAtNormalizedTime(float t)
    {
        if (splineContainer == null) return Vector3.zero;

        Vector3 localPos = splineContainer.EvaluatePosition(t);
        return splineContainer.transform.TransformPoint(localPos);
    }
}