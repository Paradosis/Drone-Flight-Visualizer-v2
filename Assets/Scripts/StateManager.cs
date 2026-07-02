using System.Collections.Generic;
using UnityEngine;

public class StateManager : MonoBehaviour
{
    public static float GlobalTime { get; private set; } = 0f;
    public static bool IsPlaying { get; private set; } = false;
    public float MaxDuration { get; private set; } = 0f;

    void Update()
    {
        if (IsPlaying)
        {
            GlobalTime += Time.deltaTime;

            if (GlobalTime >= MaxDuration && MaxDuration > 0f)
            {
                GlobalTime = MaxDuration; 
                IsPlaying = false;
            }
        }
    }

    public void StartPlayback()
    {
        IsPlaying = true;
    }

    public void PausePlayback()
    {
        IsPlaying = false;
    }

    public void ResetPlayback()
    {
        IsPlaying = false;
        GlobalTime = 0f;
    }

    public void SetMaxDuration(float duration)
    {
        MaxDuration = duration;
    }

    public void RecalculateMaxDuration(Dictionary<int, GameObject> activeSplines)
    {
        MaxDuration = 0f;
        foreach (var splineEntry in activeSplines.Values)
        {
            SplineCreator creator = splineEntry.GetComponent<SplineCreator>();
            if (creator != null)
            {
                MaxDuration = Mathf.Max(MaxDuration, creator.GetTotalDuration());
            }
        }
        Debug.Log($"New maxDuration is '{MaxDuration}'");
    }
}