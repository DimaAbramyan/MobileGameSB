using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackGroundMove : MonoBehaviour
{
    Transform tr;
    public float speed = 0.001f;
    private Renderer rend;

    private SpriteRenderer spriteRenderer;
    void Start()
    {
        rend = GetComponent<Renderer>();
        
    }

    void Update()
    {
        float offsetX = Time.time * speed;
        rend.material.mainTextureOffset = new Vector2(0, offsetX);
    }
}
