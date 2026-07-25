using System.Collections;
using UnityEngine;

public class PhantomPhaseActiveAbility : ActiveAbility
{
    [SerializeField, Min(0.05f)] private float maximumPhaseDuration = 3f;
    [SerializeField] private bool hideVisuals = true;
    [SerializeField] private bool stopShooting = true;
    [SerializeField] private bool purgeProjectilesWhenReappearing = true;
    [SerializeField] private bool enableKeyboardTestInput = true;
    [SerializeField] private KeyCode keyboardTestKey = KeyCode.R;
    [SerializeField, Range(0f, 1f)] private float phasedAlpha = 0.4f;

    private Coroutine phaseCoroutine;
    private ParentShip activeOwner;
    private WeaponController weaponController;
    private SpriteRenderer[] renderers;
    private bool[] rendererStates;
    private Color[] rendererColors;
    private bool isPhased;

    protected override bool StartsCooldownOnActivation => false;

    public override bool Activate(ParentShip owner)
    {
        if (isPhased || owner == null)
            return false;

        activeOwner = owner;
        owner.EnterIntangibleState();

        if (stopShooting)
        {
            weaponController = owner.GetComponent<WeaponController>();
            weaponController?.BeginShootingSuppression();
        }

        if (hideVisuals)
            SetVisualsTransparent(owner);

        isPhased = true;
        phaseCoroutine = StartCoroutine(PhaseTimer());
        return true;
    }

    public override void Release(ParentShip owner)
    {
        EndPhase();
    }

    private IEnumerator PhaseTimer()
    {
        yield return new WaitForSeconds(maximumPhaseDuration);
        EndPhase();
    }

    private void EndPhase()
    {
        if (!isPhased)
            return;

        if (phaseCoroutine != null)
        {
            StopCoroutine(phaseCoroutine);
            phaseCoroutine = null;
        }

        if (hideVisuals && activeOwner != null)
            RestoreVisuals();

        if (stopShooting)
            weaponController?.EndShootingSuppression();

        activeOwner?.ExitIntangibleState();

        if (purgeProjectilesWhenReappearing && activeOwner != null)
        {
            PhantomProjectilePurgePassive purgePassive =
                activeOwner.GetComponentInChildren<PhantomProjectilePurgePassive>(true);
            purgePassive?.PurgeNow();
        }

        activeOwner = null;
        weaponController = null;
        isPhased = false;
        StartCooldown();
    }

    protected override void Update()
    {
        base.Update();

        if (!enableKeyboardTestInput)
            return;

        ParentShip keyboardOwner = activeOwner != null
            ? activeOwner
            : owner != null
                ? owner
                : GetComponent<ParentShip>();

        if (keyboardOwner == null)
            return;

        if (Input.GetKeyDown(keyboardTestKey))
            TryActivate(keyboardOwner);

        if (Input.GetKeyUp(keyboardTestKey))
            Release(keyboardOwner);
    }

    private void SetVisualsTransparent(ParentShip owner)
    {
        renderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
        rendererStates = new bool[renderers.Length];
        rendererColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            rendererStates[i] = renderers[i] != null && renderers[i].enabled;
            rendererColors[i] = renderers[i] != null ? renderers[i].color : Color.white;

            if (renderers[i] != null)
            {
                Color color = renderers[i].color;
                color.a = Mathf.Clamp01(phasedAlpha);
                renderers[i].color = color;
            }
        }
    }

    private void RestoreVisuals()
    {
        if (renderers == null || rendererStates == null || rendererColors == null)
            return;

        int count = Mathf.Min(renderers.Length, rendererStates.Length, rendererColors.Length);
        for (int i = 0; i < count; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = rendererStates[i];
                renderers[i].color = rendererColors[i];
            }
        }
    }

    private void OnDisable()
    {
        EndPhase();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        cooldown = 2f;
    }
#endif
}
