using System;
using UnityEngine;

public sealed class ShipKnockbackService
{
    [Serializable]
    public struct Settings
    {
        [Header("Enemy Impact")]
        [Min(0f)] public float baseImpulse;
        [Min(0f)] public float relativeSpeedMultiplier;
        [Min(0f)] public float maximumImpulse;

        [Header("Ship Resistance")]
        [Min(0.001f)] public float referenceMass;
        [Min(0.001f)] public float referenceSpeed;
        [Min(0f)] public float massInfluence;
        [Min(0f)] public float speedInfluence;
        [Min(0f)] public float dragInfluence;

        public static Settings Default => new Settings
        {
            baseImpulse = 30f,
            relativeSpeedMultiplier = 3.75f,
            maximumImpulse = 70f,
            referenceMass = 5f,
            referenceSpeed = 100f,
            massInfluence = 2f,
            speedInfluence = 0.1f,
            dragInfluence = 0.02f
        };
    }

    public float CalculateImpulse(
        ShipData shipData,
        float enemyImpactSpeed,
        Settings settings)
    {
        float enemyImpactImpulse = Mathf.Max(0f, settings.baseImpulse)
            + Mathf.Max(0f, enemyImpactSpeed)
            * Mathf.Max(0f, settings.relativeSpeedMultiplier);

        float resistance = CalculateResistance(shipData, settings);
        float impulse = enemyImpactImpulse / resistance;

        return Mathf.Clamp(
            impulse,
            0f,
            Mathf.Max(0f, settings.maximumImpulse));
    }

    private static float CalculateResistance(
        ShipData shipData,
        Settings settings)
    {
        if (shipData == null)
            return 1f;

        float referenceMass = Mathf.Max(0.001f, settings.referenceMass);
        float referenceSpeed = Mathf.Max(0.001f, settings.referenceSpeed);
        float mass = Mathf.Max(0.001f, shipData.mass);
        float speed = Mathf.Max(0f, shipData.speed);
        float drag = Mathf.Max(0f, shipData.drag);

        float massResistance =
            Mathf.Pow(mass / referenceMass, Mathf.Max(0f, settings.massInfluence));
        float speedResistance =
            1f
            + Mathf.Sqrt(speed / referenceSpeed)
            * Mathf.Max(0f, settings.speedInfluence);
        float dragResistance = 1f + drag * Mathf.Max(0f, settings.dragInfluence);

        return Mathf.Max(0.001f, massResistance * speedResistance * dragResistance);
    }
}
