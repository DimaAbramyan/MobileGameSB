using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class TwinCloneController : MonoBehaviour
{
    [Header("Collision")]
    [SerializeField] private bool destroyOnEnemyProjectile = true;
    [SerializeField] private bool suppressShootingInsideEnemy = true;

    [Header("Movement")]
    [SerializeField] private bool moveForward;
    [SerializeField, Min(0f)] private float moveSpeed = 7f;
    [SerializeField, Min(0f)] private float lifetime = 6f;

    [Header("Auto shell")]
    [SerializeField] private bool createVisualFromOwner = true;
    [SerializeField] private bool createTriggerColliderFromVisual = true;

    private readonly List<Weapon> cloneWeapons = new();

    private ParentShip ownerShip;
    private WeaponController ownerWeaponController;
    private Rigidbody2D rb;
    private DiContainer container;
    private int enemyOverlapCount;
    private float lifeTimer;
    private bool isConfigured;

    public bool IsTwinCopy => true;

    public void Configure(
        ParentShip owner,
        DiContainer diContainer,
        bool isFlyingClone,
        float flyingCloneSpeed,
        float flyingCloneLifetime)
    {
        ownerShip = owner;
        ownerWeaponController = ownerShip != null
            ? ownerShip.GetComponent<WeaponController>()
            : null;
        container = diContainer;

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.angularVelocity = 0f;

        moveForward = isFlyingClone;
        moveSpeed = Mathf.Max(0f, flyingCloneSpeed);
        lifetime = Mathf.Max(0f, flyingCloneLifetime);
        lifeTimer = lifetime;

        RemoveCloneShipComponents();
        EnsureShell();
        RebuildWeaponsFromOwner();

        isConfigured = true;
    }

    public void MirrorPositionFrom(Transform source)
    {
        if (source == null)
            return;

        Vector3 position = source.position;
        position.x = -position.x;
        transform.position = position;
    }

    private void FixedUpdate()
    {
        if (!isConfigured)
            return;

        if (!moveForward)
            return;

        Vector2 velocity = transform.up * moveSpeed;
        if (rb != null)
            rb.linearVelocity = velocity;
        else
            transform.position += (Vector3)(velocity * Time.fixedDeltaTime);

        if (lifetime <= 0f)
            return;

        lifeTimer -= Time.fixedDeltaTime;
        if (lifeTimer <= 0f)
            Destroy(gameObject);
    }

    private void RebuildWeaponsFromOwner()
    {
        UnregisterCloneWeapons();

        if (ownerShip == null || ownerWeaponController == null)
            return;

        Weapon[] sourceWeapons = ownerShip.GetComponentsInChildren<Weapon>(true);
        for (int i = 0; i < sourceWeapons.Length; i++)
        {
            Weapon sourceWeapon = sourceWeapons[i];
            if (sourceWeapon == null)
                continue;

            Weapon cloneWeapon = CreateWeaponCopy(sourceWeapon);
            if (cloneWeapon == null)
                continue;

            cloneWeapon.SetLevel(ownerShip.GetLevel());
            ownerWeaponController.RegisterExternalWeapon(cloneWeapon);
            cloneWeapon.ShowWeapon();
            cloneWeapons.Add(cloneWeapon);
        }
    }

    private Weapon CreateWeaponCopy(Weapon sourceWeapon)
    {
        GameObject instance = container != null
            ? container.InstantiatePrefab(sourceWeapon.gameObject, transform)
            : Instantiate(sourceWeapon.gameObject, transform);

        instance.transform.localPosition = sourceWeapon.transform.localPosition;
        instance.transform.localRotation = sourceWeapon.transform.localRotation;
        instance.transform.localScale = sourceWeapon.transform.localScale;

        ParentShip nestedShip = instance.GetComponent<ParentShip>();
        if (nestedShip != null)
            nestedShip.enabled = false;

        ActiveAbility[] activeAbilities = instance.GetComponentsInChildren<ActiveAbility>(true);
        for (int i = 0; i < activeAbilities.Length; i++)
            activeAbilities[i].enabled = false;

        PassiveAbility[] passiveAbilities = instance.GetComponentsInChildren<PassiveAbility>(true);
        for (int i = 0; i < passiveAbilities.Length; i++)
            passiveAbilities[i].enabled = false;

        return instance.GetComponent<Weapon>();
    }

    private void EnsureShell()
    {
        if (createVisualFromOwner)
            EnsureVisualFromOwner();

        if (createTriggerColliderFromVisual && GetComponent<Collider2D>() == null)
            CreateTriggerCollider();
    }

    private void EnsureVisualFromOwner()
    {
        if (GetComponentInChildren<SpriteRenderer>(true) != null || ownerShip == null)
            return;

        SpriteRenderer ownerRenderer =
            ownerShip.GetComponentInChildren<SpriteRenderer>(true);
        if (ownerRenderer == null)
            return;

        SpriteRenderer cloneRenderer = gameObject.AddComponent<SpriteRenderer>();
        cloneRenderer.sprite = ownerRenderer.sprite;
        cloneRenderer.color = ownerRenderer.color;
        cloneRenderer.flipX = ownerRenderer.flipX;
        cloneRenderer.flipY = ownerRenderer.flipY;
        cloneRenderer.sortingLayerID = ownerRenderer.sortingLayerID;
        cloneRenderer.sortingOrder = ownerRenderer.sortingOrder;
        cloneRenderer.sharedMaterial = ownerRenderer.sharedMaterial;
        transform.localScale = ownerShip.transform.lossyScale;
    }

    private void CreateTriggerCollider()
    {
        CircleCollider2D trigger = gameObject.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;

        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>(true);
        if (renderer != null && renderer.sprite != null)
        {
            Bounds bounds = renderer.sprite.bounds;
            trigger.radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
        }
    }

    private void RemoveCloneShipComponents()
    {
        ParentShip cloneShip = GetComponent<ParentShip>();
        if (cloneShip != null)
            cloneShip.enabled = false;

        WeaponController cloneWeaponController = GetComponent<WeaponController>();
        if (cloneWeaponController != null)
            cloneWeaponController.enabled = false;

        ActiveAbility[] activeAbilities = GetComponentsInChildren<ActiveAbility>(true);
        for (int i = 0; i < activeAbilities.Length; i++)
            activeAbilities[i].enabled = false;

        PassiveAbility[] passiveAbilities = GetComponentsInChildren<PassiveAbility>(true);
        for (int i = 0; i < passiveAbilities.Length; i++)
            passiveAbilities[i].enabled = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleColliderEntered(collision.collider);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        HandleColliderExited(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleColliderEntered(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        HandleColliderExited(other);
    }

    private void HandleColliderEntered(Collider2D other)
    {
        if (other == null)
            return;

        if (destroyOnEnemyProjectile
            && other.GetComponentInParent<EnemyProjectile>() != null)
        {
            Destroy(gameObject);
            return;
        }

        if (!suppressShootingInsideEnemy
            || other.GetComponentInParent<Enemy>() == null)
        {
            return;
        }

        enemyOverlapCount++;
        if (enemyOverlapCount == 1)
            SetCloneWeaponsAbleToShoot(false);
    }

    private void HandleColliderExited(Collider2D other)
    {
        if (!suppressShootingInsideEnemy
            || other == null
            || other.GetComponentInParent<Enemy>() == null)
        {
            return;
        }

        enemyOverlapCount = Mathf.Max(0, enemyOverlapCount - 1);
        if (enemyOverlapCount == 0)
            SetCloneWeaponsAbleToShoot(true);
    }

    private void SetCloneWeaponsAbleToShoot(bool ableToShoot)
    {
        for (int i = 0; i < cloneWeapons.Count; i++)
        {
            if (cloneWeapons[i] != null)
                cloneWeapons[i].AbleToShoot(ableToShoot);
        }
    }

    private void UnregisterCloneWeapons()
    {
        if (ownerWeaponController != null)
        {
            for (int i = 0; i < cloneWeapons.Count; i++)
            {
                if (cloneWeapons[i] != null)
                    ownerWeaponController.UnregisterExternalWeapon(cloneWeapons[i]);
            }
        }

        cloneWeapons.Clear();
    }

    private void OnDestroy()
    {
        UnregisterCloneWeapons();
    }

    private void OnDisable()
    {
        UnregisterCloneWeapons();
        enemyOverlapCount = 0;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }
}
