using UnityEngine;

[CreateAssetMenu(menuName = "ProjectileBehaviour/SimpleGrow")]
public class SimpleGrowBehaviour : ProjectileBehaviourSO
{
    public float growSpeed = 0.5f;
    public override void Tick(Projectile projectile)
    {
        projectile.transform.localScale += Vector3.one * growSpeed * Time.deltaTime;
    }
}