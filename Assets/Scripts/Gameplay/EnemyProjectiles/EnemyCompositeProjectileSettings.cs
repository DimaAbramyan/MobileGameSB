using System;
using Unity.Mathematics;
using UnityEngine;

public enum EnemyCompositeShape { Circle, [InspectorName("Regular Polygon")] RegularPolygon, [InspectorName("Segmented Ring")] SegmentedRing }

public enum EnemyCompositeRadiusMode { Automatic, Custom }
public enum EnemyCompositeRotationMode { None, Continuous, PingPong, AlignToMovement }

[Serializable]
public sealed class EnemyCompositeProjectileSettings
{
    [SerializeField] private EnemyCompositeShape shape;
    public EnemyCompositeShape Shape => shape;
    public void SetShape(EnemyCompositeShape value) => shape = value;

    [SerializeField, Range(1,256)] private int componentCount = 12;
    [SerializeField, Range(3,64)] private int numberOfSides = 4;
    [SerializeField, Range(2,65)] private int projectilesPerSide = 4;
    [SerializeField, Range(1,128)] private int segmentCount = 3;
    [SerializeField, Range(1,128)] private int projectilesPerSegment = 4;
    [SerializeField, Min(0)] private float gapAngle = 20;
    [SerializeField] private EnemyCompositeRadiusMode radiusMode;
    [SerializeField, Min(.001f)] private float radius = 1;
    [SerializeField, Min(0)] private float spacingGap;
    [SerializeField] private float startAngle;
    [SerializeField] private EnemyCompositeRotationMode rotationMode;
    [SerializeField, Randomizable(nameof(initialRotationRandom), scopes: EnemyNumericScopes.All, defaultScope: EnemyNumericRandomizeOn.PerProjectile)] private float initialRotation;
    [SerializeField, HideInInspector] private RandomFloatRange initialRotationRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    public bool UsesNumericRandomization => EnemyNumericSample.Enabled(initialRotationRandom);
    [SerializeField] private float angularSpeed = 60;
    [SerializeField] private float minAngle = -45, maxAngle = 45;
    [SerializeField, Min(.01f)] private float componentScale = 1;
    [SerializeField] private bool overrideHitRadius;
    [SerializeField, Min(.001f)] private float hitRadius = .1f;
    [SerializeField, Min(0)] private float damage = 1;
    [SerializeField] private EnemyDamageType damageType = EnemyDamageType.Default;

    public EnemyCompositeProjectileSpec Resolve(EnemyProjectileType type, EnemyNumericSample numeric = default) => new EnemyCompositeProjectileSpec {
        // Keep the existing internal geometry IDs; only the authoring choice is unified.
        Type=type == EnemyProjectileType.Composite ? (EnemyProjectileType)(4 + (int)shape) : type, Count=math.clamp(componentCount,1,256), Sides=math.clamp(numberOfSides,3,64),
        PerSide=math.clamp(projectilesPerSide,2,65), Segments=math.clamp(segmentCount,1,128),
        PerSegment=math.clamp(projectilesPerSegment,1,128), GapAngle=math.max(0,Finite(gapAngle)),
        RadiusMode=radiusMode, Radius=math.max(.001f,Finite(radius)), SpacingGap=math.max(0,Finite(spacingGap)), StartAngle=Finite(startAngle),
        RotationMode=rotationMode, InitialRotation=Finite(numeric.Float(initialRotationRandom, initialRotation, 1701u)), AngularSpeed=Finite(angularSpeed),
        MinAngle=math.min(Finite(minAngle),Finite(maxAngle)), MaxAngle=math.max(Finite(minAngle),Finite(maxAngle)),
        ComponentScale=math.max(.01f,Finite(componentScale)), OverrideHitRadius=overrideHitRadius,
        HitRadius=math.max(.001f,Finite(hitRadius)), Damage=math.max(0,Finite(damage)), DamageType=damageType };
    private static float Finite(float value) => float.IsFinite(value) ? value : 0;
    public EnemyCompositeProjectileSettings Copy() { var copy=new EnemyCompositeProjectileSettings(); JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this),copy); return copy; }
}

