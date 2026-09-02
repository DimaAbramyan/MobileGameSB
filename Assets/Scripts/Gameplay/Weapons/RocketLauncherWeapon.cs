using System.Collections;

using UnityEngine;

public sealed class RocketLauncherWeapon : Weapon
{
    private Coroutine burstRoutine;
    private float burstReloadMultiplier = 1f;

    public override bool TryToShoot()
    {
        if (!IsAbleToShoot || burstRoutine != null)
            return false;

        currentReloadTime -= Time.deltaTime;
        if (currentReloadTime > 0f)
            return false;

        if (!FireRocket(notifyShot: true))
        {
            currentReloadTime = reloadTime;
            return false;
        }

        int remainingRockets = CurrentStats.VolleysPerActivation - 1;
        if (remainingRockets <= 0)
            return true;

        burstRoutine = StartCoroutine(FireRemainingRockets(
            remainingRockets,
            CurrentStats.DelayBetweenVolleys));
        return true;
    }

    public override bool TryShootImmediately(float reloadMultiplier = 1f)
    {
        if (!IsAbleToShoot || !gameObject.activeInHierarchy)
            return false;

        bool rocketFired = FireRocket(notifyShot: false);
        currentReloadTime = reloadTime * Mathf.Max(0f, reloadMultiplier);
        return rocketFired;
    }

    public override void Reload(float multiplier)
    {
        burstReloadMultiplier = Mathf.Max(0f, multiplier);

        if (burstRoutine == null)
            base.Reload(burstReloadMultiplier);
    }

    public override void HideWeapon()
    {
        base.HideWeapon();
        CancelBurst();
    }

    public override void AbleToShoot(bool newAble)
    {
        base.AbleToShoot(newAble);

        if (!newAble)
            CancelBurst();
    }

    private IEnumerator FireRemainingRockets(
        int remainingRockets,
        float delayBetweenRockets)
    {
        for (int rocketIndex = 0;
             rocketIndex < remainingRockets;
             rocketIndex++)
        {
            // Let WeaponController provide the current reload multiplier first.
            yield return null;

            float remainingDelay = delayBetweenRockets * burstReloadMultiplier;
            while (remainingDelay > 0f)
            {
                if (!CanContinueBurst())
                {
                    FinishBurst();
                    yield break;
                }

                remainingDelay -= Time.deltaTime;
                yield return null;
            }

            while (Time.timeScale <= 0f)
                yield return null;

            if (!CanContinueBurst())
            {
                FinishBurst();
                yield break;
            }

            FireRocket(notifyShot: true);
        }

        FinishBurst();
    }

    private bool FireRocket(bool notifyShot)
    {
        bool rocketFired = base.Fire();
        if (rocketFired && notifyShot)
            RaiseShotFired();

        return rocketFired;
    }

    private bool CanContinueBurst()
    {
        return IsAbleToShoot && gameObject.activeInHierarchy;
    }

    private void CancelBurst()
    {
        if (burstRoutine == null)
            return;

        StopCoroutine(burstRoutine);
        FinishBurst();
    }

    private void FinishBurst()
    {
        burstRoutine = null;
        currentReloadTime = reloadTime * burstReloadMultiplier;
    }

    private void OnDisable()
    {
        CancelBurst();
    }
}
