using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyAttackSequenceChildren
{
    private static int checks;
    public static object Run()
    {
        checks = 0;
        var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        var tracker = new EnemyAttackCompletionTracker();
        using (var world = new EnemyProjectilePreviewWorld())
        {
            var child = Child(.2f);
            Set(child.Pattern, "attackShotCount", 2); Set(child.Pattern, "attackShotInterval", .2f);
            Spawn(world, bullet, child, tracker);
            Check(tracker.PendingChildAttacks == 1, "Parent registers pending child attack");
            world.Step(.1f, .1f, Vector3.down * 4f); Check(!tracker.IsComplete, "Delayed trigger is not complete");
            world.Step(.3f, .2f, Vector3.down * 4f); Check(!tracker.IsComplete && world.Count == 2, "First child volley does not finish Burst");
            world.Step(.6f, .3f, Vector3.down * 4f); Check(tracker.IsComplete && world.Count == 3, "Completion tracks scheduling, not bullet lifetime");
        }
        using (var world = new EnemyProjectilePreviewWorld())
        {
            var child = Child(0f); var nested = Child(.4f); child.Pattern.Shooting.SetChildAttack(nested);
            Spawn(world, bullet, child, tracker);
            world.Step(.1f, .1f, Vector3.down * 4f);
            Check(!tracker.IsComplete && tracker.PendingChildAttacks == 1, "Nested child inherits completion scope");
            world.Step(.6f, .5f, Vector3.down * 4f);
            Check(tracker.IsComplete && world.Count == 3, "Nested completion releases scope");
        }
        using (var world = new EnemyProjectilePreviewWorld())
        {
            Spawn(world, bullet, Child(10000f), tracker);
            world.Step(1f, 1f, Vector3.down * 4f);
            Check(!tracker.IsComplete, "Long effect remains tracked for explicit timeout gate");
        }
        Check(tracker.IsComplete, "World disposal releases completion counters");
        return new { passed = true, checks, delayedChildren = true, nestedChildren = true, cleanup = true };
    }
    private static EnemyChildAttackSettings Child(float delay)
    {
        var child = new EnemyChildAttackSettings(); Set(child, "triggerValue", delay);
        Set(child.Pattern, "attackCooldown", 0f);
        Set(child.Pattern.Shooting, "overrideProjectileLifetime", true); Set(child.Pattern.Shooting, "projectileLifetime", 100f);
        return child;
    }
    private static void Spawn(EnemyProjectilePreviewWorld world, EnemyBullet bullet, EnemyChildAttackSettings child, EnemyAttackCompletionTracker tracker)
    {
        world.Spawn(new List<EnemyProjectileSpawnRequest> { new EnemyProjectileSpawnRequest(bullet, Vector3.zero, Vector3.down,
            1f, 1f, EnemyProjectileSpeedBehavior.None, 0f, null, EnemyProjectileSizeBehavior.Default, 0f, 1f, null, 1f,
            Vector3.zero, EnemyProjectileMovementPattern.Default, 0f, 0f, EnemyProjectileOrbitCenterMode.SourcePosition,
            Vector2.zero, 0f, 0f, true, 100f,
            childAttack: child, childPrefabs: new EnemyProjectilePrefabSet(bullet), completionTracker: tracker) });
    }
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Set(object value, string name, object field) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(value, field);
}
