using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public class TailCreateAbility : ActiveAbility
{
    [InjectOptional] private DiContainer container;

    float baseMaxHealth;
    float baseMaxShield;
    int tailsCreated;

    [Header("Tail")]
    [SerializeField] GameObject tailPrefab;
    [SerializeField, Min(0.05f)] private float tailSpacing = 0.5f;
    [SerializeField, Min(0.001f)] private float baseFollowSmoothTime = 0.04f;
    [SerializeField, Min(0f)] private float followSmoothTimePerSegment = 0.025f;
    [SerializeField, Min(0.001f)] private float maxFollowSpeed = 30f;

    WeaponController controller;
    List<Weapon> weapons;
    ParentShip parentShip;

    private readonly List<GameObject> createdTails = new List<GameObject>();
    private readonly List<Weapon> createdTailWeapons = new List<Weapon>();
    private Coroutine refreshWeaponsCoroutine;
    private bool tailsVisible;

    public override bool Activate(ParentShip owner)
    {
        if (owner == null || !owner.IsWeaponLevelMax || tailPrefab == null)
            return false;

        parentShip ??= owner;
        owner.SetLevel(ParentShip.MinWeaponLevel);

        Transform followTarget = createdTails.Count == 0
            ? owner.transform
            : createdTails[^1].transform;
        GameObject newTail = Instantiate(tailPrefab);
        TailSegmentFollower follower =
            newTail.GetComponent<TailSegmentFollower>()
            ?? newTail.AddComponent<TailSegmentFollower>();
        follower.Configure(
            followTarget,
            -tailSpacing,
            baseFollowSmoothTime
                + followSmoothTimePerSegment * createdTails.Count,
            maxFollowSpeed);

        createdTails.Add(newTail);
        SetTailVisible(newTail, parentShip.IsVisible);
        tailsCreated++;

        foreach (Weapon weapon in weapons.Take(2))
        {
            Weapon newWeapon = Instantiate(weapon, newTail.transform);
            container?.InjectGameObject(newWeapon.gameObject);

            newWeapon.transform.localPosition = weapon.transform.localPosition;
            newWeapon.transform.localRotation = weapon.transform.localRotation;
            newWeapon.transform.localScale = weapon.transform.localScale;
            createdTailWeapons.Add(newWeapon);
            controller.RegisterExternalWeapon(newWeapon);

            if (parentShip.IsVisible)
                newWeapon.ShowWeapon();
            else
                newWeapon.HideWeapon();
        }

        controller.RefreshWeaponOwners();

        parentShip.AddMaxHealthPoints(baseMaxHealth);
        parentShip.AddMaxShieldPoints(baseMaxShield);

        return true;
    }

    private void LateUpdate()
    {
        if (createdTails.Count == 0)
            return;

        bool shouldBeVisible = parentShip != null && parentShip.IsVisible;

        if (tailsVisible != shouldBeVisible)
            SetTailsVisible(shouldBeVisible);
    }

    private void SetTailsVisible(bool isVisible)
    {
        for (int i = 0; i < createdTails.Count; i++)
            SetTailVisible(createdTails[i], isVisible);

        tailsVisible = isVisible;

        if (isVisible)
        {
            for (int i = 0; i < createdTails.Count; i++)
            {
                if (createdTails[i] == null)
                    continue;

                TailSegmentFollower follower =
                    createdTails[i].GetComponent<TailSegmentFollower>();
                follower?.SnapYToTarget();
            }

            controller?.RefreshWeaponOwners();
            controller?.ShowWeapons();
        }
    }

    private static void SetTailVisible(GameObject tail, bool isVisible)
    {
        if (tail != null)
            tail.SetActive(isVisible);
    }

    private void ClearTails()
    {
        for (int i = 0; i < createdTailWeapons.Count; i++)
            controller?.UnregisterExternalWeapon(createdTailWeapons[i]);

        createdTailWeapons.Clear();

        for (int i = 0; i < createdTails.Count; i++)
        {
            if (createdTails[i] != null)
                Destroy(createdTails[i]);
        }

        createdTails.Clear();

        if (parentShip != null && tailsCreated > 0)
        {
            parentShip.AddMaxHealthPoints(-baseMaxHealth * tailsCreated);
            parentShip.AddMaxShieldPoints(-baseMaxShield * tailsCreated);
        }

        tailsCreated = 0;

        if (refreshWeaponsCoroutine != null)
            StopCoroutine(refreshWeaponsCoroutine);

        if (isActiveAndEnabled)
            refreshWeaponsCoroutine = StartCoroutine(RefreshWeaponsAfterTailCleanup());
        else
            controller?.UpdateWeapons();
    }

    private IEnumerator RefreshWeaponsAfterTailCleanup()
    {
        yield return null;
        controller?.UpdateWeapons();
        refreshWeaponsCoroutine = null;
    }

    private void OnDestroy()
    {
        ClearTails();
    }

    public void Init(Centipede owner)
    {
        weapons = owner.Weapons;
        parentShip = GetComponent<ParentShip>();
        controller = GetComponent<WeaponController>();
        baseMaxHealth = parentShip.MaximumHealthPoints;
        baseMaxShield = parentShip.MaximumShieldPoints;
        tailsVisible = parentShip.IsVisible;
    }
}
