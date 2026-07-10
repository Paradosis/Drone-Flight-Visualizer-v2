using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Splines;

public class SceneController : MonoBehaviour
{
    [Header("Controllers & Managers")]
    public UIController uiController;
    public DataService dataService;
    public StateManager stateManager;

    [Header("Camera Configuration")]
    public Camera mainCamera;
    public float cameraPadding = 1.2f;

    [Header("Prefabs")]
    public GameObject droneSplinePrefab;

    private Dictionary<int, GameObject> activeSplines = new Dictionary<int, GameObject>();

    void Awake()
    {
        if (dataService == null || stateManager == null || uiController == null)
        {
            Debug.LogError("Missing component references on UIManager!");
            return;
        }
    }

    void Start()
    {
        uiController.Initialize(HandlePlay, HandlePause, HandleRestart, HandleSpawnDrone, HandleRemoveDrone);

        InitializeUIData();
    }

    void Update()
    {
        uiController.UpdateTimerText(StateManager.GlobalTime);
    }

    private void InitializeUIData()
    {
        List<string> choices = new List<string>();
        int index = 1;
        foreach (var drone in dataService.FullDroneList)
        {
            choices.Add($"P{index}: {drone.Player.PlayerID}");
            index++;
        }
        uiController.PopulateDropdown(choices);
    }

    private void HandlePlay()
    {
        if (activeSplines.Count > 0)
        {
            stateManager.StartPlayback();
        }
        else
        {
            Debug.LogWarning("Cannot play: No drones have been spawned yet.");
        }
    }

    private void HandlePause()
    {
        stateManager.PausePlayback();
    }

    private void HandleRestart()
    {
        stateManager.ResetPlayback();
    }

    private void HandleSpawnDrone(int selectedIndex)
    {
        if (StateManager.GlobalTime > 0f || StateManager.IsPlaying)
        {
            Debug.LogWarning("Drones can only be spawned when the simulation is restarted and paused");
            return;
        }

        var droneList = dataService.FullDroneList;
        if (selectedIndex < 0 || selectedIndex >= droneList.Count) return;

        if (activeSplines.ContainsKey(selectedIndex))
        {
            Debug.LogWarning($"Drone for player {droneList[selectedIndex].Player.PlayerID} is already active");
            return;
        }

        if (activeSplines.Count > 0)
        {
            int firstActiveKey = 0;
            foreach (int key in activeSplines.Keys)
            {
                firstActiveKey = key;
                break;
            }

            string activeTrackName = droneList[firstActiveKey].Player.Track;
            string selectedTrackName = droneList[selectedIndex].Player.Track;

            if (!selectedTrackName.Equals(activeTrackName))
            {
                Debug.LogWarning($"Cannot spawn player from track '{selectedTrackName}'. A drone from track '{activeTrackName}' is already active.");
                return;
            }
        }

        GameObject newSplineInstance = Instantiate(droneSplinePrefab, Vector3.zero, Quaternion.identity);
        string playerId = droneList[selectedIndex].Player.PlayerID;
        float laptime = droneList[selectedIndex].Laps[0].LapTime;
        newSplineInstance.name = $"Spline_{playerId}";

        SplineCreator creator = newSplineInstance.GetComponent<SplineCreator>();

        if (creator != null)
        {
            creator.InitializeAndBuild(droneList[selectedIndex]);
            activeSplines.Add(selectedIndex, newSplineInstance);
            
            uiController.CreateDroneUIEntry(selectedIndex, playerId, laptime, creator.trackColor);

            if (activeSplines.Count == 1)
            {
                FocusCameraOnSpline(newSplineInstance);
            }
            
            stateManager.SetMaxDuration(Mathf.Max(stateManager.MaxDuration, creator.GetTotalDuration()));
            Debug.Log($"New maxDuration is '{stateManager.MaxDuration}'");
        }
    }

    private void HandleRemoveDrone(int index, VisualElement uiRow)
    {
        if (StateManager.IsPlaying || StateManager.GlobalTime != 0f)
        {
            Debug.LogWarning("Cannot remove drones until simulation is restarted");
            return;
        }

        if (activeSplines.ContainsKey(index))
        {
            Destroy(activeSplines[index]);
            activeSplines.Remove(index);
        }

        uiController.RemoveDroneUIEntry(uiRow);
        stateManager.RecalculateMaxDuration(activeSplines);
    }

    void FocusCameraOnSpline(GameObject splineRoot)
    {
        if (mainCamera == null) return;

        SplineContainer splineContainer = splineRoot.GetComponentInChildren<SplineContainer>();
        if (splineContainer == null) return;

        TrackCameraController cameraController = mainCamera.GetComponent<TrackCameraController>();

        if (cameraController != null)
        {
            cameraController.InitializeTarget(splineContainer, cameraPadding);
        }
        else
        {
            Bounds splineBounds = SplineUtility.GetBounds(splineContainer.Spline);
            float objectSize = Mathf.Max(splineBounds.size.x, splineBounds.size.z);
            float cameraFOV = mainCamera.fieldOfView;
            float targetDistance = (objectSize / 2f) / Mathf.Tan(cameraFOV * 0.5f * Mathf.Deg2Rad);
            targetDistance *= cameraPadding;

            mainCamera.transform.position = splineBounds.center + (Vector3.up * targetDistance);
            mainCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            
            Debug.LogWarning("TrackCameraController component not found on Main Camera. Defaulting to static top-down view.");
        }
    }
}