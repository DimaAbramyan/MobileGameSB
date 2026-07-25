using UnityEngine;

public sealed class TailSegmentFollower : MonoBehaviour
{
    private Transform followTarget;
    private float targetYOffset;
    private float xVelocity;
    private float smoothTime = 0.08f;
    private float maxSpeed = 30f;

    public void Configure(
        Transform target,
        float yOffset,
        float followSmoothTime,
        float followMaxSpeed)
    {
        followTarget = target;
        targetYOffset = yOffset;
        smoothTime = Mathf.Max(0.001f, followSmoothTime);
        maxSpeed = Mathf.Max(0.001f, followMaxSpeed);
        xVelocity = 0f;

        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (followTarget == null)
            return;

        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = GetTargetPosition();

        currentPosition.x = Mathf.SmoothDamp(
            currentPosition.x,
            targetPosition.x,
            ref xVelocity,
            smoothTime,
            maxSpeed,
            Time.deltaTime);

        currentPosition.y = targetPosition.y;
        currentPosition.z = targetPosition.z;
        transform.position = currentPosition;
    }

    private Vector3 GetTargetPosition()
    {
        Vector3 targetPosition = followTarget.position;
        targetPosition.y += targetYOffset;
        return targetPosition;
    }

    public void SnapToTarget()
    {
        if (followTarget == null)
            return;

        xVelocity = 0f;
        transform.position = GetTargetPosition();
    }

    public void SnapYToTarget()
    {
        if (followTarget == null)
            return;

        Vector3 position = transform.position;
        Vector3 targetPosition = GetTargetPosition();
        position.y = targetPosition.y;
        position.z = targetPosition.z;
        transform.position = position;
    }
}
