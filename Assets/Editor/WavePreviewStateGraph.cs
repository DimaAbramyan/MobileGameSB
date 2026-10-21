using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Entities;
using UnityEngine;

// Editor-only memento of existing managed schedulers. Object identity is retained so
// shared completion trackers, tickets and permission owners stay shared on rollback.
// No constructors, Unity serialization, GameObjects or gameplay reflection are involved.
internal sealed class WavePreviewStateGraph
{
    private enum Kind { Fields, Array, List, Dictionary, Set, Stack, Queue }
    private sealed class Node
    {
        public object Target;
        public Kind Kind;
        public FieldInfo[] Fields;
        public object[] Values;
        public MethodInfo Clear, Add;
    }
    private sealed class Identity : IEqualityComparer<object>
    {
        public new bool Equals(object a, object b) => ReferenceEquals(a, b);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }
    private static readonly Dictionary<Type, FieldInfo[]> fields = new();
    private static readonly Dictionary<Type, bool> entityValues = new();
    private readonly List<Node> nodes = new();
    private readonly HashSet<object> seen = new(new Identity());
    public long EstimatedBytes { get; private set; }

    public static WavePreviewStateGraph Capture(params object[] roots)
    {
        var result = new WavePreviewStateGraph();
        foreach (var root in roots) result.Visit(root);
        result.seen.Clear();
        return result;
    }
    private static bool Atomic(Type type)
        => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
        || type == typeof(Entity) || typeof(UnityEngine.Object).IsAssignableFrom(type)
        || typeof(Delegate).IsAssignableFrom(type) || type == typeof(AnimationCurve)
        || type == typeof(Gradient) || type == typeof(Type)
        || type.Namespace != null && (type.Namespace.StartsWith("Unity.Entities")
            || type.Namespace.StartsWith("Unity.Collections") || type.Namespace.StartsWith("Unity.Mathematics"));
    private static bool Skip(FieldInfo field)
    {
        var owner = field.DeclaringType;
        if (owner == typeof(WaveShootingPreviewSession) && field.Name == "renderersVisible") return true;
        if (owner == typeof(EnemyProjectileEcsSpawner))
            return field.Name == "visualsByConfiguration" || field.Name == "configurationsByPrefab"
                || field.Name == "invalidConfigurationsLogged" || field.Name == "preparedSpawns"
                || field.Name == "singleSpawnRequestBuffer" || field.Name == "entityWorld" || field.Name == "entityManager";
        return owner?.Name == "PreviewParticipant" && (field.Name == "bound" || field.Name == "cue");
    }
    private static FieldInfo[] Fields(Type type)
    {
        if (fields.TryGetValue(type, out var cached)) return cached;
        var result = new List<FieldInfo>();
        for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (!field.IsStatic && !field.FieldType.IsPointer && !Skip(field)) result.Add(field);
        cached = result.ToArray(); fields.Add(type, cached); return cached;
    }
    private void Visit(object value)
    {
        if (value == null) return;
        var type = value.GetType();
        if (Atomic(type) || value is EnemyProjectilePreviewWorld || value is WaveConductorShootingPreview) return;
        if (type.IsValueType)
        {
            foreach (var field in Fields(type)) Visit(field.GetValue(value));
            return;
        }
        if (!seen.Add(value)) return;
        var node = new Node { Target = value };
        if (value is Array array)
        {
            if (array.Rank != 1) throw new NotSupportedException("Preview snapshot requires a one-dimensional state array: " + type);
            node.Kind = Kind.Array; node.Values = new object[array.Length];
            for (int i = 0; i < array.Length; i++) node.Values[i] = array.GetValue(i);
        }
        else if (value is IDictionary dictionary)
        {
            node.Kind = Kind.Dictionary; node.Values = new object[dictionary.Count * 2]; int i = 0;
            foreach (DictionaryEntry entry in dictionary) { node.Values[i++] = entry.Key; node.Values[i++] = entry.Value; }
        }
        else if (value is IList list)
        {
            node.Kind = Kind.List; node.Values = new object[list.Count]; list.CopyTo(node.Values, 0);
        }
        else if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(HashSet<>)
            || type.GetGenericTypeDefinition() == typeof(Stack<>) || type.GetGenericTypeDefinition() == typeof(Queue<>)))
        {
            bool stack = type.GetGenericTypeDefinition() == typeof(Stack<>), queue = type.GetGenericTypeDefinition() == typeof(Queue<>);
            node.Kind = stack ? Kind.Stack : queue ? Kind.Queue : Kind.Set;
            var values = new List<object>(); foreach (var item in (IEnumerable)value) values.Add(item);
            node.Values = values.ToArray(); node.Clear = type.GetMethod("Clear");
            node.Add = type.GetMethod(stack ? "Push" : queue ? "Enqueue" : "Add");
        }
        else
        {
            // Native engine resources and services are stable external dependencies.
            bool owned = type.Assembly == typeof(WaveShootingPreviewSession).Assembly || type.Assembly == typeof(EnemyChildAttackRunner).Assembly;
            bool random = type == typeof(System.Random) || type.FullName.StartsWith("System.Random+");
            if (!owned && !random || type.Name == "Cue") return;
            node.Kind = Kind.Fields; node.Fields = Fields(type); node.Values = new object[node.Fields.Length];
            for (int i = 0; i < node.Fields.Length; i++) node.Values[i] = node.Fields[i].GetValue(value);
        }
        nodes.Add(node); EstimatedBytes += 80L + node.Values.Length * 32L;
        foreach (var item in node.Values) Visit(item);
    }
    private static bool ContainsEntity(Type type)
    {
        if (type == typeof(Entity)) return true;
        if (!type.IsValueType || Atomic(type)) return false;
        if (entityValues.TryGetValue(type, out bool result)) return result;
        entityValues[type] = false;
        foreach (var field in Fields(type)) if (ContainsEntity(field.FieldType)) { result = true; break; }
        entityValues[type] = result; return result;
    }
    private static object Remap(object value, Dictionary<Entity, Entity> entities)
    {
        if (value is Entity entity) return entities != null && entities.TryGetValue(entity, out var mapped) ? mapped : entity;
        if (value == null || entities == null || !ContainsEntity(value.GetType())) return value;
        object copy = RuntimeHelpers.GetObjectValue(value);
        foreach (var field in Fields(value.GetType()))
            if (ContainsEntity(field.FieldType)) field.SetValue(copy, Remap(field.GetValue(value), entities));
        return copy;
    }
    public void Restore(Dictionary<Entity, Entity> entities = null)
    {
        foreach (var node in nodes)
        {
            if (node.Kind != Kind.Fields) continue;
            for (int i = 0; i < node.Fields.Length; i++)
            {
                object value = Remap(node.Values[i], entities);
                if (!ReferenceEquals(node.Fields[i].GetValue(node.Target), value)) node.Fields[i].SetValue(node.Target, value);
            }
        }
        var argument = new object[1];
        foreach (var node in nodes)
        {
            switch (node.Kind)
            {
                case Kind.Array:
                    var array = (Array)node.Target;
                    for (int i = 0; i < node.Values.Length; i++) array.SetValue(Remap(node.Values[i], entities), i);
                    break;
                case Kind.List:
                    var list = (IList)node.Target; list.Clear();
                    foreach (var value in node.Values) list.Add(Remap(value, entities));
                    break;
                case Kind.Dictionary:
                    var dictionary = (IDictionary)node.Target; dictionary.Clear();
                    for (int i = 0; i < node.Values.Length; i += 2) dictionary.Add(Remap(node.Values[i], entities), Remap(node.Values[i + 1], entities));
                    break;
                case Kind.Set:
                case Kind.Stack:
                case Kind.Queue:
                    node.Clear.Invoke(node.Target, null);
                    for (int i = 0; i < node.Values.Length; i++)
                    {
                        int index = node.Kind == Kind.Stack ? node.Values.Length - 1 - i : i;
                        argument[0] = Remap(node.Values[index], entities); node.Add.Invoke(node.Target, argument);
                    }
                    break;
            }
        }
    }
}
