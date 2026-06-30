using UnityEngine;
using System.Collections.Generic;
using System.Collections;
public class CreateBladeAbility : ActiveAbility
{
    [SerializeField] GameObject blade;
    List<Vector2> savedPoints;
    public override bool Activate(ParentShip owner)
    {
        savedPoints = new List<Vector2> ();
        StartCoroutine(CollectAndSpawn());
        return true;
    }
    private IEnumerator CollectAndSpawn()
    {
        yield return StartCoroutine(CollectPoints());
        SpawnBlade();
    }
    IEnumerator CollectPoints()
    {
        for (int i = 0; i < 30; i++)
        {
            yield return new WaitForSeconds(0.10f);
            savedPoints.Add(transform.position);
        }
    }
    public void SpawnBlade()
    {
        GameObject trail = Instantiate(blade, transform.position, Quaternion.identity);
        PolygonCollider2D poly = trail.GetComponent<PolygonCollider2D>();
        Rigidbody2D rb = trail.GetComponent<Rigidbody2D>();

        poly.pathCount = 1;
        poly.SetPath(0, savedPoints);
        savedPoints.Clear();
    }
}
