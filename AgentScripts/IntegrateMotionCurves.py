from pathlib import Path
import re

def edit(path, fn):
    p=Path(path)
    text=p.read_text(encoding='utf-8-sig')
    backup=Path('AgentArtifacts/MotionBefore')/(p.name+'.before.txt')
    backup.parent.mkdir(parents=True,exist_ok=True)
    if not backup.exists(): backup.write_bytes(p.read_bytes())
    updated=fn(text)
    if updated != text:
        p.write_bytes(updated.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))

def replace(t, a, b, count=1):
    assert t.count(a)>=count, a[:100]
    return t.replace(a,b,count)

def runtime(t):
    t=replace(t,'easeToNext = checkpoint.easeToNext','easeToNext = checkpoint.easeToNext,\n                curveSettings = GetWorldCurveSettings(checkpoint.curveSettings, pathCoordinateSpace)')
    t=replace(t,'checkpoints[currentIndex].motionToNext,\n                    curvedTime);','checkpoints[currentIndex].motionToNext,\n                    curvedTime, checkpoints[currentIndex].curveSettings, checkpoints[nextIndex].curveSettings);')
    start=t.index('    private Vector3 EvaluatePatrolSegment(')
    end=t.index('    private int GetPreviousPatrolIndex(',start)
    t=t[:start]+'''    private Vector3 EvaluatePatrolSegment(int segmentIndex, float time)
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return Vector3.zero;
        int nextIndex = GetNextPatrolIndex(segmentIndex);
        return DirectedWavePathEvaluator.EvaluateSegment(
            GetPatrolPointPosition(GetPreviousPatrolIndex(segmentIndex)),
            GetPatrolPointPosition(segmentIndex), GetPatrolPointPosition(nextIndex),
            GetPatrolPointPosition(GetNextPatrolIndex(nextIndex)), patrolPoints[segmentIndex].motionToNext, time,
            GetWorldCurveSettings(patrolPoints[segmentIndex].curveSettings, patrolCoordinateSpace),
            GetWorldCurveSettings(patrolPoints[nextIndex].curveSettings, patrolCoordinateSpace));
    }

    private DirectedWaveCurveSettings GetWorldCurveSettings(DirectedWaveCurveSettings settings,
        DirectedWaveCoordinateSpace space)
    {
        Vector3 origin = ToWorld(Vector3.zero, space);
        settings.inHandle = ToWorld(settings.inHandle, space) - origin;
        settings.outHandle = ToWorld(settings.outHandle, space) - origin;
        return settings;
    }

'''+t[end:]
    return t

def loop(t):
    t=replace(t,'checkpoints[currentIndex].motionToNext,\n            time);','checkpoints[currentIndex].motionToNext,\n            time, checkpoints[currentIndex].curveSettings, checkpoints[nextIndex].curveSettings);')
    return replace(t,'checkpoints[currentIndex].motionToNext,\n            normalizedDistance);','checkpoints[currentIndex].motionToNext,\n            normalizedDistance, checkpoints[currentIndex].curveSettings, checkpoints[nextIndex].curveSettings);')

def editor(t):
    t=replace(t,'public sealed class DirectedEnemySubWaveEditor : Editor','public sealed partial class DirectedEnemySubWaveEditor : Editor')
    # Use exactly the same geometry as runtime for route drawing and metrics.
    start=t.index('    private Vector3 EvaluatePatrolSegment(')
    end=t.index('    private int GetPreviousPatrolPointIndex(',start)
    t=t[:start]+'''    private Vector3 EvaluatePatrolSegment(int segmentIndex, float time)
    {
        if (patrolPoints == null || patrolPoints.arraySize == 0) return Vector3.zero;
        int next = GetNextPatrolPointIndex(segmentIndex);
        var wave = target as DirectedEnemySubWave;
        var space = (DirectedWaveCoordinateSpace)patrolCoordinateSpace.enumValueIndex;
        return DirectedWavePathEvaluator.EvaluateSegment(
            GetPatrolPointPosition(GetPreviousPatrolPointIndex(segmentIndex)),
            GetPatrolPointPosition(segmentIndex), GetPatrolPointPosition(next),
            GetPatrolPointPosition(GetNextPatrolPointIndex(next)),
            (DirectedWaveSegmentMotion)patrolPoints.GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("motionToNext").enumValueIndex,
            time, ReadWorldCurveSettings(patrolPoints.GetArrayElementAtIndex(segmentIndex), wave, space),
            ReadWorldCurveSettings(patrolPoints.GetArrayElementAtIndex(next), wave, space));
    }

'''+t[end:]
    start=t.index('    private static Vector3 EvaluateCheckpointSegment(')
    end=t.index('    private static float EvaluateCurve(',start)
    t=t[:start]+'''    private static Vector3 EvaluateCheckpointSegment(
        DirectedWaveRuntimeCheckpoint[] checkpoints, int segmentIndex, float time)
    {
        return DirectedWavePathEvaluator.EvaluateSegment(checkpoints, segmentIndex, time);
    }

'''+t[end:]
    t=re.sub(r'    private struct EditorPathCheckpoint\n    \{.*?\n    \}\n\n','',t,flags=re.S)
    t=t.replace('EditorPathCheckpoint','DirectedWaveRuntimeCheckpoint')
    t=replace(t,'.FindPropertyRelative("easeToNext")\n                    .animationCurveValue\n            };','.FindPropertyRelative("easeToNext")\n                    .animationCurveValue,\n                curveSettings = ReadWorldCurveSettings(checkpoint, wave, coordinateSpace)\n            };')
    # Numeric settings are inside existing change tracking, so timing/metric synchronization is retained.
    t=replace(t,'new GUIContent("Motion To Next"));\n                    EditorGUILayout.PropertyField(\n                        easeToNext,','new GUIContent("Motion To Next"));\n                    DrawSegmentCurveInspector(pathCheckpoints, i, false);\n                    EditorGUILayout.PropertyField(\n                        easeToNext,')
    t=replace(t,'new GUIContent("Motion To Next"));\n                    EditorGUILayout.PropertyField(\n                        easeToNext,','new GUIContent("Motion To Next"));\n                    DrawSegmentCurveInspector(patrolPoints, i, true);\n                    EditorGUILayout.PropertyField(\n                        easeToNext,')
    t=replace(t,'new GUIContent("Motion To Loop Start"));','new GUIContent("Motion To Loop Start"));\n                        DrawSegmentCurveInspector(pathCheckpoints, i, false);')
    t=replace(t,'        DrawCheckpointSceneHandles(wave);','        DrawCheckpointSceneHandles(wave);\n        DrawCurveSceneHandles(wave, pathCheckpoints, false);')
    t=replace(t,'        DrawPatrolRoute(centerWorld);','        DrawPatrolRoute(centerWorld);\n        DrawCurveSceneHandles(wave, patrolPoints, true);')
    return t

edit('Assets/Scripts/Gameplay/Waves/DirectedEnemySubWave.cs',runtime)
edit('Assets/Scripts/Gameplay/Waves/DirectedWaveEntranceLoopEvaluator.cs',loop)
edit('Assets/Editor/DirectedEnemySubWaveEditor.cs',editor)
print('Runtime, loop, Inspector and preview now use common curve settings.')
