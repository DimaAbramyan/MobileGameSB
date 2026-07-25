using UnityEngine;
using Zenject;

public sealed class ArkanoidPassiveAbility : PassiveAbility
{
    [InjectOptional] private DiContainer container;

    [Header("Prefabs")]
    [SerializeField] private ArkanoidPaddle paddlePrefab;
    [SerializeField] private ArkanoidBall ballPrefab;

    [Header("Paddle")]
    [SerializeField] private Vector2 paddleOffset = new Vector2(0f, -0.85f);
    [SerializeField, Min(0.01f)] private float paddleFollowSpeed = 30f;

    [Header("Ball")]
    [SerializeField] private Vector2 ballSpawnOffset = new Vector2(0f, 0.75f);
    [SerializeField, Min(0.01f)] private float ballSpeed = 5f;
    [SerializeField, Min(0f)] private float ballDamage = 10f;
    [SerializeField, Range(0f, 80f)] private float randomLaunchAngle = 45f;
    [SerializeField] private bool keepBallAliveWhenShipIsInactive = true;

    private ArkanoidPaddle paddle;
    private ArkanoidBall ball;

    public override void Init(ParentShip ship)
    {
        owner = ship;
    }

    public override void On()
    {
        base.On();

        if (owner == null)
            owner = GetComponent<ParentShip>();
        if (owner == null)
            return;

        bool ballWasCreated = ball == null;
        EnsureObjects();

        if (paddle != null)
        {
            paddle.gameObject.SetActive(true);
            paddle.Configure(owner.transform, paddleOffset, paddleFollowSpeed);
        }

        if (ball != null)
        {
            bool shouldLaunch = ballWasCreated || !ball.gameObject.activeInHierarchy;
            ball.gameObject.SetActive(true);
            ball.Configure(owner, paddle, ballSpeed, ballDamage, ballSpawnOffset);

            if (shouldLaunch)
            {
                ball.ResetAndLaunch(
                    ball.GetOwnerFrontSpawnPosition(),
                    CreateRandomLaunchDirection());
            }
            else
            {
                ball.TryRespawnIfReady();
            }
        }
    }

    public override void Off()
    {
        base.Off();

        if (ball != null && !keepBallAliveWhenShipIsInactive)
            ball.gameObject.SetActive(false);

        if (paddle != null)
            paddle.gameObject.SetActive(false);
    }

    public bool TryActivateStasis()
    {
        return isActive
            && ball != null
            && ball.gameObject.activeInHierarchy
            && ball.TryActivateStasis();
    }

    private void EnsureObjects()
    {
        if (paddle == null && paddlePrefab != null)
            paddle = CreateInstance(paddlePrefab);

        if (ball == null && ballPrefab != null)
            ball = CreateInstance(ballPrefab);
    }

    private T CreateInstance<T>(T prefab)
        where T : Component
    {
        T instance = Instantiate(prefab);
        container?.InjectGameObject(instance.gameObject);
        return instance;
    }

    private Vector2 CreateRandomLaunchDirection()
    {
        float angle = Random.Range(-randomLaunchAngle, randomLaunchAngle);
        return Quaternion.Euler(0f, 0f, angle) * Vector2.up;
    }

    private void OnDestroy()
    {
        if (ball != null)
            Destroy(ball.gameObject);

        if (paddle != null)
            Destroy(paddle.gameObject);
    }
}
