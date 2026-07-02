using System.Collections.Generic;
using UnityEngine;

public class JsonManager : MonoBehaviour
{
    public TextAsset jsonFile;
    public List<DroneData> FullDroneList { get; private set; } = new List<DroneData>();

    void Awake()
    {
        ParseJsonData();
    }

    private void ParseJsonData()
    {
        if (jsonFile == null)
        {
            Debug.LogError("No JSON file assigned to JsonManager");
            return;
        }

        // Wrap data array into valid JSON format and parse it
        string raw = jsonFile.text;
        string wrappedJsonText = "{\"droneList\": " + raw + " }";
        DroneDataCollection wrappedData = JsonUtility.FromJson<DroneDataCollection>(wrappedJsonText);
        
        if (wrappedData != null && wrappedData.droneList != null)
        {
            FullDroneList = wrappedData.droneList;
        }
    }
}