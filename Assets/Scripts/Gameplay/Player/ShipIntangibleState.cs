using System.Collections;
using UnityEngine;

public sealed class ShipIntangibleState : MonoBehaviour
{
    [SerializeField] private Collider2D[] hitboxColliders;
    [SerializeField] private bool autoCollectRootColliders = true;
    [SerializeField] private bool autoCollectChildColliders = true;

    private bool[] initialColliderStates;
    private int activeRequests;

    public bool IsActive => activeRequests > 0;

    private void Awake()
    {
        InitializeColliders();
    }

    public void Enter()
    {
        InitializeColliders();

        activeRequests++;
        ApplyColliderState(false);
    }

    public void Exit()
    {
        if (activeRequests <= 0)
            return;

        activeRequests--;

        if (activeRequests == 0)
            RestoreColliderState();
    }

    public void ActivateForSeconds(float duration)
    {
        if (duration <= 0f)
            return;

        StartCoroutine(ActivateForSecondsRoutine(duration));
    }

    private IEnumerator ActivateForSecondsRoutine(float duration)
    {
        Enter();
        yield return new WaitForSeconds(duration);
        Exit();
    }

    private void InitializeColliders()
    {
        if (hitboxColliders != null && hitboxColliders.Length > 0)
        {
            EnsureInitialStates();
            return;
        }

        if (!autoCollectRootColliders)
            return;

        hitboxColliders = autoCollectChildColliders
            ? GetComponentsInChildren<Collider2D>(true)
            : GetComponents<Collider2D>();
        EnsureInitialStates();
    }

    private void EnsureInitialStates()
    {
        if (hitboxColliders == null)
            return;

        if (initialColliderStates != null
            && initialColliderStates.Length == hitboxColliders.Length)
        {
            return;
        }

        initialColliderStates = new bool[hitboxColliders.Length];
        for (int i = 0; i < hitboxColliders.Length; i++)
            initialColliderStates[i] =
                hitboxColliders[i] != null && hitboxColliders[i].enabled;
    }

    private void ApplyColliderState(bool isEnabled)
    {
        if (hitboxColliders == null)
            return;

        for (int i = 0; i < hitboxColliders.Length; i++)
        {
            if (hitboxColliders[i] != null)
                hitboxColliders[i].enabled = isEnabled;
        }
    }

    private void RestoreColliderState()
    {
        if (hitboxColliders == null)
            return;

        EnsureInitialStates();

        for (int i = 0; i < hitboxColliders.Length; i++)
        {
            if (hitboxColliders[i] != null)
                hitboxColliders[i].enabled = initialColliderStates[i];
        }
    }

    private void OnDisable()
    {
        activeRequests = 0;
        RestoreColliderState();
    }
}
