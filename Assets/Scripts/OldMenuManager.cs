using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Splines;

public class DroneMenuManager : MonoBehaviour
{
    [Header("Camera Configuration")]
    public Camera mainCamera;
    public float cameraPadding = 1.2f;

    public TextAsset jsonFile;
    public GameObject droneSplinePrefab; 

    [Header("UI Toolkit References")]
    public UIDocument uiDocument;
    
    // UI elements
    private DropdownField playerDropdown;
    private Button spawnButton;
    private Button playButton;
    private Button pauseButton;
    private Button restartButton;
    private VisualElement droneListContainer; 
    private Label timerLabel;

    // shared time value accessible by all drone instances
    public static float GlobalTime { get; private set; } = 0f;
    public static bool IsPlaying { get; private set; } = false;

    private float maxDuration = 0f;

    private List<DroneData> fullDroneList = new List<DroneData>();

    // dictionary holds the corresponding list index of each spline
    // this allows drones to be spawned out of order while still maintaining their original index
    // this is useful when fetching data for the drones such as track name after they have already been instantiated
    private Dictionary<int, GameObject> activeSplines = new Dictionary<int, GameObject>();

    void Awake()
    {
        if (jsonFile == null)
        {
            Debug.LogError("No JSON file assigned to DroneMenuManager");
            return;
        }

        // Wrap data array into valid JSON format and parse it
        string raw = jsonFile.text;
        string wrappedJsonText = "{\"droneList\": " + raw + " }";
        DroneDataCollection wrappedData = JsonUtility.FromJson<DroneDataCollection>(wrappedJsonText);
        fullDroneList = wrappedData.droneList;

        SetupUI();
    }

