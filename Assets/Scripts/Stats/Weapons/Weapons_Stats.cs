using UnityEngine;
using System;
using Zenject;

public class Weapon : MonoBehaviour
{
    [Inject] DiContainer container;

    [SerializeField] protected Projectile projectilePrefab;
    [SerializeField] protected Transform projectileSpawn;
    [SerializeField] public  WeaponData weaponData;

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

        reloadTime = weaponData.ReloadTimeByLevel[level];
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
            speed = weaponData.SpeedByLevel[level],
            damage = weaponData.DamageByLevel[level],
            maxLength = weaponData.RangeByLevel[level],
            direction = transform.up,
            maxAngle = weaponData.AngleByLevel[level],
        };
        Projectile proj = Instantiate(projectilePrefab, projectileSpawn.position, Quaternion.identity);
        proj.Init(param,
                  weaponData.MovementStrategy,
                  weaponData.ImpactBehavior,
                  weaponData.ContiniousImpactBehavior,
                  weaponData.ProjectileBehaviour,
                  owner);
    }

    public void Reload(float multiplier)
    {
        currentReloadTime = reloadTime * multiplier;
    }

    public void SetLevel(int newLevel)
    {
        level = Mathf.Clamp(newLevel, 0, weaponData.ReloadTimeByLevel.Count - 1);
        reloadTime = weaponData.ReloadTimeByLevel[level];
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