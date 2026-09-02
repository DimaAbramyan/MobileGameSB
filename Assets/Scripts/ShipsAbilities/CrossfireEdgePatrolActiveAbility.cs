using System.Collections;
using UnityEngine;
using Zenject;

public sealed class CrossfireEdgePatrolActiveAbility : ActiveAbility
{
    [InjectOptional] private Camera injectedCamera;

    [SerializeField] private CrossfireCompanionWeaponsPassiveAbility companionWeapons;
    [SerializeField] private Camera cameraOverride;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float duration = 6f;
    [SerializeField, Min(0.01f)] private float speed = 6f;
    [SerializeField, Range(0f, 0.45f)] private float edgeInset = 0.05f;
    [SerializeField] private AnimationCurve approachCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve edgeMovementCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private Coroutine patrolRoutine;
    private CrossfireCompanionWeaponsPassiveAbility activeCompanions;

    public override bool Activate(ParentShip owner)
    {
        if (owner == null || patrolRoutine != null)
            return false;

        CrossfireCompanionWeaponsPassiveAbility companions = ResolveCompanions(owner);
        Camera gameplayCamera = ResolveCamera();
        if (companions == null || gameplayCamera == null)
            return false;

        if (!companions.TryEnterEdgePatrol(out Weapon leftWeapon, out Weapon rightWeapon))
        {
            Debug.LogWarning("Crossfire cannot start edge patrol without two active companion weapons.", this);
            return false;
        }

        activeCompanions = companions;
        patrolRoutine = StartCoroutine(RunPatrol(owner, gameplayCamera, leftWeapon, rightWeapon));
        return true;
    }

    protected override void OnDisable()
    {
        if (patrolRoutine != null)
        {
            StopCoroutine(patrolRoutine);
            patrolRoutine = null;
        }

        FinishPatrol();
        base.OnDisable();
    }

    private IEnumerator RunPatrol(
        ParentShip owner,
        Camera gameplayCamera,
        Weapon leftWeapon,
        Weapon rightWeapon)
    {
        Vector3 leftCenter = GetViewportPosition(
            gameplayCamera,
            edgeInset,
            0.5f,
            leftWeapon.transform.position.z);
        Vector3 rightCenter = GetViewportPosition(
            gameplayCamera,
            1f - edgeInset,
            0.5f,
            rightWeapon.transform.position.z);

        Quaternion leftEdgeRotation = GetRotationForDirection(Vector2.right);
        Quaternion rightEdgeRotation = GetRotationForDirection(Vector2.left);

        yield return MoveToEdgeCenters(
            owner,
            leftWeapon,
            rightWeapon,
            leftCenter,
            rightCenter,
            leftEdgeRotation,
            rightEdgeRotation);

        if (!CanContinue(owner, leftWeapon, rightWeapon))
        {
            FinishPatrol();
            yield break;
        }

        Vector3 leftBottom = GetViewportPosition(
            gameplayCamera,
            edgeInset,
            edgeInset,
            leftWeapon.transform.position.z);
        Vector3 leftTop = GetViewportPosition(
            gameplayCamera,
            edgeInset,
            1f - edgeInset,
            leftWeapon.transform.position.z);
        Vector3 rightBottom = GetViewportPosition(
            gameplayCamera,
            1f - edgeInset,
            edgeInset,
            rightWeapon.transform.position.z);
        Vector3 rightTop = GetViewportPosition(
            gameplayCamera,
            1f - edgeInset,
            1f - edgeInset,
            rightWeapon.transform.position.z);

        float edgeHeight = Mathf.Max(0.01f, leftTop.y - leftBottom.y);
        float leftProgress = 0.5f;
        float rightProgress = 0.5f;
        float leftDirection = 1f;
        float rightDirection = -1f;
        float elapsed = 0f;

        while (elapsed < duration && CanContinue(owner, leftWeapon, rightWeapon))
        {
            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;

            float progressDelta = speed / edgeHeight * deltaTime;
            AdvancePingPong(ref leftProgress, ref leftDirection, progressDelta);
            AdvancePingPong(ref rightProgress, ref rightDirection, progressDelta);

            float leftCurveValue = EvaluateCurve(edgeMovementCurve, leftProgress);
            float rightCurveValue = EvaluateCurve(edgeMovementCurve, rightProgress);
            activeCompanions.SetEdgePatrolPose(
                Vector3.LerpUnclamped(leftBottom, leftTop, leftCurveValue),
                leftEdgeRotation,
                Vector3.LerpUnclamped(rightBottom, rightTop, rightCurveValue),
                rightEdgeRotation);

            yield return null;
        }

        FinishPatrol();
    }

