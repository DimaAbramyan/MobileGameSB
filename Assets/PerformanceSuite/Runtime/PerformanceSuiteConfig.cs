#if UNITY_EDITOR || ANDROID_PERFORMANCE_SUITE
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Performance
{
    public enum PerformanceWorkload { Projectiles, Churn, Collision, Homing, Angular, Orbit, Child, Laser, Waves, Combat, Soak }

    [Serializable]
    public sealed class PerformanceCase
    {
        public string id = "ecs_500";
        public bool enabled = true;
        public PerformanceWorkload workload;
        [Range(1, 10000)] public int projectiles = 500;
        [Range(1, 256)] public int enemies = 24;
        [Range(1, 8)] public int subwaves = 2;
        [Range(1, 4)] public int childGenerations = 1;
        [Range(.1f, 128f)] public float childRootsPerSecond = 8f;
        public bool coordinator;
        [Min(.1f)] public float lifetime = 3f;
        [Min(.01f)] public float baseSpeed = 2f;
        public float angularSpeed = 90f;
        public float lateralSpeed = .5f;
        [Min(0f)] public float orbitRadius = .3f;
        public float orbitRadialSpeed = .2f;
        [Min(0f)] public float warmupSeconds = 3f;
        [Min(.2f)] public float measurementSeconds = 20f;
        [Range(1, 10)] public int repetitions = 3;
        public int seed = 53;
        public GameObject wavePrefab;
    }

    [CreateAssetMenu(menuName = "Performance/Android Performance Suite", fileName = "AndroidPerformanceSuite")]
    public sealed class PerformanceSuiteConfig : ScriptableObject
    {
        public EnemyBullet bullet;
        public EnemyBullet homingBullet;
        public Enemy enemy;
        public ParentShip ship;
        public List<PerformanceCase> scenarios = new();
        [Range(1000, 20000)] public int maximumLiveProjectiles = 10000;
        [Range(256, 400000)] public int maximumSamples = 200000;
        [Range(.1f, 10f)] public float memoryInterval = 1f;
        [Range(.1f, 20f)] public float cleanupSettleSeconds = 2f;
        [Range(50, 3000)] public float abortFrameMilliseconds = 1500f;
        [Range(128, 8192)] public int abortAllocatedMegabytes = 2048;
        public bool renderProjectiles = true;
        public bool forceCollectionAfterCleanup;
        public bool captureProfilerTrace;
        public bool showPlayerOverlay;
        public bool autoRunPlayer = true;
    }
}
#endif
