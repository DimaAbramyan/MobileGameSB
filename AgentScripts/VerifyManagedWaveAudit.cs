using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;

// Executes the built production assembly. No native Unity lifecycle is claimed here.
public static class VerifyManagedWaveAudit
{
    public sealed class Result
    {
        public int checks;
        public int existingLaserGeometryChecks;
        public long numericAllocatedBytes;
        public double numericMilliseconds;
        public List<string> passed = new();
        public List<string> failures = new();
    }
    private static void Check(bool value, string message, Result result)
    { result.checks++; if (!value) throw new InvalidOperationException(message); }
    private static void Scenario(string name, Action action, Result result)
    { try { action(); result.passed.Add(name); } catch (Exception error) { error = error.GetBaseException(); result.failures.Add(name + ": " + error.GetType().Name + ": " + error.Message); } }
    private static DirectedWaveAttackSequenceStep Step(string name, int repeats = 1)
    {
        // The fake host does not emit projectiles. Avoid constructing its unused pattern
        // payload (AnimationCurve needs the native Editor); assign the scheduler fixture explicitly.
        var step = (DirectedWaveAttackSequenceStep)FormatterServices.GetUninitializedObject(typeof(DirectedWaveAttackSequenceStep));
        step.name = name; step.enabled = true; step.repeatCount = repeats;
        step.waitForCompletion = true; step.steps = new(); step.childWaitTimeout = .2f;
        return step;
    }
    private static string Order(DirectedWaveAttackSequenceSettings settings, int limit = 100)
    {
        var cursor = new DirectedWaveAttackSequenceCursor(settings); var order = new List<string>();
        for (int i = 0; i < limit && cursor.TryNext(out var step); i++) order.Add(step.name);
        return string.Join(",", order);
    }
    private sealed class Host : IDirectedWaveAttackSequenceHost
    {
        public readonly List<string> Starts = new();
        public float Time, Duration = .1f, ChildDuration;
        public int Stops;
        public bool TryStart(DirectedWaveAttackSequenceStep step, out IDirectedWaveAttackSequenceRun run)
        { Starts.Add(step.name); run = new Execution(this, Time + Duration, Time + Duration + ChildDuration); return true; }
    }
    private sealed class Execution : IDirectedWaveAttackSequenceRun
    {
        private readonly Host host;
        private readonly float end, childrenEnd;
        public Execution(Host host, float end, float childrenEnd) { this.host = host; this.end = end; this.childrenEnd = childrenEnd; }
        public bool RootComplete => host.Time >= end;
        public bool ChildrenComplete => host.Time >= childrenEnd;
        public void Tick() { }
        public void Stop() => host.Stops++;
    }
    private static void Tick(DirectedWaveAttackSequencePlayer player, Host host, float time) { host.Time = time; player.Tick(time); }
    public static Result Run()
    {
        var result = new Result();
        Scenario("Sequence ordering, nested blocks, seed and reset", () => {
            var settings = new DirectedWaveAttackSequenceSettings { steps = new() { Step("A"), Step("B"), Step("C") } };
            Check(Order(settings) == "A,B,C", "Once order", result);
            settings.startStep = 1; settings.sequenceMode = DirectedWaveAttackSequenceMode.Loop; settings.loopCount = 2;
            Check(Order(settings) == "B,C,A,B,C", "Loop from selected step", result);
            settings.startStep = 0; settings.sequenceMode = DirectedWaveAttackSequenceMode.PingPong;
            Check(Order(settings) == "A,B,C,B,A,B,C,B,A", "PingPong endpoints", result);
            settings.sequenceMode = DirectedWaveAttackSequenceMode.Random; settings.loopCount = 20;
            string first = Order(settings); Check(first == Order(settings), "Repeatable seed", result);
            settings.seed++; Check(first != Order(settings), "Seed affects order", result);
            var block = Step("Block", 2); block.type = DirectedWaveAttackSequenceStepType.RepeatBlock;
            var inner = Step("Inner", 3); inner.type = DirectedWaveAttackSequenceStepType.RepeatBlock; inner.steps.Add(Step("B"));
            block.steps.Add(Step("A")); block.steps.Add(inner);
            settings.sequenceMode = DirectedWaveAttackSequenceMode.Once; settings.steps = new() { block, Step("C") };
            Check(Order(settings) == "A,B,B,B,A,B,B,B,C", "Nested repeat blocks", result);
            var cursor = new DirectedWaveAttackSequenceCursor(settings); cursor.TryNext(out _); cursor.Reset(); cursor.TryNext(out var reset);
            Check(reset.name == "A", "Reset restarts cursor", result);
        }, result);
        Scenario("Sequence delays, repeats, child timeout and interruption", () => {
            var a = Step("A", 3); a.startDelay = .2f; a.delayAfterStep = .3f;
            var pause = Step("pause"); pause.type = DirectedWaveAttackSequenceStepType.Pause; pause.pauseDuration = .5f;
            var settings = new DirectedWaveAttackSequenceSettings { steps = new() { a, pause, Step("B") } };
            var host = new Host(); var player = new DirectedWaveAttackSequencePlayer(settings, host); player.Begin(0, true);
            for (int i = 0; i <= 150; i++) Tick(player, host, i / 100f);
            Check(string.Join(",", host.Starts) == "A,A,A,B" && player.IsComplete, "Delays, pause and repeats", result);
            a = Step("A"); a.waitForChildAttacks = true;
            settings.steps = new() { a, Step("B") }; host = new Host { ChildDuration = 1000 };
            player = new DirectedWaveAttackSequencePlayer(settings, host); player.Begin(0, true);
            Tick(player, host, 0); Tick(player, host, .1f); Tick(player, host, .29f);
            Check(host.Starts.Count == 1, "Wait for explicit children", result);
            Tick(player, host, .31f); Check(host.Starts.Count == 2 && player.ChildWaitTimeouts == 1, "Bounded child timeout", result);
            player.Pause(.32f); int stops = host.Stops; player.Pause(.33f);
            Check(host.Stops == stops, "Repeated pause does not double-stop runs", result);
            player.Begin(5, false); Tick(player, host, 5);
            Check(host.Starts[host.Starts.Count - 1] == "B", "Interrupted repetition restarts", result);
            player.Stop(); player.Stop(); int starts = host.Starts.Count; Tick(player, host, 100);
            Check(host.Starts.Count == starts, "Stop is idempotent and suppresses launches", result);
        }, result);
        Scenario("Repeated Pause preserves the whole suspended interval", () => {
            var step = Step("Delayed"); step.startDelay = 10;
            var host = new Host(); var player = new DirectedWaveAttackSequencePlayer(new() { steps = new() { step } }, host);
            player.Begin(0, true); Tick(player, host, 0); player.Pause(2); player.Pause(5);
            player.Begin(12, false); Tick(player, host, 19);
            Check(host.Starts.Count == 0, "Eight seconds of original delay remain after resume at 12", result);
            Tick(player, host, 20); Check(host.Starts.Count == 1, "Delay expires at 20", result);
        }, result);
        Scenario("Sequence empty/disabled/cyclic blocks remain bounded", () => {
            var settings = new DirectedWaveAttackSequenceSettings { steps = null };
            var cursor = new DirectedWaveAttackSequenceCursor(settings);
            Check(!cursor.TryNext(out _) && cursor.IsComplete, "Null list terminates", result);
            var block = Step("Cycle"); block.type = DirectedWaveAttackSequenceStepType.RepeatBlock; block.steps.Add(block);
            settings.steps = new() { block, Step("After") };
            Check(Order(settings) == "After", "Recursion depth guard preserves following step", result);
            var pause = Step("Zero"); pause.type = DirectedWaveAttackSequenceStepType.Pause;
            settings.sequenceMode = DirectedWaveAttackSequenceMode.Loop; settings.loopCount = 0; settings.steps = new() { pause };
            var host = new Host(); var player = new DirectedWaveAttackSequencePlayer(settings, host); player.Begin(0, true); Tick(player, host, 0);
            Check(!player.IsComplete, "Infinite zero-time loop yields safely", result);
        }, result);
        Scenario("Numeric bounds, disabled values and deterministic independent channels", () => {
            var a = new NumericRandomStream(937); var b = new NumericRandomStream(937);
            var range = new RandomIntRange { enabled = true, min = int.MaxValue, max = int.MinValue };
            for (int i = 0; i < 10000; i++) Check(range.Sample(0, ref a) == range.Sample(0, ref b), "Full Int32 repeatability", result);
            range.min = range.max = 7; Check(range.Sample(0, ref a) == 7, "Degenerate int range", result);
            a = new NumericRandomStream(937); b = new NumericRandomStream(937);
            range.enabled = false; uint expected = b.Next(); range.Sample(91, ref a); Check(a.Next() == expected, "Disabled range does not consume stream", result);
            var floating = new RandomFloatRange { enabled = true, min = float.MaxValue, max = -float.MaxValue };
            Check(float.IsFinite(floating.Sample01(.5f)), "Extreme float interpolation remains finite", result);
            floating.min = float.NaN; floating.max = float.PositiveInfinity; floating.GetBounds(out float low, out float high, 0);
            Check(low == 0 && high == 0, "Invalid randomized endpoints normalized", result);
            floating.min = -10; floating.max = 10; floating.randomizeOn = EnemyNumericRandomizeOn.PerAttack;
            var sample = new EnemyNumericSample(123, 456);
            float first = sample.Float(floating, 0, 9);
            Check(first == new EnemyNumericSample(123, 999).ForDirection(12).ForProjectile(17).Float(floating, 0, 9), "Per Attack identity", result);
            floating.randomizeOn = EnemyNumericRandomizeOn.PerShot;
            first = sample.Float(floating, 0, 9);
            Check(first == sample.ForDirection(12).ForProjectile(17).Float(floating, 0, 9), "Per Shot identity", result);
            floating.randomizeOn = EnemyNumericRandomizeOn.PerProjectile;
            Check(sample.ForProjectile(1).Float(floating, 0, 9) != sample.ForProjectile(2).Float(floating, 0, 9), "Per Projectile identity", result);
            first = sample.Float(floating, 0, 9); sample.Float(floating, 0, 10);
            Check(first == sample.Float(floating, 0, 9), "Other channels cannot perturb existing sample", result);
        }, result);
        Scenario("Completion accounting and damage profiles", () => {
            var completion = new EnemyAttackCompletionTracker(); completion.BeginChild(); completion.BeginChild(); completion.EndChild();
            Check(!completion.IsComplete && completion.PendingChildAttacks == 1, "Parallel children retain completion lease", result);
            completion.EndChild(); completion.EndChild(); Check(completion.IsComplete, "Duplicate completion cannot become negative", result);
            Check(EnemyDamageProfiles.Get(EnemyDamageType.Chemical).ShieldBypassFraction == 1, "Chemical bypass", result);
            Check(EnemyDamageProfiles.Get(EnemyDamageType.Default).HullMultiplier == 1 && EnemyDamageProfiles.Get(EnemyDamageType.Default).ShieldMultiplier == 1, "Default neutral", result);
            Check(EnemyDamageProfiles.Get(EnemyDamageType.Kinetick).HullMultiplier == 1.35f && EnemyDamageProfiles.Get(EnemyDamageType.Plazma).ShieldMultiplier == 1.35f, "Strong effects retained", result);
        }, result);
        Scenario("Existing Laser geometry suite", () => {
            result.existingLaserGeometryChecks = (int)typeof(VerifyManagedWaveAudit).Assembly.GetType("VerifyExistingLaserGeometry").GetMethod("Run").Invoke(null, null);
            Check(result.existingLaserGeometryChecks > 0, "Existing geometry checks executed", result);
        }, result);
        Scenario("Swept collision symmetry, translation and high speeds", () => {
            var random = new System.Random(937);
            float2 Point() => new((float)random.NextDouble() * 20 - 10, (float)random.NextDouble() * 20 - 10);
            for (int i = 0; i < 2000; i++) {
                float2 a = Point(), b = Point(), c = Point(), d = Point();
                float radius = (float)random.NextDouble() * 2;
                bool hit = ProjectileInterceptorSystem.IntersectsMovingCircle(a, b, c, d, radius);
                Check(hit == ProjectileInterceptorSystem.IntersectsMovingCircle(b, a, d, c, radius), "Circle time reversal", result);
                Check(hit == ProjectileInterceptorSystem.IntersectsMovingCircle(c, d, a, b, radius), "Relative movement symmetry", result);
                float2 size = new(.3f, 1.2f), right = new(.6f, .8f), shift = new(100, -100);
                hit = ProjectileInterceptorSystem.IntersectsMovingBox(a, b, c, d, size, right, radius);
                Check(hit == ProjectileInterceptorSystem.IntersectsMovingBox(b, a, d, c, size, right, radius), "Box time reversal", result);
                Check(hit == ProjectileInterceptorSystem.IntersectsMovingBox(a + shift, b + shift, c + shift, d + shift, size, right, radius), "Common world translation", result);
            }
            Check(ProjectileInterceptorSystem.IntersectsMovingCircle(new(-100, 0), new(100, 0), float2.zero, float2.zero, .05f), "Fast crossing cannot tunnel", result);
        }, result);
        Scenario("Player short bounds sweep includes its endpoint", () => {
            Check(PlayerProjectileCollisionMath.IntersectsExpandedBounds(new(1.000002f, 0), new(.999998f, 0),
                new(-1, -1), new(1, 1), 0), "Short player sweep crosses the box edge", result);
            Check(!PlayerProjectileCollisionMath.IntersectsExpandedBounds(new(1.000002f, 2), new(.999998f, 2),
                new(-1, -1), new(1, 1), 0), "Short player sweep still rejects a miss", result);
        }, result);
        Scenario("Short circle sweep includes its endpoint", () => {
            Check(ProjectileInterceptorSystem.IntersectsMovingCircle(new(1.0001f, 0), new(.9999f, 0), float2.zero, float2.zero, 1),
                "A short segment crosses the circle surface", result);
        }, result);
        Scenario("Short interceptor segment retains its full capsule", () => {
            Check(ProjectileInterceptorSystem.IntersectsSegment(new(.00015f, .00005f), new(.00015f, .00005f), new(0, 0), new(.0002f, 0), .00006f),
                "A stationary point overlaps the middle of a small segment capsule", result);
        }, result);
        Scenario("Small crossing segments intersect without a radius", () => {
            Check(ProjectileInterceptorSystem.IntersectsSegment(new(.0001f, -.0001f), new(.0001f, .0001f), new(0, 0), new(.0002f, 0), 0),
                "Non-parallel short segments have an interior intersection", result);
        }, result);
        Scenario("Short box sweep includes its endpoint", () => {
            Check(ProjectileInterceptorSystem.IntersectsMovingBox(new(1.000002f, 0), new(.999998f, 0), float2.zero, float2.zero, new(1, 1), new(1, 0), 0),
                "A tiny segment crosses a box surface", result);
        }, result);
        Scenario("Path curve endpoints, finite samples and distance mapping", () => {
            var random = new System.Random(731);
            Vector3 Point() => new((float)random.NextDouble() * 20 - 10, (float)random.NextDouble() * 20 - 10, 0);
            for (int path = 0; path < 30; path++) {
                Vector3 previous = Point(), current = Point(), next = path == 0 ? current : Point(), following = Point();
                foreach (DirectedWaveSegmentMotion motion in Enum.GetValues(typeof(DirectedWaveSegmentMotion))) {
                    var settings = new DirectedWaveCurveSettings { bulge = .4f, tension = .3f, parameterization = DirectedWaveCurveParameterization.Centripetal };
                    Check(Vector3.Distance(DirectedWavePathEvaluator.EvaluateSegment(previous, current, next, following, motion, 0, settings), current) < .0001f, "Curve starts at checkpoint", result);
                    Check(Vector3.Distance(DirectedWavePathEvaluator.EvaluateSegment(previous, current, next, following, motion, 1, settings), next) < .0001f, "Curve ends at checkpoint", result);
                    float last = -1;
                    for (int i = 0; i <= 20; i++) {
                        float t = DirectedWavePathEvaluator.GetParameterAtNormalizedDistance(previous, current, next, following, motion, i / 20f, settings);
                        Vector3 value = DirectedWavePathEvaluator.EvaluateSegment(previous, current, next, following, motion, t, settings);
                        Check(float.IsFinite(value.x) && float.IsFinite(value.y) && t >= last && t <= 1, "Finite monotonic distance parameter", result); last = t;
                    }
                }
            }
        }, result);
        Scenario("Dive minimum duration, speed caps, reversals and zero distance", () => {
            foreach (float distance in new[] { 0f, .000001f, .03f, 1f, 12f })
            foreach (float maximum in new[] { 0f, .5f, 2f }) {
                var progress = new DirectedWaveDiveMotionProgress(Vector3.zero, distance, 30, .3f);
                float elapsed = 0; Vector3 previous = Vector3.zero; int steps = 0;
                while (!progress.IsComplete && steps < 100000) {
                    float delta = steps % 2 == 0 ? .007f : .13f;
                    float phase = progress.PrepareStep(delta, 8);
                    // Analytic reversal, not a replacement AnimationCurve implementation.
                    float curve = phase < .4f ? phase * 2.5f : (phase - .4f) / .6f;
                    Vector3 value = progress.ApplyStep(new Vector3(distance * curve, 0, 0), delta, maximum);
                    Check(float.IsFinite(value.x), "Finite dive position", result);
                    if (maximum > 0) Check(Vector3.Distance(value, previous) <= maximum * delta + 1e-6f, "Real displacement cap", result);
                    previous = value; elapsed += delta; steps++;
                }
                Check(progress.IsComplete && steps < 100000, "Limited dive terminates", result);
                if (distance > 0) Check(elapsed + .00001f >= .3f, "Movement multiplier cannot bypass minimum duration", result);
                else Check(elapsed == 0 && progress.Position.Equals(Vector3.zero), "Zero distance completes immediately", result);
            }
        }, result);
        Scenario("Managed Preview snapshot restores containers, RNG and Entity remaps repeatedly", () => {
            System.Reflection.Assembly editor = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) if (assembly.GetName().Name == "Assembly-CSharp-Editor") editor = assembly;
            Type graphType = editor.GetType("WavePreviewStateGraph", true);
            var capture = graphType.GetMethod("Capture", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var restore = graphType.GetMethod("Restore", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var originalA = new Entity { Index = 1, Version = 1 }; var originalB = new Entity { Index = 2, Version = 1 };
            var mappedA = new Entity { Index = 11, Version = 2 }; var mappedB = new Entity { Index = 12, Version = 2 };
            var map = new Dictionary<Entity, Entity> { [originalA] = mappedA, [originalB] = mappedB };
            var list = new List<Entity> { originalA, originalB }; var stack = new Stack<Entity>(); stack.Push(originalA); stack.Push(originalB);
            var queue = new Queue<Entity>(); queue.Enqueue(originalA); queue.Enqueue(originalB);
            var set = new HashSet<Entity> { originalA, originalB }; var dictionary = new Dictionary<Entity, Entity> { [originalA] = originalB };
            var array = new (Entity entity, int value)[] { (originalA, 17) };
            var cycle = new List<object>(); cycle.Add(cycle);
            var random = new System.Random(937); var numeric = new EnemyAttackNumericState(937, 2, 7);
            var range = new RandomFloatRange { enabled = true, min = 1, max = 10 };
            numeric.SampleCooldown(0, range); random.Next();
            object snapshot = capture.Invoke(null, new object[] { new object[] { list, stack, queue, set, dictionary, array, cycle, random, numeric } });
            float expectedNumeric = numeric.SampleCooldown(0, range); int expectedRandom = random.Next();
            for (int iteration = 0; iteration < 3; iteration++) {
                list.Clear(); stack.Clear(); queue.Clear(); set.Clear(); dictionary.Clear(); array[0] = (originalB, -1); cycle.Clear();
                for (int i = 0; i < 10; i++) { random.Next(); numeric.SampleCooldown(0, range); }
                restore.Invoke(snapshot, new object[] { map });
                Check(list.Count == 2 && list[0] == mappedA && list[1] == mappedB, "List entity remap", result);
                Check(stack.Pop() == mappedB && stack.Pop() == mappedA, "Stack order", result);
                Check(queue.Dequeue() == mappedA && queue.Dequeue() == mappedB, "Queue order", result);
                Check(set.SetEquals(new[] { mappedA, mappedB }) && dictionary[mappedA] == mappedB, "Set and dictionary remap", result);
                Check(array[0].entity == mappedA && array[0].value == 17, "Entity in a nested value type", result);
                Check(cycle.Count == 1 && ReferenceEquals(cycle[0], cycle), "Cyclic reference identity", result);
                Check(random.Next() == expectedRandom && numeric.SampleCooldown(0, range) == expectedNumeric, "RNG state replay", result);
            }
        }, result);
        Scenario("Numeric sample allocation baseline", () => {
            var range = new RandomFloatRange { enabled = true, min = -10, max = 10, randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
            var sample = new EnemyNumericSample(123, 456); float total = 0;
            for (int i = 0; i < 1000; i++) total += sample.ForProjectile(i).Float(range, 0, 10);
            var watch = System.Diagnostics.Stopwatch.StartNew(); long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100000; i++) total += sample.ForProjectile(i).Float(range, 0, 10);
            result.numericAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated; watch.Stop(); result.numericMilliseconds = watch.Elapsed.TotalMilliseconds;
            Check(float.IsFinite(total) && result.numericAllocatedBytes == 0, "Sampling itself allocates no managed memory", result);
        }, result);
        return result;
    }
}
