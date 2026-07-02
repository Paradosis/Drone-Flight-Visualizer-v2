using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Splines;

public class UIManager : MonoBehaviour
{
    [Header("Managers")]
    public JsonManager jsonManager;
    public StateManager stateManager;

    [Header("Camera Configuration")]
    public Camera mainCamera;
    public float cameraPadding = 1.2f;

    [Header("Prefabs")]
    public GameObject droneSplinePrefab;

    [Header("UI Toolkit References")]
    public UIDocument uiDocument;
    
    private DropdownField playerDropdown;
    private Button spawnButton;
    private Button playButton;
    private Button pauseButton;
    private Button restartButton;
    private VisualElement droneListContainer; 
    private Label timerLabel;

    private Dictionary<int, GameObject> activeSplines = new Dictionary<int, GameObject>();

    void Start()
    {
        if (jsonManager == null || stateManager == null)
        {
            Debug.LogError("Missing Manager references on UIManager!");
            return;
        }

        SetupUI();
    }

    void Update()
    {
        if (timerLabel != null)
        {
            timerLabel.text = $"Time: {StateManager.GlobalTime.ToString("F2")}s";
        }
    }

    void SetupUI()
    {
        if (uiDocument == null) return;
        
        var root = uiDocument.rootVisualElement;

        playerDropdown = root.Q<DropdownField>("PlayerDropdown");
        spawnButton = root.Q<Button>("SpawnButton");
        playButton = root.Q<Button>("PlayButton");
        pauseButton = root.Q<Button>("PauseButton");
        restartButton = root.Q<Button>("RestartButton");
        droneListContainer = root.Q<VisualElement>("DroneList");
        timerLabel = root.Q<Label>("TimerLabel");

        PopulateDropdown();

        spawnButton.clicked += OnSpawnDroneClicked;
        playButton.clicked += OnPlayClicked;
        pauseButton.clicked += OnPauseClicked;
        restartButton.clicked += OnRestartClicked;
    }

    private void PopulateDropdown()
    {
        List<string> choices = new List<string>();
        int index = 1; // starting index at 1 for player numbers, eg P1, P2, P3, etc.
        
        foreach (var drone in jsonManager.FullDroneList)
        {
            choices.Add($"P{index}: {drone.Player.PlayerId}");
            index++;
        }
        playerDropdown.choices = choices;

        if (choices.Count > 0)
            playerDropdown.index = 0;
    }

    private void OnPlayClicked()
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

    private void OnPauseClicked()
    {
        stateManager.PausePlayback();
    }

    private void OnRestartClicked()
    {
        stateManager.ResetPlayback();
    }

    private void OnSpawnDroneClicked()
    {
        if (StateManager.GlobalTime > 0f || StateManager.IsPlaying)
        {
            Debug.LogWarning("Drones can only be spawned when the simulation is restarted and paused");
            return;
        }

        int selectedIndex = playerDropdown.index;
        var droneList = jsonManager.FullDroneList;

        if (selectedIndex < 0 || selectedIndex >= droneList.Count) return;

        if (activeSplines.ContainsKey(selectedIndex))
        {
            Debug.LogWarning($"Drone for player {droneList[selectedIndex].Player.PlayerId} is already active");
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
        string playerId = droneList[selectedIndex].Player.PlayerId;
        float laptime = droneList[selectedIndex].Data[0].LapTime / 200f;
        newSplineInstance.name = $"Spline_{playerId}";

        SplineCreator creator = newSplineInstance.GetComponent<SplineCreator>();

        if (creator != null)
        {
            creator.InitializeAndBuild(droneList[selectedIndex]);
            activeSplines.Add(selectedIndex, newSplineInstance);
            
            CreateDroneUIEntry(selectedIndex, playerId, laptime, creator.trackColor);

            if (activeSplines.Count == 1)
            {
                FocusCameraOnSpline(newSplineInstance);
            }
            
            stateManager.SetMaxDuration(Mathf.Max(stateManager.MaxDuration, creator.GetTotalDuration()));
            Debug.Log($"New maxDuration is '{stateManager.MaxDuration}'");
        }
    }

    void CreateDroneUIEntry(int index, string playerId, float laptime, Color textColor)
    {
        if (droneListContainer == null) return;

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 4;

        Label label = new Label();
        label.style.color = new StyleColor(textColor);
        label.text = $"Player {index + 1}: {playerId}\nLap time: {laptime}";

        Button removeButton = new Button {text = "X"};
        removeButton.clicked += () => {
            if (activeSplines.ContainsKey(index))
            {
                Destroy(activeSplines[index]);
                activeSplines.Remove(index);
            }
            droneListContainer.Remove(row);
            stateManager.RecalculateMaxDuration(activeSplines);
        };

        row.Add(label);
        row.Add(removeButton);
        droneListContainer.Add(row);
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

    void OnDestroy()
    {
        if (spawnButton != null)   spawnButton.clicked -= OnSpawnDroneClicked;
        if (playButton != null)    playButton.clicked -= OnPlayClicked;
        if (pauseButton != null)   pauseButton.clicked -= OnPauseClicked;
        if (restartButton != null) restartButton.clicked -= OnRestartClicked;
    }
}