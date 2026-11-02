#if UNITY_EDITOR || INCLUDE_TUTORIAL
using System;
using UnityEngine;
using Zenject;

namespace Game.Tutorial
{
    // Scene-scoped observer. It never generates input, changes ship selection or runs waves.
    public sealed class TutorialBattleBridge : IInitializable, ITickable, IDisposable
    {
        private readonly ITutorialService tutorial;
        private readonly PlayerController player;
        private readonly EnemyManager enemies;
        private readonly WaveManager waves;
        private ParentShip ship;
        private ParentShip[] team = Array.Empty<ParentShip>();
        private Vector3 position;
        private bool disposed, initialized;
        public TutorialBattleBridge(ITutorialService tutorial, PlayerController player, EnemyManager enemies, WaveManager waves)
        { this.tutorial = tutorial; this.player = player; this.enemies = enemies; this.waves = waves; }
        public void Initialize()
        {
            if (initialized || disposed) return;
            initialized = true;
            player.OnCurrentShipChanged += ShipChanged;
            player.OnTeamShipsRevived += TeamRevived;
            enemies.OnEnemyDestroyed += EnemyDestroyed;
            waves.OnWaveCleared += WaveCleared;
            position = player.transform.position;
            RebuildTeam(); BindShip(player.CurrentShip);
        }
        private void TeamRevived() { RebuildTeam(); BindShip(player.CurrentShip); position = player.transform.position; }
        private void RebuildTeam() => team = player.GetComponentsInChildren<ParentShip>(true);
        private void ShipChanged(ParentShip next)
        {
            if (ship != null && !ship.IsDestroyed && next != null && next != ship) tutorial.Signals.Switches++;
            BindShip(next); RebuildTeam(); position = player.transform.position;
        }
        private void BindShip(ParentShip next)
        {
            if (ship != null)
            {
                ship.OnDamageTaken -= Damage; ship.OnDied -= Died;
                if (ship.ActiveAbility != null) ship.ActiveAbility.Activated -= Ability;
            }
            ship = next;
            if (ship == null) return;
            ship.OnDamageTaken += Damage; ship.OnDied += Died;
            if (ship.ActiveAbility != null) ship.ActiveAbility.Activated += Ability;
        }
        private void Damage(float amount) { if (amount > 0f) tutorial.Signals.Damage++; }
        private void Died() => tutorial.Signals.PlayerDeaths++;
        private void Ability(ParentShip owner) { if (owner == ship) tutorial.Signals.Abilities++; }
        private void EnemyDestroyed(Enemy enemy) => tutorial.Signals.EnemyDeaths++;
        private void WaveCleared() => tutorial.Signals.Waves++;
        public void Tick()
        {
            if (disposed || player == null) return;
            // Actual root displacement; no synthetic movement or second input path.
            Vector3 now = player.transform.position;
            if (ship != null && !ship.IsDestroyed) tutorial.Signals.Movement += Vector2.Distance(position, now);
            position = now;
            int available = 0;
            foreach (var member in team) if (member != null && !member.IsDestroyed) available++;
            tutorial.AttachPlayer(player, available >= 2);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            player.OnCurrentShipChanged -= ShipChanged; player.OnTeamShipsRevived -= TeamRevived;
            enemies.OnEnemyDestroyed -= EnemyDestroyed; waves.OnWaveCleared -= WaveCleared;
            BindShip(null); tutorial.DetachPlayer(player);
        }
    }
}
#endif
