using UnityEngine;

public class DealDamageManager : MonoBehaviour
{
    public static DealDamageManager instanse;
    private void Awake()
    {
        if (instanse != null && instanse != this)
        {
            Destroy(gameObject);
            return;
        }
        instanse = this;
    }
    public void DealDamage(iDamagable target, Projectile projectile)
    {
        if (target == null)
            return;
        if (projectile.Owner != null)
            projectile.Owner.NotifyDamageDealt(projectile.GetDamage());
        target.TakeDamage(projectile.GetDamage());
    }
    private void OnDestroy()
    {
        if (instanse == this)
            instanse = null;
    }
}
