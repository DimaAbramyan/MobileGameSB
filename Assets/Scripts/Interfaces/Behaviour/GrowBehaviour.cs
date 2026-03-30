using UnityEngine;

[CreateAssetMenu(menuName = "ProjectileBehaviour/Grow")]
public class GrowBehaviour : ProjectileBehaviourSO
{
    public float growSpeed = 30f;
    public override void Tick(Projectile projectile)
    {
        var scale = projectile.transform.localScale;
        scale.x += growSpeed * Time.deltaTime;
        projectile.transform.localScale = scale;

        float newDamage = projectile.baseDamage / Mathf.Max(0.1f, projectile.transform.localScale.x);
        projectile.SetDamage(newDamage);
    }
}