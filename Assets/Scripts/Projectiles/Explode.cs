using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
using FMODUnity;

public class Explode : MonoBehaviour
{
    [Inject] SoundManager soundManager;
    [SerializeField] EventReference explosionSound;
    [SerializeField] private float damage;
    [SerializeField] private Collider2D explosionCollider;
    [SerializeField] private float activeTime = 0.1f;
    [SerializeField] private bool destroyAfterExplosion = true;
    [SerializeField] private float destroyDelay = 0.5f;

    private HashSet<GameObject> damagedObjects = new HashSet<GameObject>();

    private void Awake()
    {
        soundManager.PlaySound(explosionSound, transform.position);
        if (explosionCollider == null)
        {
            explosionCollider = GetComponent<Collider2D>();
        }

        explosionCollider.isTrigger = true;

        StartCoroutine(ExplosionRoutine());
    }

    private IEnumerator ExplosionRoutine()
    {
        yield return new WaitForSeconds(activeTime);

        explosionCollider.enabled = false;

        damagedObjects.Clear();

        if (destroyAfterExplosion)
        {
            yield return new WaitForSeconds(destroyDelay);
            Destroy(gameObject);
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (damagedObjects.Contains(collision.gameObject))
            return;

        iDamagable receiver = collision.gameObject.GetComponent<iDamagable>();
        if (receiver != null)
        {
            Debug.Log($"Взрыв наносит урон: {collision.gameObject.name}, урон: {damage}");
            receiver.TakeDamage(damage);
            damagedObjects.Add(collision.gameObject);
        }
    }

    protected virtual void OnTriggerStay2D(Collider2D collision)
    {
        if (damagedObjects.Contains(collision.gameObject))
            return;

        iDamagable receiver = collision.gameObject.GetComponent<iDamagable>();
        if (receiver != null)
        {
            Debug.Log($"Взрыв наносит урон (Stay): {collision.gameObject.name}");
            receiver.TakeDamage(damage);
            damagedObjects.Add(collision.gameObject);
        }
    }

    public void SetDamage(float newDamage)
    {
        damage = newDamage;
    }

    private void OnDrawGizmosSelected()
    {
        if (explosionCollider != null)
        {
            Gizmos.color = Color.red;

            if (explosionCollider is CircleCollider2D circle)
            {
                Gizmos.DrawWireSphere(transform.position, circle.radius);
            }
            else if (explosionCollider is BoxCollider2D box)
            {
                Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
            }
        }
    }
}