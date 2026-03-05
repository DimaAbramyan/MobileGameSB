using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldAbilityPrefab : MonoBehaviour, iDamagable
{
    private float shieldHealth;
    private float multiplier = 2;
    [SerializeField] private PlayerController controller;

    private Transform currentAnchor;
    public void Awake()
    {
        controller = FindAnyObjectByType<PlayerController>();
        if (controller == null)
            Debug.LogError("Controller not found");
    }
    private void OnEnable()
    {
        controller.OnCurrentShipChanged += HandleShipChanged;

        if (controller.CurrentShip != null)
            HandleShipChanged(controller.CurrentShip);
    }

    private void OnDisable()
    {
        controller.OnCurrentShipChanged -= HandleShipChanged;
    }

    private void HandleShipChanged(ParentShip newShip)
    {
        currentAnchor = newShip.ShieldAnchor;
    }

    private void LateUpdate()
    {
        if (currentAnchor == null)
            return;
        transform.position = currentAnchor.position;
        transform.rotation = currentAnchor.rotation;
    }
    public void Init(float health)
    {
        shieldHealth = health * multiplier;
    }
    public void TakeDamage(float damage)
    {
        shieldHealth-=damage;
        if (shieldHealth < 0)
        {
            Dying();
        }
    }
    public void Dying()
    {
        Destroy(gameObject);
    }
}