    void Update()
    {
        if (IsPlaying)
        {
            GlobalTime += Time.deltaTime;

            if (GlobalTime >= maxDuration && maxDuration > 0f)
            {
                GlobalTime = maxDuration; 
                IsPlaying = false;
            }
        }

        if (timerLabel != null)
        {
            timerLabel.text = $"Time: {GlobalTime.ToString("F2")}s";
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

        List<string> choices = new List<string>();
        
        int index = 1;
        foreach (var drone in fullDroneList)
        {
            choices.Add($"P{index}: {drone.Player.PlayerId}");
            index++;
        }
        playerDropdown.choices = choices;

        if (choices.Count > 0)
            playerDropdown.index = 0;

        // Register button actions
        spawnButton.clicked += OnSpawnDroneClicked;
        playButton.clicked += OnPlayClicked;
        pauseButton.clicked += OnPauseClicked;
        restartButton.clicked += ResetPlayback;
    }

    void ResetPlayback()
    {
        IsPlaying = false;
        GlobalTime = 0f;
    }

    void OnSpawnDroneClicked()
    {
        // Enforce the requirement that spawning only works when paused at the beginning
        if (GlobalTime > 0f || IsPlaying)
        {
            Debug.LogWarning("Drones can only be spawned when the simulation is restarted and paused");
            return;
        }

        int selectedIndex = playerDropdown.index;
        if (selectedIndex < 0 || selectedIndex >= fullDroneList.Count) return;

        // Prevents same drone being spawned repeatedly
        if (activeSplines.ContainsKey(selectedIndex))
        {
            Debug.LogWarning($"Drone for player {fullDroneList[selectedIndex].Player.PlayerId} is already active");
            return;
        }

        // Prevents drones from different tracks being spawned together
        if (activeSplines.Count > 0)
        {
            // Grab the dropdown index (the dictionary key) of any currently active drone
            int firstActiveKey = 0;
            foreach (int key in activeSplines.Keys)
            {
                firstActiveKey = key;
                break; // just need one to find out what track is currently active
            }

            string activeTrackName = fullDroneList[firstActiveKey].Player.Track;
            string selectedTrackName = fullDroneList[selectedIndex].Player.Track;

            if (!selectedTrackName.Equals(activeTrackName))
            {
                Debug.LogWarning($"Cannot spawn player from track '{selectedTrackName}'. A drone from track '{activeTrackName}' is already active.");
                return;
            }
        }

        // instantiate new drone and grab info to display about it
        GameObject newSplineInstance = Instantiate(droneSplinePrefab, Vector3.zero, Quaternion.identity);
        string playerId = fullDroneList[selectedIndex].Player.PlayerId;
        float laptime = (float)fullDroneList[selectedIndex].Data[0].LapTime / 200; // dividing by 200 since data is in 200 fps
        newSplineInstance.name = $"Spline_{playerId}";

        SplineCreator creator = newSplineInstance.GetComponent<SplineCreator>();

        if (creator != null)
        {
            creator.InitializeAndBuild(fullDroneList[selectedIndex]);
            activeSplines.Add(selectedIndex, newSplineInstance);
            
            CreateDroneUIEntry(selectedIndex, playerId, laptime, newSplineInstance, creator.trackColor);

            if (activeSplines.Count == 1)
            {
                FocusCameraOnSpline(newSplineInstance);
            }
            maxDuration = Mathf.Max(maxDuration, creator.GetTotalDuration());
            Debug.Log($"New maxDuration is '{maxDuration}'");
        }
    }

    // Generates a UI element list entry containing the player ID and an 'X' button to remove the drone
    void CreateDroneUIEntry(int index, string playerId, float laptime, GameObject droneInstance, Color textColor)
    {
        if (droneListContainer == null) return;

        // alignment
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 4;

        Label label = new Label();
        label.style.color = new StyleColor(textColor);
        label.text = $"Player {index + 1}: {playerId}\nLap time: {laptime}";

        Button removeButton = new Button();
        removeButton.text = "X";
        
        // lambda method, lets button hold onto code even after instantiated
        removeButton.clicked += () => {
            if (activeSplines.ContainsKey(index))
            {
                Destroy(activeSplines[index]);
                activeSplines.Remove(index);
            }
            droneListContainer.Remove(row);

            RecalculateMaxDuration();
        };

        row.Add(label);
        row.Add(removeButton);

        // putting the row with the label into the ui on screen
        droneListContainer.Add(row);
    }

    // allows camera to move to different tracks and rotate with user input
    void FocusCameraOnSpline(GameObject splineRoot)
    {
        if (mainCamera == null) return;

        SplineContainer splineContainer = splineRoot.GetComponentInChildren<SplineContainer>();
        if (splineContainer == null) return;

        // try to find our camera controller script on the main camera
        TrackCameraController cameraController = mainCamera.GetComponent<TrackCameraController>();

        if (cameraController != null)
        {
            // hands off the calculations and manual positioning to the camera script
            cameraController.InitializeTarget(splineContainer, cameraPadding);
        }
        else
        {
            // Fallback to original static position if the camera script is missing
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

    private void RecalculateMaxDuration()
    {
        maxDuration = 0f;
        foreach (var splineEntry in activeSplines.Values)
        {
            SplineCreator creator = splineEntry.GetComponent<SplineCreator>();
            if (creator != null)
            {
                maxDuration = Mathf.Max(maxDuration, creator.GetTotalDuration());
            }
        }
        Debug.Log($"New maxDuration is '{maxDuration}'");
    }

    private void OnPlayClicked()
    {
        if (activeSplines.Count > 0)
        {
            IsPlaying = true;
        }
        else
        {
            Debug.LogWarning("Cannot play: No drones have been spawned yet.");
        }
    }

    private void OnPauseClicked()
    {
        IsPlaying = false;
    }

    void OnDestroy()
    {
        if (spawnButton != null)   spawnButton.clicked -= OnSpawnDroneClicked;
        if (playButton != null)    playButton.clicked -= OnPlayClicked;
        if (pauseButton != null)   pauseButton.clicked -= OnPauseClicked;
        if (restartButton != null) restartButton.clicked -= ResetPlayback;
    }
}