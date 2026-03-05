using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBar : MonoBehaviour
{
    private ParentShip currentShip;
    [SerializeField] private PlayerController playerController;
    public Slider slider;
    float maxHealth;
    public Gradient gradient;
    [SerializeField] private Image fill;
    private void OnEnable()
    {
        playerController.OnCurrentShipChanged += Bind;
    }

    private void OnDisable()
    {
        playerController.OnCurrentShipChanged -= Bind;
    }
    public void SetHealth(float health)
    {
        Debug.Log($"Текущее здоровье - {health}");
        Debug.Log($"Макс здоровье - {maxHealth}");

        if (maxHealth <= 0f)
            return;

        float normalized = Mathf.Clamp01(health / maxHealth);

        slider.value = normalized;

        if (gradient != null)
            fill.color = gradient.Evaluate(normalized);
    }
    public void Bind(ParentShip ship)
    {
        if (currentShip != null)
            currentShip.OnHealthChanged -= SetHealth;

        currentShip = ship;
        maxHealth = ship.ShipData.maximumHealthPoints;

        currentShip.OnHealthChanged += SetHealth;

        SetHealth(ship.CurrentHealthPoints);
        Debug.Log(ship.CurrentHealthPoints);
    }
}
