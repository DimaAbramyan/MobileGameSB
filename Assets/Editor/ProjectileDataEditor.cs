using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProjectileData))]
public sealed class ProjectileDataEditor : Editor
{
    private SerializedProperty deliveryType;
    private SerializedProperty simulationBackend;
    private SerializedProperty damage;
    private SerializedProperty damageType;
    private SerializedProperty projectilePrefab;
    private SerializedProperty range;
    private SerializedProperty speed;
    private SerializedProperty contracts;

    private void OnEnable()
    {
        deliveryType = serializedObject.FindProperty("deliveryType");
        simulationBackend = serializedObject.FindProperty("simulationBackend");
        damage = serializedObject.FindProperty("damage");
        damageType = serializedObject.FindProperty("damageType");
        projectilePrefab = serializedObject.FindProperty("projectilePrefab");
        range = serializedObject.FindProperty("range");
        speed = serializedObject.FindProperty("speed");
        contracts = serializedObject.FindProperty("contracts");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Delivery", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(deliveryType, new GUIContent("Type"));

        if (IsProjectile())
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Direct Damage", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(damage);
            EditorGUILayout.PropertyField(damageType, new GUIContent("Damage Type"));
            DrawPhysicalProjectileFields();
        }
        else if (IsContact())
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Contact Damage", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(damageType, new GUIContent("Damage Type"));
            DrawContactHelp();
        }
        else
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Direct Damage", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(damage);
            EditorGUILayout.PropertyField(damageType, new GUIContent("Damage Type"));
            DrawBeamHelp();
        }

        DrawContracts();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPhysicalProjectileFields()
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Physical Projectile", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(projectilePrefab, new GUIContent("Prefab"));
        EditorGUILayout.PropertyField(range);
        EditorGUILayout.PropertyField(speed);
        EditorGUILayout.PropertyField(
            simulationBackend,
            new GUIContent("Simulation Backend"));
        if (simulationBackend.enumValueIndex
            == (int)ProjectileSimulationBackend.Entities)
        {
            EditorGUILayout.HelpBox(
                "Entities supports straight or homing Damage And Destroy, Pierce Once, "
                + "Pierce Continuous and Explode And Spawn projectiles, including Scale "
                + "Growth and Enemy Debuffs. Fade, collider-lifetime and custom contact "
                + "damage-source contracts remain MonoBehaviour-only.",
                MessageType.Info);
        }
    }

    private static void DrawBeamHelp()
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.HelpBox(
            "Beam range is intentionally not configured yet. Add the Beam "
            + "contract below for width, damage interval and impact effects.",
            MessageType.Info);
    }

    private void DrawContactHelp()
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Contact", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            simulationBackend,
            new GUIContent("Simulation Backend"));
        EditorGUILayout.HelpBox(
            "Contact runs through ECS. Damage and tick interval come from the owning "
            + "WeaponData. Add a Contact Area contract and edit its PolygonCollider2D "
            + "vertices to set the hit area.",
            simulationBackend.enumValueIndex
                == (int)ProjectileSimulationBackend.Entities
                ? MessageType.Info
                : MessageType.Error);
    }

    private void DrawContracts()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Contracts", EditorStyles.boldLabel);

        for (int index = 0; index < contracts.arraySize; index++)
        {
            SerializedProperty contract = contracts.GetArrayElementAtIndex(index);
            ProjectileDataContract value =
                contract.managedReferenceValue as ProjectileDataContract;
            if (value == null)
                continue;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        value.DisplayName,
                        EditorStyles.boldLabel);
                    if (GUILayout.Button("Remove", GUILayout.Width(65f)))
                    {
                        RemoveContract(value.GetType());
                        GUIUtility.ExitGUI();
                    }
                }

                DrawContractFields(contract, value);
            }
        }

        if (GUILayout.Button("Add Contract"))
            ShowAddContractMenu();
    }

    private void DrawContractFields(
        SerializedProperty contract,
        ProjectileDataContract value)
    {
        switch (value)
        {
            case ProjectileFlightContract:
                DrawField(contract, "growDuringFlight", "Grow During Flight");
                if (contract.FindPropertyRelative("growDuringFlight").boolValue)
                {
                    DrawField(
                        contract,
                        "scaleGrowthPerSecond",
                        "Scale Growth Per Second");
                }
                break;
            case ProjectileResonanceSphereContract:
                DrawField(
                    contract,
                    "maximumStoredDamage",
                    "Maximum Stored Damage");
                DrawField(contract, "lifetime", "Lifetime");
                DrawField(contract, "explosionRadius", "Explosion Radius");
                DrawField(contract, "waveSpeed", "Wave Speed");
                EditorGUILayout.HelpBox(
                    "The sphere does not deal contact damage. It stores damage "
                    + "from player projectiles except Resonance, then deals the "
                    + "stored damage once to each enemy as the wave reaches it.",
                    MessageType.None);
                break;
            case ProjectileHomingContract:
                DrawField(contract, "rotationSpeed", "Rotation Speed");
                break;
            case ProjectileTargetingContract:
                DrawField(contract, "maxTargets", "Maximum Targets");
                DrawField(
                    contract,
                    "searchRadius",
                    "Search Radius (0 = Unlimited)");
                break;
            case ProjectileContactContract:
                DrawField(contract, "contactMode", "Contact Mode");
                break;
            case ProjectileContinuousDamageContract:
                DrawField(contract, "damageTickInterval", "Damage Tick Interval");
                break;
            case ProjectileCircularChainContract:
                  DrawField(
                      contract,
                      "hitsPerTarget",
                      "Damage Applications Per Target");
                  DrawField(contract, "damagePerHit", "Damage Per Hit");
                  DrawField(contract, "hitInterval", "Hit Interval");
                  DrawField(
                      contract,
                      "maximumTargets",
                      "Chain Targets (Including Initial Target)");
                  DrawField(contract, "searchConeAngle", "Search Cone Angle");
                  DrawField(contract, "searchRange", "Search Range");
                  DrawField(contract, "randomEscapeAngle", "Random Escape Angle");
                  EditorGUILayout.HelpBox(
                      "Every chain target receives Damage Applications Per Target hits. "
                      + "For example: 3 chain targets × 3 applications = 9 total hits.",
                      MessageType.None);
                  break;
            case ProjectileArcNodesContract:
                DrawField(contract, "damagePerArc", "Electric Damage Per Arc");
                DrawField(contract, "connectionRange", "Connection Range");
                DrawField(contract, "pulseInterval", "Pulse Interval");
                DrawField(
                    contract,
                    "maximumConnections",
                    "Maximum Connections Per Node");
                DrawField(contract, "arcHitRadius", "Arc Hit Radius");
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Arc Visual", EditorStyles.miniBoldLabel);
                DrawField(contract, "visualDuration", "Duration");
                DrawField(contract, "visualWidth", "Width");
                DrawField(contract, "visualSegments", "Segments");
                DrawField(contract, "visualJitter", "Jitter");
                DrawField(contract, "visualColor", "Color");
                EditorGUILayout.HelpBox(
                    "Arc Nodes ignore direct contact. Only a segment between two "
                    + "nearby nodes deals Electric damage.",
                    MessageType.None);
                break;
            case ProjectileLifetimeContract:
                DrawField(contract, "lifetime", "Lifetime");
                DrawField(
                    contract,
                    "disableColliderAfterFirstPhysicsStep",
                    "Collider Active For One Physics Step");
                DrawField(contract, "fadeBeforeDespawn", "Fade Before Despawn");
                if (contract.FindPropertyRelative("fadeBeforeDespawn").boolValue)
                    DrawField(contract, "fadeDuration", "Fade Duration");
                break;
            case ProjectileExplosionContract:
                DrawField(contract, "explosionPrefab", "Explosion Visual");
                DrawField(
                    contract,
                    "explosionRadius",
                    "Explosion Radius (0 = Prefab)");
                DrawField(
                    contract,
                    "explodeAtMaximumRange",
                    "Explode At Maximum Range");
                EditorGUILayout.HelpBox(
                    "Configure the Explosion damage and damage type in Damage Sources.",
                    MessageType.None);
                break;
            case SpawnSecondaryProjectileContract:
                DrawField(contract, "spawnTrigger", "Spawn Trigger");
                if (contract.FindPropertyRelative("spawnTrigger").enumValueIndex
                    == (int)SecondaryProjectileSpawnTrigger.AfterTravelDistance)
                {
                    DrawField(contract, "travelDistance", "Travel Distance");
                }
                DrawField(
                    contract,
                    "ignoreTriggeringEnemy",
                    "Ignore Triggering Enemy");
                DrawField(
                    contract,
                    "secondaryProjectile",
                    "Secondary Projectile");
                EditorGUILayout.HelpBox(
                    "The secondary projectile inherits the parent's direction and owner. "
                    + "It uses its own Projectile Data for damage, range, speed and contracts.",
                    MessageType.None);
                break;
                case ProjectileDamageSourcesContract:
                    EditorGUILayout.PropertyField(
                        contract.FindPropertyRelative("sources"),
                        new GUIContent("Sources"),
                        true);
                    break;
                case ProjectileEnemyDebuffsContract:
                    EditorGUILayout.PropertyField(
                        contract.FindPropertyRelative("debuffs"),
                        new GUIContent("Debuffs"),
                        true);
                    EditorGUILayout.HelpBox(
                        "Debuffs are applied only when this hit deals hull damage. "
                        + "A shield-only hit does not add a stack.",
                        MessageType.None);
                    break;
            case ProjectileBeamContract:
                DrawField(contract, "width", "Width");
                DrawField(contract, "damageTickInterval", "Damage Tick Interval");
                EditorGUILayout.PropertyField(
                    contract.FindPropertyRelative("impactEffects"),
                    new GUIContent("Impact Effects"),
                    true);
                break;
            case ProjectileContactAreaContract:
                DrawField(contract, "contactArea", "Contact Area");
                EditorGUILayout.HelpBox(
                    "The PolygonCollider2D on this prefab is the Contact hitbox. "
                    + "Select the prefab to edit its vertices and their coordinates.",
                    MessageType.None);
                break;
        }
    }

    private void ShowAddContractMenu()
    {
        GenericMenu menu = new();
        List<Type> types = new(TypeCache.GetTypesDerivedFrom<ProjectileDataContract>());
        types.Sort((first, second) => string.Compare(
            GetDisplayName(first),
            GetDisplayName(second),
            StringComparison.Ordinal));

        ProjectileData projectileData = (ProjectileData)target;
        for (int index = 0; index < types.Count; index++)
        {
            Type type = types[index];
            if (type.IsAbstract || !IsAvailableForCurrentType(type))
                continue;

            string displayName = GetDisplayName(type);
            if (projectileData.ContainsContract(type))
            {
                menu.AddDisabledItem(new GUIContent(displayName));
                continue;
            }

            menu.AddItem(
                new GUIContent(displayName),
                false,
                () => AddContract(type));
        }

        menu.ShowAsContext();
    }

    private bool IsAvailableForCurrentType(Type contractType)
    {
        if (IsContact())
        {
            return contractType == typeof(ProjectileContactAreaContract)
                || contractType == typeof(ProjectileEnemyDebuffsContract);
        }

        if (contractType == typeof(ProjectileDamageSourcesContract)
            || contractType == typeof(ProjectileTargetingContract))
            return true;

        if (IsProjectile())
            return contractType != typeof(ProjectileBeamContract);

        return contractType == typeof(ProjectileBeamContract);
    }

    private void AddContract(Type contractType)
    {
        serializedObject.ApplyModifiedProperties();
        ProjectileData projectileData = (ProjectileData)target;
        Undo.RecordObject(projectileData, "Add projectile contract");
        if (projectileData.TryAddContract(contractType))
            EditorUtility.SetDirty(projectileData);
        serializedObject.Update();
    }

    private void RemoveContract(Type contractType)
    {
        serializedObject.ApplyModifiedProperties();
        ProjectileData projectileData = (ProjectileData)target;
        Undo.RecordObject(projectileData, "Remove projectile contract");
        if (projectileData.TryRemoveContract(contractType))
            EditorUtility.SetDirty(projectileData);
        serializedObject.Update();
    }

    private bool IsProjectile()
    {
        return deliveryType == null
            || deliveryType.enumValueIndex
                == (int)ProjectileDeliveryType.Projectile;
    }

    private bool IsContact()
    {
        return deliveryType != null
            && deliveryType.enumValueIndex
                == (int)ProjectileDeliveryType.Contact;
    }

    private static string GetDisplayName(Type contractType)
    {
        if (Activator.CreateInstance(contractType) is ProjectileDataContract contract)
            return contract.DisplayName;

        return ObjectNames.NicifyVariableName(contractType.Name);
    }

    private static void DrawField(
        SerializedProperty parent,
        string name,
        string label)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }
}
