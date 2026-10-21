using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class VerifyProjectileDestructionMeta
{
    static int checks;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
    public static object Run()
    {
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Meta destruction verification");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.SetActive(false);
        try
        {
            ShipMetaContract[] contracts = { new BlackHoleShipMetaContract(), new BladeShipMetaContract(), new ArkanoidShipMetaContract(), new OrbitShipMetaContract() };
            for (int i = 0; i < contracts.Length; i++)
            {
                var contract = contracts[i];
                var field = contract.GetType().GetField("projectileDestructionPolicy", Flags);
                Check(field != null && field.IsDefined(typeof(SerializeField), true), "Meta exposes serialized policy");
                var ability = (IEnemyProjectileDestructionAbility)contract;
                Check(ability.CanDestroyProjectile(EnemyProjectileCharacteristic.Normal) == (i < 2), "Meta defaults preserve rules");
                foreach (var policy in new[] { EnemyProjectileDestructionPolicy.FragileOnly, EnemyProjectileDestructionPolicy.NormalAndFragile })
                {
                    field.SetValue(contract, policy);
                    var cloned = (IEnemyProjectileDestructionAbility)contract.Clone();
                    Check(cloned.ProjectileDestructionPolicy == policy, "Meta Clone retains policy");
                    var stats = new ShipMetaRuntimeStats(new ShipMetaBasicStats(), new List<ShipMetaContract> { contract });
                    if (contract is BlackHoleShipMetaContract)
                    {
                        var active = root.GetComponent<BlackHoleActive>() ?? root.AddComponent<BlackHoleActive>();
                        typeof(BlackHoleActive).GetMethod("ApplySpecificShipMetaStats", Flags).Invoke(active, new object[] { stats });
                        Check((EnemyProjectileDestructionPolicy)typeof(BlackHoleActive).GetField("projectileDestructionPolicy", Flags).GetValue(active) == policy, "Black Hole reads Meta policy");
                    }
                    if (contract is BladeShipMetaContract)
                    {
                        var blade = root.GetComponent<CreateBladeAbility>() ?? root.AddComponent<CreateBladeAbility>();
                        typeof(CreateBladeAbility).GetMethod("ApplySpecificShipMetaStats", Flags).Invoke(blade, new object[] { stats });
                        Check((EnemyProjectileDestructionPolicy)typeof(CreateBladeAbility).GetField("projectileDestructionPolicy", Flags).GetValue(blade) == policy, "Blade reads Meta policy");
                    }
                    if (contract is ArkanoidShipMetaContract arkanoid)
                    {
                        var ball = root.GetComponent<ArkanoidBall>() ?? root.AddComponent<ArkanoidBall>();
                        ball.ApplyShipMetaStats(arkanoid);
                        Check(ball.ProjectileDestructionPolicy == policy, "Arkanoid reads Meta policy");
                    }
                    if (contract is OrbitShipMetaContract orbit)
                    {
                        var satellite = root.GetComponent<OrbitSatellite>() ?? root.AddComponent<OrbitSatellite>();
                        satellite.Configure(null, null, orbit.ObjectRadius, orbit.ProjectileDestructionPolicy);
                        Check(satellite.ProjectileDestructionPolicy == policy, "Orbit receives Meta policy");
                    }
                }
            }
            foreach(var c in root.GetComponents<MonoBehaviour>())
                Check(new SerializedObject(c).FindProperty("projectileDestructionPolicy") == null, "Runtime component does not expose designer policy");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        return new { passed = checks };
    }
}
