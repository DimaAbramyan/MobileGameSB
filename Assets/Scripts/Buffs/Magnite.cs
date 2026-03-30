using UnityEngine;

public class Magnite : MonoBehaviour
{
    private Rigidbody2D rb;

    public float forceAmount = 10f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        Rigidbody2D targetRb = collision.GetComponent<Rigidbody2D>();
        if (targetRb == null)
            return;

        Vector3 direction = (collision.transform.position - transform.position + new Vector3(0, 0.3f)).normalized;
        targetRb.AddForce(-direction * forceAmount);
    }
}