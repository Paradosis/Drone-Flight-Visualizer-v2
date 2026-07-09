using System.Collections.Generic;
using UnityEngine;

public class DroneData
{
    public PlayerInfo Player { get; set; }
    public string QuadKey { get; set; }
    public List<LapRuntimeData> Laps { get; set; } = new List<LapRuntimeData>();
}

public class PlayerInfo
{
    public string PlayerID;
    public string Track;
}
public class LapRuntimeData
{
    public float LapTime { get; set; }
    public List<TelemetryRuntimeFrame> Telemetry { get; set; } = new List<TelemetryRuntimeFrame>();
}

public struct TelemetryRuntimeFrame
{
    public float TimeFrame { get; set; }
    public Vector3 DronePosition { get; set; }
    public Quaternion DroneRotation { get; set; }
}