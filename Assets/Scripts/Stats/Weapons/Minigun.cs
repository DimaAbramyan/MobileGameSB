using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using System.Threading;
using TMPro;
namespace Minigun_name
{
    public class Minigun : Weapon
    {
        private ParentShip ship;
        public Pellet pellet;
        public Bullet bulletPref;
        public Transform bulletSpawn;
        private float _currentFireRate;
        private int currentLvl;
        private List<float> thisfireRate = new List<float> {0.4f, 0.3f, 0.25f, 0.2f, 0.15f };
        public static float[] rotationsAnglesPerLvL = { 5, 7.5f, 10f, 12.5f, 15f };
        private int shotGun_reload = 100;
        public override void Shoot()
        {
            _currentFireRate = thisfireRate[ship.ShipData.currentLvl];
            float randomAngle = Random.Range(-rotationsAnglesPerLvL[ship.ShipData.currentLvl], rotationsAnglesPerLvL[ship.ShipData.currentLvl]);
            Instantiate(bulletPref, bulletSpawn.position, Quaternion.Euler(0f, 0f, randomAngle));
        }
        private void Update()
        {
            
            if (_currentFireRate > 0)
            {
                _currentFireRate -= Time.deltaTime / _currentFireRate;
            }
            else
            {
                Shoot();
            }
        }
        private void Start()
        {
            ship = GetComponentInParent<ParentShip>();
        }
    }

}