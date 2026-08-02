using UnityEngine;

[System.Serializable]
public class JitterLayer
{
    public Transform transform;

    public bool affectX = true;
    public bool affectY = true;

    public float maxOffsetX = 0.02f;
    public float maxOffsetY = 0.02f;

    public float frequencyMultiplier = 1f;

    public Vector2 seed;

    [HideInInspector]
    public Vector3 initialPosition;

    [HideInInspector]
    public bool hasInitialPosition;

    [HideInInspector]
    public Transform capturedTransform;
}
