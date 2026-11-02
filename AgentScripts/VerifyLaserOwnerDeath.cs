using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using Unity.Rendering;
using UnityEditor;
using UnityEngine;
public static class VerifyLaserOwnerDeath
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object Get(object o, string n) => o.GetType().GetField(n, F).GetValue(o);
    public static object Run()
    {
        int checks = 0;
        foreach (bool active in new[] { false, true }) foreach (string death in new[] { "dead", "disabled", "destroyed" })
        {
            var owner = new GameObject("Laser owner death check"); var enemy = owner.AddComponent<Enemy>();
            var cameraObject = new GameObject("Laser bounds"); var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = .6f;
            camera.transform.position = new Vector3(0, 0, -10);
            try
            {
                using var world = new World("Laser owner death check"); var manager = world.EntityManager;
                using var runtime = new EnemyLaserRuntime(manager);
                var settings = new EnemyShootingSettings();
                JsonUtility.FromJsonOverwrite("{\"projectileType\":3,\"laser\":{\"surviveSourceDestruction\":true,\"telegraphDuration\":1,\"geometry\":{\"lengthMode\":1,\"laserLength\":20,\"laserWidth\":0.2}}}", settings);
                settings.SetScreenBoundary(new EnemyProjectileScreenSettings(false, bounce:true));
                var requests = new List<EnemyProjectileSpawnRequest>();
                EnemyAttackProjectileBuilder.AppendEmitter(requests, null, settings, Vector3.zero, Vector3.zero, Vector3.right, 1, 1, 0, default,
                    laserOrigin: new EnemyLaserOrigin(owner.transform, Vector3.zero, settings.EmissionOrigin, camera));
                var entity = manager.Instantiate(runtime.Template); runtime.Register(entity, requests[0], 0);
                float time = active ? 1.1f : .1f; runtime.Tick(time, camera);
                var beam = ((IList)Get(runtime, "beams"))[0]; var visuals = ((List<Entity>)Get(beam, "VisualSegments")).ToArray();
                if (visuals.Length == 0) throw new Exception("Expected reflected segments");
                if (death == "dead") enemy.isDead = true;
                else if (death == "disabled") owner.SetActive(false);
                else UnityEngine.Object.DestroyImmediate(owner);
                runtime.Tick(time + .02f, camera);
                var state = manager.GetComponentData<EnemyLaserState>(entity);
                if (runtime.Count != 0 || state.Cancelled == 0 || state.Active != 0 || state.Telegraph != 0
                    || manager.GetComponentData<EnemyProjectileResolution>(entity).Kind == EnemyProjectileResolutionKind.None)
                    throw new Exception("Owner death did not cancel laser: " + death);
                foreach (var v in visuals) if (manager.Exists(v)) throw new Exception("Reflected leg survived owner");
                if (manager.GetComponentData<URPMaterialPropertyBaseColor>(entity).Value.w != 0) throw new Exception("Root visual survived owner");
                checks++;
            }
            finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(cameraObject); }
        }
        return new { passed = true, checks, warningAndActive = true, deadDisabledDestroyed = true, allBounceSegmentsRemoved = true };
    }
}
