using System.Collections;

using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Zenject;

public class CircleShip : Enemy
{
    [Inject] private DiContainer container;
    [SerializeField] private EnemyBullet EnBullet;
    private float Timer;
    private bool waveAttackControlled;
    
    private void Start()
    {
        Timer = Random.Range(_fireRate/10+1, _fireRate);
    }
    private void Update() 
    {
        if (!waveAttackControlled && EnBullet)
        Shoot(); 
        MoveForvard();
    }

    public void SetWaveAttackControl(bool isControlled)
    {
        waveAttackControlled = isControlled;
    }
    protected void Shoot()
    {
        Timer -= Time.deltaTime * FireRateMultiplier / Timer;
        if (Timer <= 0)
        {
            EnemyBullet projectile = container.InstantiatePrefabForComponent<EnemyBullet>(
                EnBullet,
                transform.position,
                Quaternion.identity,
                null);
            projectile.SetDamageMultiplier(DamageMultiplier);
            Timer = Random.Range(_fireRate/10+1, _fireRate);
        }
    }
    protected void MoveForvard()
    {
        Vector3 _position = new Vector3(0,-1, 0);
        transform.position += _position * this._speed * Time.deltaTime;
    }
}
