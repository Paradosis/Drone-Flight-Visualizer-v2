using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIController : MonoBehaviour
{
    [Header("UI Toolkit References")]
    public UIDocument uiDocument;
    
    private DropdownField playerDropdown;
    private Button spawnButton;
    private Button playButton;
    private Button pauseButton;
    private Button restartButton;
    private VisualElement droneListContainer; 
    private Label timerLabel;


    public event Action OnPlayPressed;
    public event Action OnPausePressed;
    public event Action OnRestartPressed;
    public event Action<int> OnSpawnPressed; // passes the selected dropdown index
    public event Action<int, VisualElement> OnRemoveDronePressed; // passes index and UI element to destroy

    void Awake()
    {
        SetupUI();
    }

    private void SetupUI()
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

        playButton.clicked += () => OnPlayPressed?.Invoke();
        pauseButton.clicked += () => OnPausePressed?.Invoke();
        restartButton.clicked += () => OnRestartPressed?.Invoke();
        spawnButton.clicked += () => OnSpawnPressed?.Invoke(playerDropdown.index);
    }

    public void PopulateDropdown(List<string> choices)
    {
        playerDropdown.choices = choices;
        if (choices.Count > 0)
            playerDropdown.index = 0;
    }

    public void UpdateTimerText(float globalTime)
    {
        if (timerLabel != null)
        {
            timerLabel.text = $"Time: {globalTime.ToString("F2")}s";
        }
    }

    public void CreateDroneUIEntry(int index, string playerId, float laptime, Color textColor)
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

        Button removeButton = new Button { text = "X" };
        removeButton.clicked += () => 
        {
            // 'row' is passed so the manager can tell us if it safely deleted the object before clearing the UI asset
            OnRemoveDronePressed?.Invoke(index, row);
        };

        row.Add(label);
        row.Add(removeButton);
        droneListContainer.Add(row);
    }

    public void RemoveDroneUIEntry(VisualElement row)
    {
        droneListContainer?.Remove(row);
    }
}