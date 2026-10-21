using UnityEditor;
using UnityEngine;

public sealed partial class DirectedWaveAttackBehaviourEditor
{
    private SerializedProperty executionMode, attackSequence;

    private void DrawAttackSequence()
    {
        EditorGUILayout.HelpBox("Sequence выполняет шаги по порядку. Single/Multiple внутри паттерна сохранены. "
            + "Один шаг выполняет один цикл выбранной группы, включая Attack Cooldown. "
            + "Без Wait For Completion можно перекрывать атаки разных слотов; один слот не используется двумя шагами одновременно.", MessageType.Info);
        EditorGUILayout.PropertyField(attackSequence.FindPropertyRelative("sequenceMode"));
        if (attackSequence.FindPropertyRelative("sequenceMode").enumValueIndex != (int)DirectedWaveAttackSequenceMode.Once)
            EditorGUILayout.PropertyField(attackSequence.FindPropertyRelative("loopCount"));
        EditorGUILayout.PropertyField(attackSequence.FindPropertyRelative("startStep"));
        EditorGUILayout.PropertyField(attackSequence.FindPropertyRelative("restartOnReentry"),
            new GUIContent("Restart On Reentry", "Включено: начать сначала. Выключено: продолжить; прерванное выполнение текущего шага запускается заново."));
        EditorGUILayout.PropertyField(attackSequence.FindPropertyRelative("waitForPreviousAttack"));
        if (attackSequence.FindPropertyRelative("sequenceMode").enumValueIndex == (int)DirectedWaveAttackSequenceMode.Random)
            EditorGUILayout.PropertyField(attackSequence.FindPropertyRelative("seed"));
        var steps = attackSequence.FindPropertyRelative("steps");
        if (steps.arraySize == 0) EditorGUILayout.HelpBox("Добавьте Attack, Pause или Repeat Block.", MessageType.Warning);
        else if (attackSequence.FindPropertyRelative("startStep").intValue >= steps.arraySize)
            EditorGUILayout.HelpBox("Start Step exceeds the root step count; execution starts at the last root step.", MessageType.Warning);
        DrawSequenceSteps(steps, 0);
    }

    private void DrawSequenceSteps(SerializedProperty steps, int depth)
    {
        for (int i = 0; i < steps.arraySize; i++)
        {
            var step = steps.GetArrayElementAtIndex(i);
            if (step.managedReferenceValue == null)
                step.managedReferenceValue = new DirectedWaveAttackSequenceStep();
            var name = step.FindPropertyRelative("name");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            step.isExpanded = EditorGUILayout.Foldout(step.isExpanded, $"{i}: {name.stringValue}", true);
            bool changed = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("↑", GUILayout.Width(30))) { steps.MoveArrayElement(i, i - 1); changed = true; }
                using (new EditorGUI.DisabledScope(i + 1 == steps.arraySize))
                    if (GUILayout.Button("↓", GUILayout.Width(30))) { steps.MoveArrayElement(i, i + 1); changed = true; }
                if (GUILayout.Button("Duplicate"))
                {
                    var copy = JsonUtility.FromJson<DirectedWaveAttackSequenceStep>(JsonUtility.ToJson(step.managedReferenceValue));
                    steps.InsertArrayElementAtIndex(i + 1);
                    steps.GetArrayElementAtIndex(i + 1).managedReferenceValue = copy; changed = true;
                }
                if (GUILayout.Button("Remove")) { steps.DeleteArrayElementAtIndex(i); changed = true; }
            }
            if (!changed && step.isExpanded)
            {
                EditorGUILayout.PropertyField(name);
                EditorGUILayout.PropertyField(step.FindPropertyRelative("enabled"));
                var type = step.FindPropertyRelative("type");
                EditorGUILayout.PropertyField(type, new GUIContent("Step Type"));
                EditorGUILayout.PropertyField(step.FindPropertyRelative("repeatCount"));
                if (type.enumValueIndex == (int)DirectedWaveAttackSequenceStepType.RepeatBlock)
                {
                    if (depth + 1 >= DirectedWaveAttackSequenceCursor.MaximumDepth)
                        EditorGUILayout.HelpBox("Maximum Repeat Block nesting reached.", MessageType.Warning);
                    else DrawSequenceSteps(step.FindPropertyRelative("steps"), depth + 1);
                }
                else
                {
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("startDelay"), new GUIContent("Step Start Delay"));
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("delayAfterStep"));
                    if (type.enumValueIndex == (int)DirectedWaveAttackSequenceStepType.Pause)
                        EditorGUILayout.PropertyField(step.FindPropertyRelative("pauseDuration"));
                    else DrawSequenceAttackStep(step);
                }
            }
            EditorGUILayout.EndVertical();
            if (changed)
            {
                Undo.SetCurrentGroupName("Edit Attack Sequence Steps");
                serializedObject.ApplyModifiedProperties();
                ((DirectedWaveAttackBehaviour)target).InvalidateSequenceGuides(); SceneView.RepaintAll(); GUIUtility.ExitGUI();
            }
        }
        if (GUILayout.Button(depth == 0 ? "Add Sequence Step" : "Add Block Step"))
        {
            steps.arraySize++;
            steps.GetArrayElementAtIndex(steps.arraySize - 1).managedReferenceValue = new DirectedWaveAttackSequenceStep();
        }
    }

    private void DrawSequenceAttackStep(SerializedProperty step)
    {
        EditorGUILayout.PropertyField(step.FindPropertyRelative("waitForCompletion"));
        EditorGUILayout.PropertyField(step.FindPropertyRelative("attackSpeedMultiplier"));
        EditorGUILayout.PropertyField(step.FindPropertyRelative("waitForChildAttacks"));
        if (step.FindPropertyRelative("waitForChildAttacks").boolValue)
        {
            EditorGUILayout.PropertyField(step.FindPropertyRelative("unlimitedChildWait"),
                new GUIContent("Unlimited Child Wait", "Явно разрешить неограниченное ожидание. Источник с бесконечным временем жизни может никогда не завершиться."));
            if (!step.FindPropertyRelative("unlimitedChildWait").boolValue)
                EditorGUILayout.PropertyField(step.FindPropertyRelative("childWaitTimeout"));
        }
        var useSlots = step.FindPropertyRelative("overrideTargetSlots");
        EditorGUILayout.PropertyField(useSlots, new GUIContent("Target Slots Override"));
        var slots = step.FindPropertyRelative("targetSlots");
        if (useSlots.boolValue)
        {
            DrawEnemySlotSelection(slots, "Step Target Slots");
            if (slots.arraySize == 0) EditorGUILayout.HelpBox("No target slots selected: this attack step will finish without firing.", MessageType.Warning);
        }
        var pattern = step.FindPropertyRelative("pattern");
        DrawPostFormationAttackSettings(pattern.FindPropertyRelative("attackSettings"),
            pattern.FindPropertyRelative("attackPreset"), pattern.FindPropertyRelative("overridePresetAttacksPerSecond"),
            pattern.FindPropertyRelative("presetAttacksPerSecond"), pattern.FindPropertyRelative("overrideBaseProjectileSpeed"),
            pattern.FindPropertyRelative("baseProjectileSpeed"), -1, useSlots.boolValue ? slots : null, true);
    }
}
