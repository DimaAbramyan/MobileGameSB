using UnityEngine;
using Zenject;

public class Projectile : MonoBehaviour
{
    public float speed { get; private set; }
    public float damage { get; private set; }
    public float baseDamage;
    public float maxLength { get; private set; }
    public Vector3 direction{ get; private set; }
    private float fadeDuration;
    public float fadeTime = 1f;
    private ParentShip owner;
    MovementStrategySO movement;
    ImpactBehaviorSO impact;
    ContiniousImpactBehaviorSO continiousImpact;
    ProjectileBehaviourSO[] behaviours;
    SpriteRenderer spriteRenderer;
    public ParentShip Owner { get; set; }
    Vector3 startPosition;
    public float GetDamage() => damage;

    public void Awake()
    {
        fadeDuration = 100;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    public void SetDamage(float newDamage)
    {
        damage = newDamage;
    }

    public void Fade(float speed)
    {
        fadeTime -= speed * Time.deltaTime;

        var c = spriteRenderer.color;
        c.a = fadeTime;
        spriteRenderer.color = c;

        if (fadeTime <= 0f)
        {
            Destroy(gameObject);
        }
    }
    public void Init(ProjectileParams param, 
        MovementStrategySO movement, 
        ImpactBehaviorSO impact, 
        ContiniousImpactBehaviorSO continiousImpact, 
        ProjectileBehaviourSO[] projectileBehaviour,
        ParentShip owner)
    {
        speed = param.speed;
        damage = param.damage;
        baseDamage = param.damage;
        maxLength = param.maxLength;
        direction = (param.maxAngle > 0)
            ? Quaternion.Euler(0, 0, Random.Range(-param.maxAngle, param.maxAngle)) * param.direction
            : param.direction;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle+90);
        startPosition = transform.position;
        this.behaviours = projectileBehaviour;
        this.continiousImpact = continiousImpact;
        this.movement = movement;
        this.impact = impact;
        Owner = owner;
    }

    void FixedUpdate()
    {
        movement?.Move(this);
        if (behaviours != null)
        {
            foreach (var b in behaviours)
                b.Tick(this);
        }
        if ((transform.position - startPosition).sqrMagnitude > maxLength * maxLength)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<iDamagable>(out var target))
            impact?.OnImpact(target, this);
    }
    void OnTriggerStay2D(Collider2D other)
    {
        if (other.TryGetComponent<iDamagable>(out var target))
            continiousImpact?.OnImpact(target, this);
    }
}