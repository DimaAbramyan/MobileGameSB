using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AbilityDamageVerificationEnemy : Enemy
{
    public override void Awake() { }
    public override void Dying() { isDead = true; }
}

public static class VerifyCatalogAbilitiesAndDamage
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static Scene scene;
    private static DealDamageManager damage;
    private static readonly List<DamageNumberRequest> numbers = new List<DamageNumberRequest>();
    private static readonly Vector3 Origin = new Vector3(10000f, 10000f);

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        checks = 0; numbers.Clear();
        damage = new DealDamageManager(); damage.DamageApplied += numbers.Add;
        var previous = SceneManager.GetActiveScene();
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            VerifyCatalog(); VerifyContracts(); VerifyDamageNumbers(); VerifyBlade(); VerifyArkanoid(); VerifyOrbit();
            return new { passed = checks, hulls = 8, prefabs = 14, damageTypes = 4,
                bladeSweptDamage = true, damageNumbersAllPaths = true, arkanoidPaddleAttractionAfterTopOnly = true };
        }
        finally
        {
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<AbilityDamageVerificationEnemy>(FindObjectsSortMode.None))
                if (enemy.gameObject.scene == scene) Undo.ClearUndo(enemy);
            SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static GameObject ObjectAt(string name, Vector3 position)
    {
        var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); go.transform.position = position; return go;
    }
    private static Enemy EnemyAt(Vector3 position, bool shield = false)
    {
        var go = ObjectAt("Ability damage target", position);
        var enemy = go.AddComponent<AbilityDamageVerificationEnemy>();
        Set(enemy, "_maxHealth", 1000f); enemy._currentHealth = 1000f;
        Set(enemy, "damageNumberManager", damage);
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .1f;
        var child = ObjectAt("Duplicate enemy collider", position); child.transform.SetParent(go.transform, true);
        child.AddComponent<CircleCollider2D>().radius = .05f;
        if (shield)
        {
            Set(enemy, "shieldPoints", 100f);
            var modifier = go.AddComponent<EnemyShieldModifier>();
            Set(modifier, "showShieldVisual", false); Set(modifier, "enemy", enemy);
            Set(modifier, "currentShieldPoints", 100f); Set(enemy, "shieldModifier", modifier);
        }
        return enemy;
    }
    private static ParentShip OwnerAt(Vector3 position)
    {
        var owner = ObjectAt("Ability owner", position).AddComponent<ParentShip>(); owner.IsVisible = true; return owner;
    }
    private static void VerifyCatalog()
    {
        var counts = new Dictionary<string, int> { {"Blade",2},{"Black Hole",2},{"Prism",2},{"Arkanoid",2},{"Ghost",2},
            {"Dictator",3},{"Orbit",3},{"Shield",3} };
        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>("Assets/GameContent/Catalogs/HullCatalog.asset");
        Check(catalog.Hulls.Count == counts.Count, "Catalog coverage");
        foreach (var hull in catalog.Hulls)
        {
            int count = counts[hull.DisplayName];
            Check(hull.Data.maximumWeaponCount == count, "ShipData platform limit: " + hull.DisplayName);
            var ids = new HashSet<string>();
            foreach (var prefab in new[] { hull.GameplayPrefab, hull.Prefab })
            {
                var definition = prefab.GetComponent<HullLoadoutDefinition>();
                Check(definition != null && definition.WeaponPlatforms.Count == count, "Physical count: " + prefab.name);
                Check(definition.IsConfigurationValid(out string error), "Valid loadout: " + error);
                Check(definition.ShipData == hull.Data, "Matching ShipData: " + prefab.name);
                var actual = new HashSet<string>(); var mounts = new HashSet<Transform>();
                foreach (var platform in definition.WeaponPlatforms)
                { actual.Add(platform.SlotId); mounts.Add(platform.WeaponMount); }
                Check(mounts.Count == count, "Distinct physical mounts: " + prefab.name);
                if (ids.Count == 0) ids.UnionWith(actual);
                else Check(ids.SetEquals(actual), "Blueprint/gameplay SlotIds agree: " + hull.DisplayName);
                foreach (var anchor in prefab.GetComponentsInChildren<CraftWeaponSlotAnchor>(true))
                    Check(actual.Contains(anchor.SlotId) && mounts.Contains(anchor.WeaponMount), "No obsolete blueprint anchors");
            }
            var circle = hull.GameplayPrefab.GetComponent<CircleCollider2D>();
            Check(circle != null && Mathf.Approximately(circle.radius, .05f), "Physical hitbox preserved");
        }
    }
    private static void VerifyContracts()
    {
        var blade = new BladeShipMetaContract(); Set(blade, "damageType", EnemyDamageType.Chemical);
        Check(((BladeShipMetaContract)blade.Clone()).DamageType == EnemyDamageType.Chemical, "Blade clone preserves type");
        var ball = new ArkanoidShipMetaContract(); Set(ball, "ballDamageType", EnemyDamageType.Kinetick); Set(ball, "stasisDamageType", EnemyDamageType.Plazma);
        var clone = (ArkanoidShipMetaContract)ball.Clone();
        Check(clone.BallDamageType == EnemyDamageType.Kinetick && clone.StasisDamageType == EnemyDamageType.Plazma, "Arkanoid clone preserves both types");
        Set(ball, "paddleAttractionRadius", 3f); Set(ball, "paddleAttractionForce", 7f);
        clone = (ArkanoidShipMetaContract)ball.Clone();
        Near(clone.PaddleAttractionRadius, 3f, "Arkanoid clone preserves paddle attraction radius");
        Near(clone.PaddleAttractionForce, 7f, "Arkanoid clone preserves paddle attraction force");
        var orbit = new OrbitShipMetaContract(); Set(orbit, "damageType", EnemyDamageType.Chemical);
        Check(((OrbitShipMetaContract)orbit.Clone()).DamageType == EnemyDamageType.Chemical, "Orbit clone preserves type");
        var legacy = new BlackHoleShipMetaContract(); Set(legacy, "damageType", EnemyDamageType.Plazma);
        Check(((BlackHoleShipMetaContract)legacy.Clone()).DamageType == EnemyDamageType.Plazma, "Legacy BlackHole clone preserves type");
        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>("Assets/GameContent/Catalogs/HullCatalog.asset");
        foreach (var hull in catalog.Hulls)
            foreach (var level in hull.Data.ShipMetaConfig.Levels)
                foreach (var contract in level.Contracts)
                {
                    if (contract is BladeShipMetaContract b) Check(b.DamageType == EnemyDamageType.Default, "Old Blade damage unchanged");
                    if (contract is ArkanoidShipMetaContract a) Check(a.BallDamageType == EnemyDamageType.Default && a.StasisDamageType == EnemyDamageType.Default, "Old Arkanoid damage unchanged");
                    if (contract is OrbitShipMetaContract o) Check(o.DamageType == EnemyDamageType.Default, "Old Orbit damage unchanged");
                }
    }
    private static void VerifyDamageNumbers()
    {
        var controller = ObjectAt("Damage numbers integration", Origin).AddComponent<DamageNumberController>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/UI/Common/DamageNumbers/DamageNumber.prefab");
        controller.Configure(prefab.GetComponent<DamageNumberView>(), controller.transform); controller.Construct(damage);
        var enemy = EnemyAt(Origin + Vector3.right * 100f, true);
        var shield = enemy.GetComponent<EnemyShieldModifier>();
        foreach (var type in new[] { EnemyDamageType.Default, EnemyDamageType.Plazma, EnemyDamageType.Kinetick, EnemyDamageType.Chemical })
            foreach (bool shielded in new[] { false, true })
            {
                enemy._currentHealth = 1000f; shield.SetShieldPoints(shielded ? 100f : 0f); numbers.Clear();
                var result = damage.DealDamage(enemy, null, 10f, type);
                Check(numbers.Count == 1, "Exactly one managed damage number: " + type);
                Near(numbers[0].Damage, result.TotalDamage, "Actual managed damage displayed");
                float expected = shielded && type != EnemyDamageType.Chemical
                    ? 10f * EnemyDamageProfiles.Get(type).ShieldMultiplier : 10f * EnemyDamageProfiles.Get(type).HullMultiplier;
                Near(result.TotalDamage, expected, "Configured type multiplier");
                Near(numbers[0].ModifierPercent, (expected / 10f - 1f) * 100f, "Damage color modifier retained");
                Check(numbers[0].Target == enemy, "Damage number tracks target");
            }
        enemy._currentHealth = 1000f; shield.SetShieldPoints(100f); numbers.Clear(); enemy.TakeDamage(10f);
        Check(numbers.Count == 1 && numbers[0].Damage == 10f && enemy._currentHealth == 1000f, "Direct shield-only damage number");
        numbers.Clear(); enemy.TakeDamageWithType(10f, EnemyDamageType.Chemical);
        Check(numbers.Count == 1 && numbers[0].Damage == 10f && enemy._currentHealth == 990f, "Direct typed hull damage number");
        numbers.Clear(); enemy.TakeDamageIgnoringShield(10f);
        Check(numbers.Count == 1 && numbers[0].Damage == 10f, "Direct bypass damage number");
        numbers.Clear(); damage.DealDamage(enemy, null, 0f, EnemyDamageType.Default);
        Check(numbers.Count == 0, "No popup for zero damage");
        shield.SetShieldPoints(100f); enemy._currentHealth = 1000f; numbers.Clear();
        damage.DealDebuffDamage(enemy, null, 10f, EnemyDamageType.Default);
        Check(numbers.Count == 1 && enemy._currentHealth == 990f && shield.CurrentShieldPoints == 100f, "Debuff hull damage popup");
        numbers.Clear(); damage.DealDebuffDamage(enemy, null, 10f, EnemyDamageType.Default, true);
        Check(numbers.Count == 1 && shield.CurrentShieldPoints == 90f, "Debuff shield-first popup");
        numbers.Clear(); enemy._currentHealth = 5f; enemy.TakeDamageIgnoringShield(10f);
        Check(enemy.isDead && numbers.Count == 1, "Lethal hit displays damage");
        var active = (IDictionary)Get(controller, "activeNumbers");
        Check(active.Count > 0, "Existing DamageNumberController creates visible number objects");
        Check(controller.GetComponentsInChildren<DamageNumberView>(true).Length > 0, "Existing UI prefab instantiated");
        enemy.isDead = false; enemy._currentHealth = 1000f; shield.SetShieldPoints(0f); numbers.Clear();
        var projectile = ObjectAt("Weapon projectile damage verification", Origin).AddComponent<Projectile>();
        projectile.SetDamage(10f);
        damage.DealDamage(enemy, projectile);
        Check(numbers.Count == 1, "Weapon projectile creates one popup without duplicates");
        Near(numbers[0].Damage, 13.5f, "Weapon projectile keeps Kinetick hull multiplier");
        var prismObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Ships/Prism.prefab"));
        SceneManager.MoveGameObjectToScene(prismObject, scene);
        var prism = prismObject.GetComponent<ParentShip>();
        prism.Awake(); prism.PassiveAbility.Init(prism); prism.ShowShip();
        enemy._currentHealth = 1000f; numbers.Clear();
        damage.DealDamage(enemy, prism, 100f, EnemyDamageType.Plazma);
        Near(numbers[0].Damage, 74.75f, "Outgoing passive damage bonus retained");
        Near(numbers[0].ModifierPercent, -25.25f, "Popup modifier includes outgoing bonus once");
    }
    private static void VerifyBlade()
    {
        Vector3 start = Origin + Vector3.right * 200f;
        var first = EnemyAt(start); var second = EnemyAt(start + Vector3.up); var outside = EnemyAt(start + Vector3.right * 3f);
        var owner = OwnerAt(start + Vector3.down * 5f);
        var trail = ObjectAt("Blade sweep verification", start).AddComponent<MaterializedBladeTrail>();
        Physics2D.SyncTransforms(); numbers.Clear();
        trail.Init(new[] { (Vector2)(start + Vector3.left * .5f), (Vector2)(start + Vector3.right * .5f) },
            Vector2.up, .2f, 100f, 50f, 10f, owner, 1f, null, damage, EnemyDamageType.Default);
        Near(first._currentHealth, 950f, "Blade damages static enemy at initial overlap");
        Check(numbers.Count == 1, "Blade duplicate colliders create one popup");
        trail.transform.position += Vector3.up * 2f; Physics2D.SyncTransforms(); Invoke(trail, "FixedUpdate");
        Near(second._currentHealth, 950f, "Fast Blade sweeps through enemy between frames");
        Near(first._currentHealth, 950f, "Blade never damages same enemy twice");
        Near(outside._currentHealth, 1000f, "Blade does not hit outside its shape");
        Check(numbers.Count == 2, "One damage number per Blade target");
        Invoke(trail, "FixedUpdate"); Check(numbers.Count == 2, "No repeated Blade damage event");
        Check(trail.GetComponent<Rigidbody2D>().useFullKinematicContacts, "Blade supports kinematic/static contacts");
        Vector3 stationaryPosition = start + Vector3.right * 10f;
        var target = EnemyAt(stationaryPosition);
        var shipObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Ships/BladeShip.prefab"));
        SceneManager.MoveGameObjectToScene(shipObject, scene); shipObject.transform.position = stationaryPosition;
        var ship = shipObject.GetComponent<ParentShip>();
        var ability = ship.ActiveAbility as CreateBladeAbility; Set(ability, "damageManager", damage);
        ((List<Vector2>)Get(ability, "savedPoints")).Add(stationaryPosition);
        Physics2D.SyncTransforms(); ability.SpawnBlade(ship);
        Check(target._currentHealth < 1000f, "Stationary activation creates a damaging Blade");
    }
    private static void VerifyArkanoid()
    {
        Vector3 center = Origin + Vector3.right * 300f;
        var owner = OwnerAt(center);
        var magnetObject = ObjectAt("Arkanoid owner magnet", center); magnetObject.transform.SetParent(owner.transform, true);
        magnetObject.AddComponent<CircleCollider2D>().isTrigger = true;
        var magnet = magnetObject.AddComponent<Magnite>(); Invoke(magnet, "Awake");
        var ballObject = ObjectAt("Arkanoid verification", center + Vector3.up);
        ballObject.AddComponent<CircleCollider2D>().radius = .1f;
        var rb = ballObject.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
        var ball = ballObject.AddComponent<ArkanoidBall>(); Invoke(ball, "Awake"); Set(ball, "damageManager", damage);
        var paddleObject = ObjectAt("Arkanoid actual paddle", center + Vector3.right * 2f);
        paddleObject.AddComponent<BoxCollider2D>().size = new Vector2(1f, .2f);
        var paddle = paddleObject.AddComponent<ArkanoidPaddle>(); Invoke(paddle, "Awake");
        ball.Configure(owner, paddle, 5f, 10f, Vector2.up);
        var camera = ObjectAt("Arkanoid test camera", center + Vector3.back * 10f).AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 5f; camera.aspect = 1f; Set(ball, "mainCamera", camera);
        ball.ResetAndLaunch(center + Vector3.up, Vector2.up);
        Check(!ball.CanBeAttractedToPaddle, "No paddle attraction before top bounce");
        Invoke(ball, "TryApplyPaddleAttraction", .02f);
        Check((Vector2)Get(ball, "direction") == Vector2.up, "Nearby paddle cannot steer before top bounce");
        Invoke(ball, "BounceFromPaddle", paddle, true);
        Check(!ball.CanBeAttractedToPaddle, "Paddle bounce does not unlock attraction");
        ball.ResetAndLaunch(center + Vector3.up * 5f, Vector2.down);
        Invoke(ball, "BounceFromScreenBounds");
        Check(!ball.CanBeAttractedToPaddle, "Clamping above top while descending is not a top bounce");
        ball.ResetAndLaunch(center + Vector3.up, Vector2.up);
        ball.transform.position = center + Vector3.right * 5f; Invoke(ball, "BounceFromScreenBounds");
        Check(!ball.CanBeAttractedToPaddle, "Side bounce does not unlock attraction");
        ball.transform.position = center + Vector3.up * 5f; Invoke(ball, "BounceFromScreenBounds");
        Check(ball.CanBeAttractedToPaddle && ((Vector2)Get(ball, "direction")).y < 0f, "Top bounce unlocks attraction");
        ball.transform.position = center + Vector3.right + Vector3.up; Set(ball, "direction", Vector2.down);
        Physics2D.SyncTransforms(); Invoke(magnet, "FixedUpdate");
        Check((Vector2)Get(ball, "direction") == Vector2.down, "Pickup magnet has no effect even after top bounce");
        Invoke(ball, "FixedUpdate");
        Check(((Vector2)Get(ball, "direction")).x > 0f, "Physics tick steers toward paddle, away from owner magnet");
        Near(rb.linearVelocity.magnitude, ball.CurrentSpeed, "Paddle attraction preserves ball speed");
        for (int cycle = 0; cycle < 3; cycle++)
        {
            Invoke(ball, "BounceFromPaddle", paddle, true);
            Check(!ball.CanBeAttractedToPaddle, "Paddle hit immediately disables attraction: " + cycle);
            Vector2 outgoingDirection = (Vector2)Get(ball, "direction");
            Invoke(ball, "TryApplyPaddleAttraction", .02f);
            Check((Vector2)Get(ball, "direction") == outgoingDirection, "Outgoing ball is not attracted after paddle hit: " + cycle);
            ball.transform.position = center + Vector3.right * 5f;
            Invoke(ball, "BounceFromScreenBounds");
            Check(!ball.CanBeAttractedToPaddle, "Side bounce cannot re-enable attraction: " + cycle);
            ball.transform.position = center + Vector3.up * 5f;
            Invoke(ball, "BounceFromScreenBounds");
            Check(ball.CanBeAttractedToPaddle, "Next top bounce re-enables attraction: " + cycle);
        }
        ball.transform.position = center + Vector3.left * 5f; Set(ball, "direction", Vector2.down);
        Invoke(ball, "TryApplyPaddleAttraction", .02f);
        Check((Vector2)Get(ball, "direction") == Vector2.down, "Attraction respects independent paddle radius");
        ball.transform.position = center + Vector3.right + Vector3.up;
        paddle.gameObject.SetActive(false);
        Invoke(ball, "TryApplyPaddleAttraction", .02f);
        Check(!ball.CanBeAttractedToPaddle && (Vector2)Get(ball, "direction") == Vector2.down, "Inactive paddle cannot attract");
        paddle.gameObject.SetActive(true);
        owner.IsVisible = false; Invoke(ball, "TryApplyPaddleAttraction", .02f);
        Check((Vector2)Get(ball, "direction") == Vector2.down, "Inactive owner cannot attract"); owner.IsVisible = true;
        ball.TryActivateStasis();
        Invoke(ball, "TryApplyPaddleAttraction", .02f);
        Check(!ball.CanBeAttractedToPaddle && (Vector2)Get(ball, "direction") == Vector2.down, "Stasis cannot be moved by paddle attraction");
        Invoke(ball, "EndStasis");
        Invoke(ball, "LoseBall"); Check(!ball.CanBeAttractedToPaddle, "Ball loss disables attraction");
        Invoke(ball, "RespawnBall"); Check(!ball.CanBeAttractedToPaddle, "Respawn resets top bounce gate");
        ball.ResetAndLaunch(center + Vector3.up, Vector2.up);
        Check(!ball.CanBeAttractedToPaddle, "Every launch resets paddle attraction gate");
        ball.transform.position = center + Vector3.up * 5f; Invoke(ball, "BounceFromScreenBounds");
        ball.transform.position = center + Vector3.right * 3f; Set(ball, "direction", Vector2.right);
        Invoke(ball, "TryApplyPaddleAttraction", .02f);
        Check(Mathf.Abs(((Vector2)Get(ball, "direction")).y) > .001f, "Opposite direction turns instead of cancelling acceleration");
        var contract = new ArkanoidShipMetaContract(); Set(contract, "ballDamageType", EnemyDamageType.Chemical);
        Set(contract, "stasisDamageType", EnemyDamageType.Plazma); ball.ApplyShipMetaStats(contract);
        ball.transform.position = center + Vector3.right * 3f; Physics2D.SyncTransforms();
        var target = EnemyAt(ball.transform.position, true); Physics2D.SyncTransforms(); numbers.Clear();
        Invoke(ball, "DealContactDamage", target);
        Near(target._currentHealth, 990f, "Arkanoid contact uses configured Chemical damage");
        Near(target.GetComponent<EnemyShieldModifier>().CurrentShieldPoints, 100f, "Chemical contact bypasses shield");
        Check(numbers.Count == 1, "Ball contact produces one number");
        target._currentHealth = 1000f; numbers.Clear(); ball.TryActivateStasis(); Physics2D.SyncTransforms();
        Invoke(ball, "DamageEnemiesInStasis");
        Near(target.GetComponent<EnemyShieldModifier>().CurrentShieldPoints, 95.95f, "Stasis uses configured Plazma damage");
        Check(numbers.Count == 1, "Stasis duplicate colliders produce one hit and number");
    }
    private static void VerifyOrbit()
    {
        var owner = OwnerAt(Origin + Vector3.right * 400f);
        var orbit = owner.gameObject.AddComponent<OrbitPassiveAbility>(); orbit.Init(owner);
        Set(orbit, "damageManager", damage);
        var settings = new OrbitShipMetaContract(); Set(settings, "damageType", EnemyDamageType.Chemical);
        Set(orbit, "settings", settings); Set(orbit, "isActive", true);
        var target = EnemyAt(owner.transform.position + Vector3.right, true); numbers.Clear();
        Invoke(orbit, "TryDamageEnemy", target);
        Near(target._currentHealth, 998f, "Orbit uses configured Chemical damage");
        Check(numbers.Count == 1, "Orbit creates damage number");
        Invoke(orbit, "TryDamageEnemy", target); Check(numbers.Count == 1, "Orbit retains shared hit interval");
    }
    private static FieldInfo Field(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, Private | BindingFlags.DeclaredOnly); if (field != null) return field; }
        throw new Exception("Field not found: " + name);
    }
    private static object Get(object target, string name) => Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Invoke(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, Private).Invoke(target, arguments);
    private static void Near(float actual, float expected, string name) => Check(Mathf.Abs(actual - expected) < .01f, name + ": " + actual + " != " + expected);
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
}
