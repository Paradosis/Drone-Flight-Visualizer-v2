using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[Serializable]
public class DroneDataCollection
{
    public List<DroneDataEntity> droneList;
}

[Serializable]
public class DroneDataEntity
{
    public PlayerInfoEntity Player;
    public string QuadKey;
    public List<LapDataEntity> Data;
}

[Serializable]
public class PlayerInfoEntity
{
    public string Track;
    public string PlayerId;
    public string GhostKey;
}

[Serializable]
public class LapDataEntity
{
    public int LapTime;
    public string DateOfCreation;
    public List<TelemetryFrameEntity> Data;
}

[Serializable]
public struct TelemetryFrameEntity
{
    public float TimeFrame;
    public Vector3DataEntity DronePosition;
    public QuaternionEntityData DroneRotation;
}

[Serializable]
public struct Vector3DataEntity
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
public struct QuaternionEntityData
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
}
