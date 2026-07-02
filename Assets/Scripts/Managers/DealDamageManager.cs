using UnityEngine;

public class DealDamageManager
{
    public void DealDamage(iDamagable target, Projectile projectile)
    {
        if (target == null)
            return;
        if (projectile.Owner != null)
            projectile.Owner.NotifyDamageDealt(projectile.GetDamage());
        target.TakeDamage(projectile.GetDamage());
    }
}
