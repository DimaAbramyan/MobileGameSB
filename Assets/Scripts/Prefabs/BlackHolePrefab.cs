using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackHolePrefab : MonoBehaviour
{
    float liveTime;
      [SerializeField, HideInInspector]
      float damage = 50;
      private float runtimeDamage = -1f;
      private ParentShip owner;
      private float fallbackDamageMultiplier = 1f;

      public void Init(
          float lifeTime,
          float metaDamage = -1f,
          ParentShip sourceOwner = null,
          float damageMultiplier = 1f)
      {
          liveTime = lifeTime;
          runtimeDamage = metaDamage;
          owner = sourceOwner;
          fallbackDamageMultiplier = Mathf.Max(1f, damageMultiplier);
          Destroy(gameObject, liveTime);
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        Enemy enemy = collision.gameObject.GetComponent<Enemy>();
        EnemyProjectile proj = collision.gameObject.GetComponent<EnemyProjectile>();

        if (proj != null)
        {
            Destroy(proj.gameObject);
        }
        if (enemy != null)
        {
              float baseDamage = runtimeDamage >= 0f ? runtimeDamage : damage;
              enemy.TakeDamage(baseDamage * GetCurrentDamageMultiplier());
          }
      }

      private float GetCurrentDamageMultiplier()
      {
          return owner != null && owner.ShipData != null
              ? owner.ShipData.GetAbilityDamageMultiplier(owner.GetLevel())
              : fallbackDamageMultiplier;
      }
  }
