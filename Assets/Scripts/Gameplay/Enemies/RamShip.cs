using UnityEngine;
using Zenject;

public class RamShip : Enemy
{
    private enum RamState
    {
        Charging,
        Lunging,
        Returning
    }

    private enum SpawnEdge
    {
        Left,
        Top,
        Bottom,
        Right,
        Random
    }

    [Inject] private PlayerController playerController;

    [Header("Ram Attack")]
    [SerializeField, Min(0f)] private float chargeDuration = 0.8f;
    [SerializeField, Min(0f)] private float windUpDistance = 0.4f;
    [SerializeField, Min(0f)] private float startLungeSpeed = 4f;
    [SerializeField, Min(0.01f)] private float maxLungeSpeed = 12f;
    [SerializeField, Min(0.01f)] private float accelerationTime = 0.35f;
    [SerializeField] private AnimationCurve accelerationCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f, 4f, 4f),
            new Keyframe(1f, 1f, 0f, 0f));

    [Header("Edge Spawn")]
    [SerializeField] private SpawnEdge spawnEdge = SpawnEdge.Top;
    [SerializeField] private bool useEdgeSpawnOnStart = false;
    [SerializeField, Min(0f)] private float viewportMargin = 0.15f;
    [SerializeField, Range(0f, 1f)] private float minPositionOnEdge = 0.1f;
    [SerializeField, Range(0f, 1f)] private float maxPositionOnEdge = 0.9f;

    [Header("World Reset Bounds")]
    [SerializeField] private Vector2 resetMinPosition = new Vector2(-4f, -6f);
    [SerializeField] private Vector2 resetMaxPosition = new Vector2(4f, 6f);

    [Header("Return")]
    [SerializeField, Min(0.01f)] private float returnDuration = 0.8f;
    [SerializeField] private AnimationCurve returnCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f)] private float restartDelay = 0.35f;

    private Rigidbody2D body;
    private Camera mainCamera;
    private Vector3 homePosition;
    private Vector3 chargeStartPosition;
    private Vector3 returnStartPosition;
    private Vector2 lungeDirection;
    private RamState state;
    private float stateTimer;

    private void Start()
    {
        body = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;
        homePosition = transform.position;

        if (useEdgeSpawnOnStart)
            MoveTo(GetEdgeSpawnPosition());

        BeginCharge();
    }

    private void FixedUpdate()
    {
        if (isDead)
            return;

        stateTimer += Time.fixedDeltaTime;

        switch (state)
        {
            case RamState.Charging:
                UpdateCharge();
                break;
            case RamState.Lunging:
                UpdateLunge();
                break;
            case RamState.Returning:
                UpdateReturn();
                break;
        }
    }

    private void BeginCharge()
    {
        state = RamState.Charging;
        stateTimer = 0f;
        chargeStartPosition = transform.position;
        StopMovement();
    }

    private void UpdateCharge()
    {
        Vector2 targetDirection = GetDirectionToPlayer();
        Vector2 normalizedTargetDirection = targetDirection.sqrMagnitude > 0.0001f
            ? targetDirection.normalized
            : Vector2.down;

        if (chargeDuration > 0f && windUpDistance > 0f)
        {
            float progress = Mathf.Clamp01(stateTimer / chargeDuration);
            Vector3 windUpPosition =
                chargeStartPosition
                - (Vector3)(normalizedTargetDirection * windUpDistance);
            MoveTo(Vector3.Lerp(chargeStartPosition, windUpPosition, progress));
        }

        if (stateTimer < chargeDuration + restartDelay)
            return;

        lungeDirection = normalizedTargetDirection;
        state = RamState.Lunging;
        stateTimer = 0f;
    }

    private void UpdateLunge()
    {
        float accelerationProgress = Mathf.Clamp01(stateTimer / accelerationTime);
        float curvedProgress = accelerationCurve != null
            ? Mathf.Clamp01(accelerationCurve.Evaluate(accelerationProgress))
            : accelerationProgress;
        float speed = Mathf.Lerp(startLungeSpeed, maxLungeSpeed, curvedProgress);
        Vector2 delta = lungeDirection * speed * Time.fixedDeltaTime;

        MoveTo(transform.position + (Vector3)delta);

        if (IsOutsideResetBounds())
            BeginReturn();
    }

    private void BeginReturn()
    {
        state = RamState.Returning;
        stateTimer = 0f;
        returnStartPosition = GetEdgeSpawnPosition();
        MoveTo(returnStartPosition);
        StopMovement();
    }

    private void UpdateReturn()
    {
        float progress = Mathf.Clamp01(stateTimer / returnDuration);
        float curvedProgress = returnCurve != null
            ? Mathf.Clamp01(returnCurve.Evaluate(progress))
            : progress;

        MoveTo(Vector3.Lerp(returnStartPosition, homePosition, curvedProgress));

        if (progress < 1f)
            return;

        MoveTo(homePosition);
        BeginCharge();
    }

    private Vector2 GetDirectionToPlayer()
    {
        Transform target = GetPlayerTarget();
        if (target == null)
            return Vector2.down;

        return target.position - transform.position;
    }

    private Transform GetPlayerTarget()
    {
        if (playerController == null)
            return null;

        if (playerController.CurrentShip != null)
            return playerController.CurrentShip.transform;

        return playerController.transform;
    }

    private bool IsOutsideResetBounds()
    {
        Vector3 position = transform.position;
        return position.x < resetMinPosition.x
            || position.x > resetMaxPosition.x
            || position.y < resetMinPosition.y
            || position.y > resetMaxPosition.y;
    }

    private Vector3 GetEdgeSpawnPosition()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return homePosition;

        float min = Mathf.Min(minPositionOnEdge, maxPositionOnEdge);
        float max = Mathf.Max(minPositionOnEdge, maxPositionOnEdge);
        float randomPosition = Random.Range(min, max);
        SpawnEdge selectedEdge = GetSelectedSpawnEdge();

        Vector3 viewportPosition = selectedEdge switch
        {
            SpawnEdge.Left => new Vector3(-viewportMargin, randomPosition, 0f),
            SpawnEdge.Right => new Vector3(1f + viewportMargin, randomPosition, 0f),
            SpawnEdge.Bottom => new Vector3(randomPosition, -viewportMargin, 0f),
            SpawnEdge.Top => new Vector3(randomPosition, 1f + viewportMargin, 0f),
            _ => new Vector3(randomPosition, 1f + viewportMargin, 0f)
        };

        float distanceFromCameraToShipPlane =
            Mathf.Abs(transform.position.z - mainCamera.transform.position.z);
        viewportPosition.z = distanceFromCameraToShipPlane;

        Vector3 worldPosition = mainCamera.ViewportToWorldPoint(viewportPosition);
        worldPosition.z = homePosition.z;
        return worldPosition;
    }

    private SpawnEdge GetSelectedSpawnEdge()
    {
        if (spawnEdge != SpawnEdge.Random)
            return spawnEdge;

        return (SpawnEdge)Random.Range(0, 4);
    }

    private void MoveTo(Vector3 position)
    {
        if (body != null && body.simulated)
            body.MovePosition(position);
        else
            transform.position = position;
    }

    private void StopMovement()
    {
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void OnValidate()
    {
        if (maxPositionOnEdge < minPositionOnEdge)
            maxPositionOnEdge = minPositionOnEdge;

        if (resetMaxPosition.x < resetMinPosition.x)
            resetMaxPosition.x = resetMinPosition.x;
        if (resetMaxPosition.y < resetMinPosition.y)
            resetMaxPosition.y = resetMinPosition.y;

        if (accelerationCurve == null || accelerationCurve.length == 0)
        {
            accelerationCurve = new AnimationCurve(
                new Keyframe(0f, 0f, 4f, 4f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        if (returnCurve == null || returnCurve.length == 0)
            returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }
}
