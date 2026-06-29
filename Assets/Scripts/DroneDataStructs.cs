using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DroneDataCollection
{
    public List<DroneData> droneList;
}

[Serializable]
public class DroneData
{
    public PlayerInfo Player;
    public string QuadKey;
    public List<LapData> Data;
}

[Serializable]
public class PlayerInfo
{
    public string Track;
    public string PlayerId;
    public string GhostKey;
}

[Serializable]
public class LapData
{
    public int LapTime;
    public string DateOfCreation;
    public List<TelemetryFrame> Data;
}

[Serializable]
public class TelemetryFrame
{
    public float TimeFrame;
    public Vector3Data DronePosition;
    public Vector4Data DroneRotation;
}

[Serializable]
public struct Vector3Data
{
    public float X;
    public float Y;
    public float Z;

    public Vector3 ToVector3()
    {
        return new Vector3(X, Y, Z);
    }
}

[Serializable]
public struct Vector4Data
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    // for some reason the rotations or the spawned cube seem to be flipped over the XY-Plane
    // the reflection matrix flips the rotations so that Z points up and the pitch is downward
    public Quaternion ToQuaternion()
    {
        // return new Quaternion(X, Y, Z, W);
        Quaternion rawQuat = new Quaternion(X, Y, Z, W);

        // reflection matrix that flips the vertical Y-axis
        Matrix4x4 reflectionMatrix = Matrix4x4.identity;
        reflectionMatrix.m11 = -1f; // Invert the Y basis vector (vertical axis)

        // convert the quaternion to a matrix, apply the reflection, and pull it back
        Matrix4x4 rotationMatrix = Matrix4x4.Rotate(rawQuat);
        Matrix4x4 reflectedMatrix = reflectionMatrix * rotationMatrix * reflectionMatrix;

        return reflectedMatrix.rotation;
    }

    public Vector3 ToEulerAngles() 
    {
        return ToQuaternion().eulerAngles;
    }
}