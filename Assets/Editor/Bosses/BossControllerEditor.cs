using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BossController))]
public sealed class BossControllerEditor : Editor
{
    private bool enemySettings, emitterSettings;
    private string previewPath;
    private Vector2 previewTarget = new(0f, -3f);

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        enemySettings = EditorGUILayout.Foldout(enemySettings, "Health, Shield & Enemy Properties", true);
        if (enemySettings) DrawPropertiesExcluding(serializedObject, "m_Script", "phases");
        DrawEmitter();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Boss Phases", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Each attack runs one shared Attack Pattern cycle, including Burst, delayed Sector launches and Attack Cooldown. Phase delays are additional. Sequence uses the existing Pause / Repeat player. Wave slots, spawn scheduling and dive movement do not apply to a boss.", MessageType.Info);
        var phases = serializedObject.FindProperty("phases");
        DrawPhaseWarnings(phases);
        for (int i = 0; i < phases.arraySize; i++)
        {
            var phase = phases.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                phase.isExpanded = EditorGUILayout.Foldout(phase.isExpanded,
                    $"{i + 1}. {phase.FindPropertyRelative("phaseName").stringValue}", true);
                DrawListButtons(phases, i);
                if (!phase.isExpanded) continue;
                Field(phase, "phaseName");
                using (new EditorGUI.DisabledScope(i == phases.arraySize - 1))
                    Field(phase, "nextPhaseHealthThresholdPercent", "Next Phase At HP %");
                Field(phase, "startDelay");
                Field(phase, "executionMode");
                if (phase.FindPropertyRelative("executionMode").enumValueIndex == (int)DirectedWaveAttackExecutionMode.Sequence)
                    DrawSequence(phase.FindPropertyRelative("sequence"));
                else
                {
                    Field(phase, "selectionMode");
                    if (phase.FindPropertyRelative("selectionMode").enumValueIndex == (int)BossAttackSelectionMode.Random)
                        Field(phase, "randomSeed");
                    Field(phase, "delayBetweenAttacks");
                    DrawPatterns(phase.FindPropertyRelative("attacks"));
                }
            }
        }
        if (GUILayout.Button("Add Phase"))
        {
            phases.arraySize++;
            var phase = phases.GetArrayElementAtIndex(phases.arraySize - 1);
            phase.boxedValue = new BossPhaseDefinition();
            phase.isExpanded = true;
        }
        EnemyShootingPreview.DrawPreviewControls();
        previewTarget = EditorGUILayout.Vector2Field("Preview Player Position", previewTarget);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
        serializedObject.ApplyModifiedProperties();
        if (Application.isPlaying)
            EditorGUILayout.LabelField("Active Phase", (((BossController)target).CurrentPhaseIndex + 1).ToString());
    }

    private void DrawEmitter()
    {
        var executor = ((BossController)target).GetComponent<SimpleEnemyAttackExecutor>();
        if (executor == null)
        {
            EditorGUILayout.HelpBox("Add SimpleEnemyAttackExecutor to the boss root.", MessageType.Error);
            return;
        }
        emitterSettings = EditorGUILayout.Foldout(emitterSettings, "Shared Executor & Emitter", true);
        if (!emitterSettings) return;
        var binding = new SerializedObject(executor);
        binding.Update();
        foreach (string field in new[] { "defaultProjectilePrefab", "homingProjectilePrefab", "burstAtPointProjectilePrefab",
            "projectileSpawnPoint", "projectileSpawnOffset", "rotationTarget", "facingTarget" })
            EditorGUILayout.PropertyField(binding.FindProperty(field));
        EditorGUILayout.HelpBox("Boss controls this executor even during pauses. Autonomous Attack is not used. Assign an EnemyBullet prefab with EnemyProjectileAuthoring, or select a projectile preset in the attack.", MessageType.None);
        binding.ApplyModifiedProperties();
    }

    private void DrawPatterns(SerializedProperty patterns)
    {
        if (patterns.arraySize == 0) EditorGUILayout.HelpBox("Add an Attack Pattern.", MessageType.Warning);
        for (int i = 0; i < patterns.arraySize; i++)
        {
            var pattern = patterns.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                pattern.isExpanded = EditorGUILayout.Foldout(pattern.isExpanded, $"Attack {i + 1}", true);
                DrawListButtons(patterns, i);
                if (pattern.isExpanded) DrawPattern(pattern);
            }
        }
        if (GUILayout.Button("Add Attack Pattern"))
        {
            patterns.arraySize++;
            var pattern = patterns.GetArrayElementAtIndex(patterns.arraySize - 1);
            pattern.boxedValue = new DirectedWaveAttackPattern();
            InitializePattern(pattern);
            pattern.isExpanded = true;
        }
    }

    private static void InitializePattern(SerializedProperty pattern)
        => pattern.FindPropertyRelative("attackSettings").FindPropertyRelative("burstSettingsSource").enumValueIndex
            = (int)DirectedWaveBurstSettingsSource.WaveOverride;

    private void DrawPattern(SerializedProperty pattern)
    {
        Field(pattern, "attackPreset", "Wave Attack Preset (Optional)");
        var preset = pattern.FindPropertyRelative("attackPreset");
        if (preset.objectReferenceValue != null)
        {
            EditorGUILayout.HelpBox("Preset provides firing and projectile settings. Enemy-group scheduling and movement are ignored. Use Preset As Local to edit its firing settings here.", MessageType.None);
            if (GUILayout.Button("Use Preset As Local"))
            {
                // Apply pending edits before calling the existing preset materializer.
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(target, "Use Boss Attack Preset As Local");
                var local = (DirectedWaveAttackPattern)pattern.boxedValue;
                local.UsePresetAsLocalSettings();
                pattern.boxedValue = local;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                serializedObject.Update();
            }
        }
        using (new EditorGUI.DisabledScope(preset.objectReferenceValue != null))
        {
            var settings = pattern.FindPropertyRelative("attackSettings");
            Field(settings, "fireMode");
            switch ((DirectedWaveAttackFireMode)settings.FindPropertyRelative("fireMode").enumValueIndex)
            {
                case DirectedWaveAttackFireMode.Aimed: Field(settings, "aimMode"); break;
                case DirectedWaveAttackFireMode.Stationary: Field(settings, "stationaryFireSettings"); break;
                case DirectedWaveAttackFireMode.ForwardWhenPlayerAhead: Field(settings, "forwardFireHalfAngle"); break;
            }
            Field(settings, "burstSettingsSource", "Attack Pattern Source");
            bool fromExecutor = settings.FindPropertyRelative("burstSettingsSource").enumValueIndex
                == (int)DirectedWaveBurstSettingsSource.EnemySettings;
            if (!fromExecutor)
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("waveBurstSettings"), new GUIContent("Attack Pattern"), true);
            else
            {
                EditorGUILayout.HelpBox("Enemy Settings reads the boss's shared executor profile. Wave Override uses this attack's own profile.", MessageType.None);
                Field(settings, "overrideEnemyAttackCooldown");
                if (settings.FindPropertyRelative("overrideEnemyAttackCooldown").boolValue)
                    Field(settings, "enemyAttackCooldown");
                var executor = ((BossController)target).GetComponent<SimpleEnemyAttackExecutor>();
                if (executor != null)
                {
                    var executorObject = new SerializedObject(executor);
                    executorObject.Update();
                    EditorGUILayout.PropertyField(executorObject.FindProperty("burstAttackSettings"),
                        new GUIContent("Executor Attack Pattern"), true);
                    executorObject.ApplyModifiedProperties();
                }
            }
        }
        Field(pattern, "overrideBaseProjectileSpeed");
        if (pattern.FindPropertyRelative("overrideBaseProjectileSpeed").boolValue)
        {
            Field(pattern, "baseProjectileSpeed");
            Field(pattern, "baseProjectileSpeedRandomizeOn");
        }
        if (GUILayout.Button(previewPath == pattern.propertyPath ? "Previewing This Attack" : "Preview This Attack"))
        {
            previewPath = pattern.propertyPath;
            SceneView.RepaintAll();
        }
    }

    private void DrawSequence(SerializedProperty sequence)
    {
        Field(sequence, "sequenceMode");
        if (sequence.FindPropertyRelative("sequenceMode").enumValueIndex != (int)DirectedWaveAttackSequenceMode.Once)
            Field(sequence, "loopCount");
        Field(sequence, "startStep");
        Field(sequence, "waitForPreviousAttack");
        if (sequence.FindPropertyRelative("sequenceMode").enumValueIndex == (int)DirectedWaveAttackSequenceMode.Random)
            Field(sequence, "seed");
        EditorGUILayout.HelpBox("One boss is one attack source: root attacks cannot overlap on the same executor. Already spawned projectiles may overlap. Re-enabling the boss restarts its current phase.", MessageType.None);
        DrawSteps(sequence.FindPropertyRelative("steps"), 0);
    }

    private void DrawSteps(SerializedProperty steps, int depth)
    {
        for (int i = 0; i < steps.arraySize; i++)
        {
            var step = steps.GetArrayElementAtIndex(i);
            if (step.managedReferenceValue == null) step.managedReferenceValue = new DirectedWaveAttackSequenceStep();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                step.isExpanded = EditorGUILayout.Foldout(step.isExpanded,
                    $"{i + 1}. {step.FindPropertyRelative("name").stringValue}", true);
                DrawListButtons(steps, i);
                if (!step.isExpanded) continue;
                Field(step, "name"); Field(step, "enabled"); Field(step, "type"); Field(step, "repeatCount");
                var type = (DirectedWaveAttackSequenceStepType)step.FindPropertyRelative("type").enumValueIndex;
                if (type == DirectedWaveAttackSequenceStepType.RepeatBlock)
                {
                    if (depth + 1 < DirectedWaveAttackSequenceCursor.MaximumDepth)
                        DrawSteps(step.FindPropertyRelative("steps"), depth + 1);
                    else EditorGUILayout.HelpBox("Maximum Repeat Block nesting reached.", MessageType.Warning);
                    continue;
                }
                Field(step, "startDelay"); Field(step, "delayAfterStep");
                if (type == DirectedWaveAttackSequenceStepType.Pause) { Field(step, "pauseDuration"); continue; }
                Field(step, "waitForCompletion"); Field(step, "attackSpeedMultiplier"); Field(step, "waitForChildAttacks");
                if (step.FindPropertyRelative("waitForChildAttacks").boolValue)
                {
                    Field(step, "unlimitedChildWait");
                    if (!step.FindPropertyRelative("unlimitedChildWait").boolValue) Field(step, "childWaitTimeout");
                }
                DrawPattern(step.FindPropertyRelative("pattern"));
            }
        }
        if (GUILayout.Button(depth == 0 ? "Add Sequence Step" : "Add Block Step"))
        {
            steps.arraySize++;
            var step = steps.GetArrayElementAtIndex(steps.arraySize - 1);
            step.managedReferenceValue = new DirectedWaveAttackSequenceStep();
            InitializePattern(step.FindPropertyRelative("pattern"));
            step.isExpanded = true;
        }
    }

    private void DrawListButtons(SerializedProperty list, int index)
    {
        bool changed = false;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(index == 0))
                if (GUILayout.Button("↑", GUILayout.Width(30))) { list.MoveArrayElement(index, index - 1); changed = true; }
            using (new EditorGUI.DisabledScope(index + 1 == list.arraySize))
                if (GUILayout.Button("↓", GUILayout.Width(30))) { list.MoveArrayElement(index, index + 1); changed = true; }
            if (GUILayout.Button("Duplicate"))
            {
                var element = list.GetArrayElementAtIndex(index);
                object copy = JsonUtility.FromJson(JsonUtility.ToJson(element.boxedValue), element.boxedValue.GetType());
                list.InsertArrayElementAtIndex(index + 1);
                var inserted = list.GetArrayElementAtIndex(index + 1);
                if (inserted.propertyType == SerializedPropertyType.ManagedReference) inserted.managedReferenceValue = copy;
                else inserted.boxedValue = copy;
                changed = true;
            }
            if (GUILayout.Button("Remove")) { list.DeleteArrayElementAtIndex(index); changed = true; }
        }
        if (!changed) return;
        serializedObject.ApplyModifiedProperties();
        SceneView.RepaintAll();
        GUIUtility.ExitGUI();
    }

    private static void Field(SerializedProperty parent, string name, string label = null)
    {
        var property = parent.FindPropertyRelative(name);
        if (label == null) EditorGUILayout.PropertyField(property, true);
        else EditorGUILayout.PropertyField(property, new GUIContent(label), true);
    }

    private static void DrawPhaseWarnings(SerializedProperty phases)
    {
        if (phases.arraySize == 0) EditorGUILayout.HelpBox("Add at least one boss phase.", MessageType.Warning);
        float previous = 100f;
        for (int i = 0; i + 1 < phases.arraySize; i++)
        {
            float threshold = phases.GetArrayElementAtIndex(i).FindPropertyRelative("nextPhaseHealthThresholdPercent").floatValue;
            if (threshold <= 0f || threshold >= previous)
                EditorGUILayout.HelpBox($"Phase {i + 1}: use a positive HP threshold lower than the preceding threshold. Otherwise phases may never start or be skipped.", MessageType.Warning);
            previous = threshold;
        }
    }

    private void OnSceneGUI()
    {
        if (Application.isPlaying || !EnemyShootingPreview.ShowFormationVolley) return;
        var boss = (BossController)target;
        var executor = boss.GetComponent<SimpleEnemyAttackExecutor>();
        if (executor == null) return;
        serializedObject.Update();
        var property = string.IsNullOrEmpty(previewPath) ? null : serializedObject.FindProperty(previewPath);
        var pattern = property?.boxedValue as DirectedWaveAttackPattern;
        if (pattern == null && boss.Phases.Length > 0)
        {
            var phase = boss.Phases[0];
            pattern = phase.ExecutionMode == DirectedWaveAttackExecutionMode.Sequence
                ? FirstPattern(phase.Sequence.steps) : phase.Attacks.Length > 0 ? phase.Attacks[0] : null;
        }
        if (pattern == null) return;
        var settings = new DirectedWaveAttackSettings();
        pattern.CopyResolvedSettingsTo(settings);
        var profile = EnemyAttackSettingsResolver.Resolve(executor, settings.UsesEnemyBurstSettings, settings.WaveBurstSettings);
        if (profile == null) return;
        profile = profile.ResolveAllPresets();
        for (int i = 0; i < profile.PatternCount; i++)
        {
            var shooting = profile.GetPattern(i).Shooting;
            var firing = shooting.PresetFiring;
            var mode = firing.enabled ? firing.fireMode : settings.FireMode;
            if (mode == DirectedWaveAttackFireMode.None) continue;
            var stationary = firing.enabled ? firing.stationaryFireSettings : settings.StationaryFireSettings;
            bool point = mode == DirectedWaveAttackFireMode.Stationary
                && !stationary.PreservesLegacyForward && stationary.DirectionMode == EnemyStationaryDirectionMode.WorldPoint;
            Vector3? aimTarget = point ? (Vector3)stationary.WorldPoint : mode == DirectedWaveAttackFireMode.Aimed ? (Vector3)previewTarget : null;
            Vector3 direction = mode == DirectedWaveAttackFireMode.Stationary && !point && !stationary.PreservesLegacyForward
                ? stationary.Direction : executor.transform.up;
            EnemyShootingPreview.DrawExecutorPattern(executor, shooting, boss.transform.position, 0f,
                requestedDirection: direction, aimed: mode == DirectedWaveAttackFireMode.Aimed, target: aimTarget,
                stationary: mode == DirectedWaveAttackFireMode.Stationary, originPlayerPosition: previewTarget,
                rayBudget: Mathf.Max(32, 512 / Mathf.Max(1, profile.PatternCount)), staticLayoutPose: true);
        }
    }

    private static DirectedWaveAttackPattern FirstPattern(System.Collections.Generic.List<DirectedWaveAttackSequenceStep> steps, int depth = 0)
    {
        if (steps == null || depth >= DirectedWaveAttackSequenceCursor.MaximumDepth) return null;
        foreach (var step in steps)
        {
            if (step == null || !step.enabled) continue;
            if (step.type == DirectedWaveAttackSequenceStepType.Attack) return step.pattern;
            if (step.type == DirectedWaveAttackSequenceStepType.RepeatBlock)
            {
                var pattern = FirstPattern(step.steps, depth + 1);
                if (pattern != null) return pattern;
            }
        }
        return null;
    }
}
