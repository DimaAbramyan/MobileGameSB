using UnityEngine;
using UnityEngine.Serialization;

public class Buff : MonoBehaviour
{
    [FormerlySerializedAs("speed")]
    [SerializeField, Min(0f), Tooltip(
        "Downward movement speed in world units per second.")]
    private float fallSpeed = 1f;

    private Rigidbody2D body;

    protected virtual void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    protected virtual void FixedUpdate()
    {
        Vector2 displacement = Vector2.down
            * fallSpeed
            * Time.fixedDeltaTime;

        if (body != null && body.simulated)
        {
            body.MovePosition(body.position + displacement);
            return;
        }

        transform.position += (Vector3)displacement;
    }

    private void OnValidate()
    {
        fallSpeed = Mathf.Max(0f, fallSpeed);
    }
}
