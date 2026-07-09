using System.Collections.Generic;
using UnityEngine;

public class JsonManager : MonoBehaviour
{
    public TextAsset jsonFile;
    private List<DroneDataEntity> FullDroneEntityList { get; set; } = new List<DroneDataEntity>();
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
            FullDroneEntityList = wrappedData.droneList;

            ConvertEntitiesToRuntimeData();
        }
    }

    private void ConvertEntitiesToRuntimeData()
    {
        FullDroneList.Clear();

        foreach (DroneDataEntity entity in FullDroneEntityList)
        {
            DroneData runtimeDrone = new DroneData
            {
                Player = new PlayerInfo
                {
                    PlayerID = entity.Player.PlayerId,
                    Track = entity.Player.Track
                },
                Laps = new List<LapRuntimeData>(entity.Data.Count)
            };

            foreach (LapDataEntity lapEntity in entity.Data)
            {
                LapRuntimeData runtimeLap = new LapRuntimeData
                {
                    LapTime = lapEntity.LapTime / 200f, // dividing by 200f as raw data appears to be in 200fps
                    Telemetry = new List<TelemetryRuntimeFrame>(lapEntity.Data.Count)
                };

                foreach (TelemetryFrameEntity frameEntity in lapEntity.Data)
                {
                    TelemetryRuntimeFrame runtimeFrame = new TelemetryRuntimeFrame
                    {
                        TimeFrame = frameEntity.TimeFrame,
                        
                        DronePosition = frameEntity.DronePosition.ToVector3(),
                        DroneRotation = frameEntity.DroneRotation.ToQuaternion()
                    };

                    runtimeLap.Telemetry.Add(runtimeFrame);
                }

                runtimeDrone.Laps.Add(runtimeLap);
            }

            FullDroneList.Add(runtimeDrone);
        }
    }
}