public struct EnemyCompositeProjectileSpec
{
    public EnemyProjectileType Type;
    public int Count, Sides, PerSide, Segments, PerSegment;
    public EnemyCompositeRadiusMode RadiusMode;
    public float Radius, SpacingGap, GapAngle, StartAngle;
    public EnemyCompositeRotationMode RotationMode;
    public float InitialRotation, AngularSpeed, MinAngle, MaxAngle, ComponentScale, HitRadius, Damage;
    public bool OverrideHitRadius;
    public EnemyDamageType DamageType;
    public bool Enabled => Type >= EnemyProjectileType.CompositeCircle && Type <= EnemyProjectileType.CompositeSegmentedRing;
    // Limits whole figures; never silently truncates a polygon or one segment.
    public int ComponentCount => Type == EnemyProjectileType.CompositePolygon ? Sides * PolygonIntervals
        : Type == EnemyProjectileType.CompositeSegmentedRing ? Segments * SegmentElements : math.clamp(Count,1,256);
    public int PolygonIntervals => math.clamp(PerSide-1,1,256/math.max(3,Sides));
    public int SegmentElements => math.clamp(PerSegment,1,256/math.max(1,Segments));
    public int EntityCost => Enabled ? ComponentCount + 1 : 1;
    public float EffectiveGapAngle => math.clamp(GapAngle,0,360f/math.max(1,Segments)-.01f);
    public float ResolveRadius(float componentRadius)
    {
        if(RadiusMode == EnemyCompositeRadiusMode.Custom) return math.max(.001f,Radius);
        float spacing=2*componentRadius+SpacingGap;
        if(Type == EnemyProjectileType.CompositePolygon) return PolygonIntervals*spacing/(2*math.sin(math.PI/Sides));
        if(ComponentCount==1) return 0;
        float angle=2*math.PI/ComponentCount;
        if(Type == EnemyProjectileType.CompositeSegmentedRing)
        {
            if(SegmentElements<=1) angle=2*math.PI/Segments;
            else
            {
                float gap=math.radians(EffectiveGapAngle);
                angle=(2*math.PI/Segments-gap)/SegmentElements;
                // Half an element step is reserved at both ends, so Gap=0 never duplicates endpoints.
                angle=math.min(angle,2*math.PI/Segments-(SegmentElements-1)*angle);
            }
        }
        return spacing/(2*math.max(.000001f,math.sin(angle*.5f)));
    }
    public float2 Offset(int index,float resolvedRadius)
    {
        float angle=StartAngle;
        if(Type == EnemyProjectileType.CompositePolygon)
        {
            int side=index/PolygonIntervals, part=index%PolygonIntervals;
            return math.lerp(Direction(angle+360f*side/Sides),Direction(angle+360f*(side+1)/Sides),(float)part/PolygonIntervals)*resolvedRadius;
        }
        if(Type == EnemyProjectileType.CompositeSegmentedRing)
        {
            int segment=index/SegmentElements, part=index%SegmentElements;
            float width=360f/Segments-EffectiveGapAngle;
            angle+=360f*segment/Segments+(part+.5f)*width/SegmentElements;
        }
        else angle+=360f*index/ComponentCount;
        return Direction(angle)*resolvedRadius;
    }
    private static float2 Direction(float degrees) { math.sincos(math.radians(degrees),out float s,out float c); return new float2(s,c); }
    public float Rotation(float age,float2 motion)
    {
        switch(RotationMode)
        {
            case EnemyCompositeRotationMode.Continuous: return InitialRotation+AngularSpeed*age;
            case EnemyCompositeRotationMode.AlignToMovement:
                return InitialRotation+(math.lengthsq(motion)>.00000001f ? math.degrees(math.atan2(motion.x,motion.y)) : 0);
            case EnemyCompositeRotationMode.PingPong:
                float span=MaxAngle-MinAngle;
                if(span<=.00001f)return MinAngle;
                float phase=math.clamp(InitialRotation,MinAngle,MaxAngle)-MinAngle+AngularSpeed*age;
                phase=math.fmod(math.fmod(phase,2*span)+2*span,2*span);
                return MinAngle+span-math.abs(phase-span);
            default:return InitialRotation;
        }
    }
    public static float2 Rotate(float2 offset,float degrees)
    { math.sincos(math.radians(degrees),out float s,out float c); return new float2(offset.x*c+offset.y*s,-offset.x*s+offset.y*c); }
}
