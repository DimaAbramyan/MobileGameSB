using UnityEngine;
using System;

public class Weapon : MonoBehaviour
{
    [SerializeField] protected Projectile projectilePrefab;
    [SerializeField] protected Transform projectileSpawn;
    [SerializeField] protected WeaponData weaponData;

    private ParentShip owner;
    private SpriteRenderer spriteRenderer;

    protected float reloadTime;
    protected float currentReloadTime;
    protected int level;
    protected float maxAngle;
    protected Enemy target;

    private bool ableToShoot;

    public event Action<int> OnLevelChanged;

    public int Level => level;
    public Enemy Target => target;

    public void HideWeapon()
    {
        ableToShoot = false;
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
    }

    public void ShowWeapon()
    {
        ableToShoot = true;
        if (spriteRenderer != null)
            spriteRenderer.enabled = true;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (owner != null)
        {
            owner.OnLevelChanged += HandleLevelChanged;
        }

        reloadTime = weaponData.reloadTimeByLevel[level];
        currentReloadTime = reloadTime;
    }
    private void HandleLevelChanged(int newLevel)
    {
        SetLevel(newLevel);
    }

    public void SetOwner(ParentShip ownerShip)
    {
        owner = ownerShip;
    }

    public bool TryToShoot()
    {
        if (!ableToShoot) return false;

        currentReloadTime -= Time.deltaTime;
        if (currentReloadTime <= 0f)
        {
            ShootProjectile();
            currentReloadTime = reloadTime;
            return true;
        }

        return false;
    }

    private void ShootProjectile()
    {
        if (projectilePrefab == null || projectileSpawn == null) return;

        ProjectileParams param = new ProjectileParams
        {
            speed = weaponData.speedByLevel[level],
            damage = weaponData.damageByLevel[level],
            maxLength = weaponData.rangeByLevel[level],
            direction = transform.up,
            maxAngle = weaponData.angleByLevel[level],
        };

        Projectile proj = Instantiate(projectilePrefab, projectileSpawn.position, Quaternion.identity);
        proj.Init(param,
                  weaponData.movementStrategy,
                  weaponData.impactBehavior,
                  weaponData.continiousImpactBehavior,
                  weaponData.projectileBehaviour,
                  owner);
    }

    public void Reload(float multiplier)
    {
        currentReloadTime = reloadTime * multiplier;
    }

    public void SetLevel(int newLevel)
    {
        level = Mathf.Clamp(newLevel, 0, weaponData.reloadTimeByLevel.Count - 1);
        reloadTime = weaponData.reloadTimeByLevel[level];
        currentReloadTime = reloadTime;

        OnLevelChanged?.Invoke(level);
    }
    public void AbleToShoot(bool newAble)
    {
        if (!spriteRenderer.isVisible)
        {
        ableToShoot = false;
        return;
        }
        ableToShoot = newAble;
    }
}