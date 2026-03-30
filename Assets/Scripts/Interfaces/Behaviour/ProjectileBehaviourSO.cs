using UnityEngine;
public abstract class ProjectileBehaviourSO : ScriptableObject, IProjectileBehaviour
{
    public abstract void Tick(Projectile projectile);
}