    private IEnumerator MoveToEdgeCenters(
        ParentShip owner,
        Weapon leftWeapon,
        Weapon rightWeapon,
        Vector3 leftTarget,
        Vector3 rightTarget,
        Quaternion leftTargetRotation,
        Quaternion rightTargetRotation)
    {
        Vector3 leftStart = leftWeapon.transform.position;
        Vector3 rightStart = rightWeapon.transform.position;
        Quaternion leftStartRotation = leftWeapon.transform.rotation;
        Quaternion rightStartRotation = rightWeapon.transform.rotation;
        float farthestDistance = Mathf.Max(
            Vector3.Distance(leftStart, leftTarget),
            Vector3.Distance(rightStart, rightTarget));
        float moveDuration = farthestDistance / speed;

        if (moveDuration <= 0f)
        {
            activeCompanions.SetEdgePatrolPose(
                leftTarget,
                leftTargetRotation,
                rightTarget,
                rightTargetRotation);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < moveDuration && CanContinue(owner, leftWeapon, rightWeapon))
        {
            elapsed += Time.deltaTime;
            float progress = EvaluateCurve(approachCurve, elapsed / moveDuration);
            activeCompanions.SetEdgePatrolPose(
                Vector3.LerpUnclamped(leftStart, leftTarget, progress),
                Quaternion.SlerpUnclamped(leftStartRotation, leftTargetRotation, progress),
                Vector3.LerpUnclamped(rightStart, rightTarget, progress),
                Quaternion.SlerpUnclamped(rightStartRotation, rightTargetRotation, progress));
            yield return null;
        }

        if (CanContinue(owner, leftWeapon, rightWeapon))
        {
            activeCompanions.SetEdgePatrolPose(
                leftTarget,
                leftTargetRotation,
                rightTarget,
                rightTargetRotation);
        }
    }

    private CrossfireCompanionWeaponsPassiveAbility ResolveCompanions(ParentShip owner)
    {
        if (companionWeapons != null)
            return companionWeapons;

        companionWeapons = owner.GetComponent<CrossfireCompanionWeaponsPassiveAbility>();
        if (companionWeapons == null)
            Debug.LogError("Crossfire edge patrol requires CrossfireCompanionWeaponsPassiveAbility.", this);

        return companionWeapons;
    }

    private Camera ResolveCamera()
    {
        Camera gameplayCamera = cameraOverride != null
            ? cameraOverride
            : injectedCamera;

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (gameplayCamera == null)
            Debug.LogError("Crossfire edge patrol requires a gameplay camera.", this);

        return gameplayCamera;
    }

    private bool CanContinue(ParentShip owner, Weapon leftWeapon, Weapon rightWeapon)
    {
        return owner != null
            && owner.IsVisible
            && activeCompanions != null
            && activeCompanions.IsReadyForCombat
            && leftWeapon != null
            && rightWeapon != null;
    }

    private void FinishPatrol()
    {
        activeCompanions?.ExitEdgePatrol();
        activeCompanions = null;
        patrolRoutine = null;
    }

    private static Vector3 GetViewportPosition(Camera gameplayCamera, float x, float y, float worldZ)
    {
        float distance = Mathf.Abs(worldZ - gameplayCamera.transform.position.z);
        Vector3 viewportPosition = gameplayCamera.ViewportToWorldPoint(new Vector3(x, y, distance));
        viewportPosition.z = worldZ;
        return viewportPosition;
    }

    private static Quaternion GetRotationForDirection(Vector2 direction)
    {
        return Quaternion.FromToRotation(Vector3.up, direction);
    }

    private static float EvaluateCurve(AnimationCurve curve, float value)
    {
        return curve == null || curve.length == 0
            ? Mathf.Clamp01(value)
            : curve.Evaluate(Mathf.Clamp01(value));
    }

    private static void AdvancePingPong(ref float progress, ref float direction, float delta)
    {
        progress += direction * delta;

        while (progress > 1f || progress < 0f)
        {
            if (progress > 1f)
            {
                progress = 2f - progress;
                direction = -1f;
            }
            else
            {
                progress = -progress;
                direction = 1f;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        duration = Mathf.Max(0.1f, duration);
        speed = Mathf.Max(0.01f, speed);
        edgeInset = Mathf.Clamp(edgeInset, 0f, 0.45f);
    }
#endif
}
