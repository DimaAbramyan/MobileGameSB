// Typed adapters for existing contracts. New contracts may override CollectDisplayStats directly.
// Generated offline from public APIs: no runtime reflection or gameplay-source rewriting.
public static class StatContractDeclarations
{
    public static void Collect(IStatValueProvider value, StatSourceBuilder s)
    {
        switch (value)
        {
            case WeaponMetaBasicStats v:
            {
                s.Add(nameof(v.ReloadTime), "Reload Time", () => v.ReloadTime);
                s.Add(nameof(v.Damage), "Damage", () => v.Damage);
                s.Add(nameof(v.Range), "Range", () => v.Range);
                s.Add(nameof(v.ProjectileSpeed), "Projectile Speed", () => v.ProjectileSpeed);
                s.Add(nameof(v.Angle), "Angle", () => v.Angle);
                s.Add(nameof(v.InitialDirectionAngle), "Initial Direction Angle", () => v.InitialDirectionAngle);
                return;
            }
            case BurstFireWeaponMetaContract v:
            {
                s.Add(nameof(v.VolleysPerActivation), "Volleys Per Activation", () => v.VolleysPerActivation);
                s.Add(nameof(v.ProjectilesPerVolley), "Projectiles Per Volley", () => v.ProjectilesPerVolley);
                s.Add(nameof(v.DelayBetweenVolleys), "Delay Between Volleys", () => v.DelayBetweenVolleys);
                return;
            }
            case SweepFireWeaponMetaContract v:
            {
                s.Add(nameof(v.TraversalDuration), "Traversal Duration", () => v.TraversalDuration);
                return;
            }
            case FanFireWeaponMetaContract v:
            {
                s.Add(nameof(v.IsEnabled), "Is Enabled", () => v.IsEnabled);
                s.Add(nameof(v.CyclesPartialFanGroups), "Cycles Partial Fan Groups", () => v.CyclesPartialFanGroups);
                s.Add(nameof(v.ProjectileCount), "Projectile Count", () => v.ProjectileCount);
                s.Add(nameof(v.TotalSpreadAngle), "Total Spread Angle", () => v.TotalSpreadAngle);
                s.Add(nameof(v.CustomAngles), "Custom Angles", () => v.CustomAngles);
                s.Add(nameof(v.UsePartialFan), "Use Partial Fan", () => v.UsePartialFan);
                if (v.PartialFanGroups != null) foreach (var g in v.PartialFanGroups) if (g != null) s.Nested(g, g.StatSourceId, "Fan Group " + g.StatSourceId);
                return;
            }
            case FanFireAngleGroup v:
            {
                s.Add(nameof(v.ProjectileCount), "Projectile Count", () => v.ProjectileCount);
                s.Add(nameof(v.AngleOffset), "Angle Offset", () => v.AngleOffset);
                return;
            }
            case TargetingWeaponMetaContract v:
            {
                s.Add(nameof(v.MaxTargets), "Max Targets", () => v.MaxTargets);
                s.Add(nameof(v.TargetSearchRadius), "Target Search Radius", () => v.TargetSearchRadius);
                return;
            }
            case ContinuousDamageWeaponMetaContract v:
            {
                s.Add(nameof(v.DamageTickInterval), "Damage Tick Interval", () => v.DamageTickInterval);
                return;
            }
            case HomingWeaponMetaContract v:
            {
                s.Add(nameof(v.RotationSpeed), "Rotation Speed", () => v.RotationSpeed);
                return;
            }
            case ShipMetaBasicStats v:
            {
                s.Add(nameof(v.MaximumHealthPoints), "Maximum Health Points", () => v.MaximumHealthPoints);
                s.Add(nameof(v.MaximumShieldPoints), "Maximum Shield Points", () => v.MaximumShieldPoints);
                s.Add(nameof(v.HealthRegenRatePercent), "Health Regen Rate Percent", () => v.HealthRegenRatePercent);
                s.Add(nameof(v.ShieldRegenRatePercent), "Shield Regen Rate Percent", () => v.ShieldRegenRatePercent);
                s.Add(nameof(v.Speed), "Speed", () => v.Speed);
                s.Add(nameof(v.MaximumEnergy), "Maximum Energy", () => v.MaximumEnergy);
                return;
            }
            case AbilityMetaRecoverySettings v:
            {
                s.Add(nameof(v.Mode), "Mode", () => v.Mode);
                s.Add(nameof(v.Cooldown), "Cooldown", () => v.Cooldown);
                s.Add(nameof(v.MaxCharges), "MaxCharges", () => v.MaxCharges);
                s.Add(nameof(v.ToggleMaximumTime), "ToggleMaximumTime", () => v.ToggleMaximumTime);
                s.Add(nameof(v.ToggleTimeCostPerSecond), "ToggleTimeCostPerSecond", () => v.ToggleTimeCostPerSecond);
                s.Add(nameof(v.ToggleRechargeStartTime), "ToggleRechargeStartTime", () => v.ToggleRechargeStartTime);
                s.Add(nameof(v.ToggleRechargeDuration), "ToggleRechargeDuration", () => v.ToggleRechargeDuration);
                return;
            }
            case AbilityRecoveryShipMetaContract v:
            {
                s.Add(nameof(v.AbilityMode), "Ability Mode", () => v.AbilityMode);
                s.Nested(v.Settings, "recovery", "Recovery Settings");
                return;
            }
            case BlackHoleShipMetaContract v:
            {
                s.Add(nameof(v.ActiveDuration), "Active Duration", () => v.ActiveDuration);
                s.Add(nameof(v.ActiveCooldown), "Active Cooldown", () => v.ActiveCooldown);
                s.Add(nameof(v.FrontSpeedMultiplier), "Front Speed Multiplier", () => v.FrontSpeedMultiplier);
                s.Add(nameof(v.RearSpeedMultiplier), "Rear Speed Multiplier", () => v.RearSpeedMultiplier);
                s.Add(nameof(v.RewriteAttackSpeed), "Rewrite Attack Speed", () => v.RewriteAttackSpeed);
                s.Add(nameof(v.EnablePassiveAbility), "Enable Passive Ability", () => v.EnablePassiveAbility);
                s.Add(nameof(v.ProjectileDestructionPolicy), "Projectile Destruction Policy", () => v.ProjectileDestructionPolicy);
                s.Add(nameof(v.Duration), "Duration", () => v.Duration);
                s.Add(nameof(v.Damage), "Damage", () => v.Damage);
                s.Add(nameof(v.DamageType), "Damage Type", () => v.DamageType);
                s.Add(nameof(v.ProjectileSlowRadius), "Projectile Slow Radius", () => v.ProjectileSlowRadius);
                s.Add(nameof(v.FrontFullEffectAngle), "Front Full Effect Angle", () => v.FrontFullEffectAngle);
                s.Add(nameof(v.FrontTransitionAngle), "Front Transition Angle", () => v.FrontTransitionAngle);
                s.Add(nameof(v.BackFullEffectAngle), "Back Full Effect Angle", () => v.BackFullEffectAngle);
                s.Add(nameof(v.BackTransitionAngle), "Back Transition Angle", () => v.BackTransitionAngle);
                s.Add(nameof(v.MaximumProjectileSpeedMultiplier), "Maximum Projectile Speed Multiplier", () => v.MaximumProjectileSpeedMultiplier);
                s.Add(nameof(v.DistanceFalloff), "Distance Falloff", () => v.DistanceFalloff);
                s.Add(nameof(v.MinimumProjectileSpeedMultiplier), "Minimum Projectile Speed Multiplier", () => v.MinimumProjectileSpeedMultiplier);
                return;
            }
            case BladeShipMetaContract v:
            {
                s.Add(nameof(v.ProjectileDestructionPolicy), "Projectile Destruction Policy", () => v.ProjectileDestructionPolicy);
                s.Add(nameof(v.RecordingDuration), "Recording Duration", () => v.RecordingDuration);
                s.Add(nameof(v.SampleInterval), "Sample Interval", () => v.SampleInterval);
                s.Add(nameof(v.Width), "Width", () => v.Width);
                s.Add(nameof(v.Speed), "Speed", () => v.Speed);
                s.Add(nameof(v.Damage), "Damage", () => v.Damage);
                s.Add(nameof(v.DamageType), "Damage Type", () => v.DamageType);
                s.Add(nameof(v.Lifetime), "Lifetime", () => v.Lifetime);
                s.Add(nameof(v.PassiveSpeedDivisor), "Passive Speed Divisor", () => v.PassiveSpeedDivisor);
                s.Add(nameof(v.PassiveSpeedMultiplier), "Passive Speed Multiplier", () => v.PassiveSpeedMultiplier);
                s.Add(nameof(v.MaximumReloadMultiplier), "Maximum Reload Multiplier", () => v.MaximumReloadMultiplier);
                s.Add(nameof(v.MinimumReloadMultiplier), "Minimum Reload Multiplier", () => v.MinimumReloadMultiplier);
                return;
            }
            case HeavyShieldShipMetaContract v:
            {
                s.Add(nameof(v.ShieldHealthMultiplier), "Shield Health Multiplier", () => v.ShieldHealthMultiplier);
                s.Add(nameof(v.ExtraHealthAmount), "Extra Health Amount", () => v.ExtraHealthAmount);
                return;
            }
            case ArkanoidShipMetaContract v:
            {
                s.Add(nameof(v.ProjectileDestructionPolicy), "Projectile Destruction Policy", () => v.ProjectileDestructionPolicy);
                s.Add(nameof(v.PaddleFollowSpeed), "Paddle Follow Speed", () => v.PaddleFollowSpeed);
                s.Add(nameof(v.PaddleAttractionRadius), "Paddle Attraction Radius", () => v.PaddleAttractionRadius);
                s.Add(nameof(v.PaddleAttractionForce), "Paddle Attraction Force", () => v.PaddleAttractionForce);
                s.Add(nameof(v.BallSpeed), "Ball Speed", () => v.BallSpeed);
                s.Add(nameof(v.PaddleBounceSpeedMultiplier), "Paddle Bounce Speed Multiplier", () => v.PaddleBounceSpeedMultiplier);
                s.Add(nameof(v.PaddleBounceRecoveryDuration), "Paddle Bounce Recovery Duration", () => v.PaddleBounceRecoveryDuration);
                s.Add(nameof(v.BallDamage), "Ball Damage", () => v.BallDamage);
                s.Add(nameof(v.BallDamageType), "Ball Damage Type", () => v.BallDamageType);
                s.Add(nameof(v.StasisDamageType), "Stasis Damage Type", () => v.StasisDamageType);
                s.Add(nameof(v.StasisDuration), "Stasis Duration", () => v.StasisDuration);
                s.Add(nameof(v.StasisRadiusMultiplier), "Stasis Radius Multiplier", () => v.StasisRadiusMultiplier);
                s.Add(nameof(v.StasisDamagePerSecond), "Stasis Damage Per Second", () => v.StasisDamagePerSecond);
                s.Add(nameof(v.StasisTickInterval), "Stasis Tick Interval", () => v.StasisTickInterval);
                return;
            }
            case DictatorTurretSpawnGroup v:
            {
                s.Add("LocalPosition.x", "Local Position X", () => v.LocalPosition.x);
                s.Add("LocalPosition.y", "Local Position Y", () => v.LocalPosition.y);
                s.Add(nameof(v.Count), "Count", () => v.Count);
                s.Add(nameof(v.HorizontalSpacing), "Horizontal Spacing", () => v.HorizontalSpacing);
                return;
            }
            case DictatorShipMetaContract v:
            {
                s.Add(nameof(v.TurretLifetime), "Turret Lifetime", () => v.TurretLifetime);
                s.Add(nameof(v.TurretDamageType), "Turret Damage Type", () => v.TurretDamageType);
                s.Add(nameof(v.TurretProjectileDamage), "Turret Projectile Damage", () => v.TurretProjectileDamage);
                s.Add(nameof(v.TurretReloadTime), "Turret Reload Time", () => v.TurretReloadTime);
                s.Add(nameof(v.TurretProjectileSpeed), "Turret Projectile Speed", () => v.TurretProjectileSpeed);
                s.Add(nameof(v.TurretTargetingRange), "Turret Targeting Range", () => v.TurretTargetingRange);
                s.Add(nameof(v.KineticDamageBonusPercent), "Kinetic Damage Bonus Percent", () => v.KineticDamageBonusPercent);
                s.Add(nameof(v.ExplosionDamageBonusPercent), "Explosion Damage Bonus Percent", () => v.ExplosionDamageBonusPercent);
                if (v.TurretSpawnGroups != null) foreach (var g in v.TurretSpawnGroups) if (g != null) s.Nested(g, g.StatSourceId, "Turret Group " + g.StatSourceId);
                return;
            }
            case PrismShipMetaContract v:
            {
                s.Add(nameof(v.SpawnOffset), "Spawn Offset", () => v.SpawnOffset);
                s.Add(nameof(v.WaveWidth), "Wave Width", () => v.WaveWidth);
                s.Add(nameof(v.WaveThickness), "Wave Thickness", () => v.WaveThickness);
                s.Add(nameof(v.WaveSpeed), "Wave Speed", () => v.WaveSpeed);
                s.Add(nameof(v.MaxDistance), "Max Distance", () => v.MaxDistance);
                s.Add(nameof(v.Damage), "Damage", () => v.Damage);
                s.Add(nameof(v.DamageType), "Damage Type", () => v.DamageType);
                s.Add(nameof(v.DestroyProjectiles), "Destroy Projectiles", () => v.DestroyProjectiles);
                s.Add(nameof(v.ProjectileDestructionPolicy), "Projectile Destruction Policy", () => v.ProjectileDestructionPolicy);
                s.Add(nameof(v.PlasmaDamage), "Plasma Damage", () => v.PlasmaDamage);
                s.Add(nameof(v.PlasmaDamageBonusPercent), "Plasma Damage Bonus Percent", () => v.PlasmaDamageBonusPercent);
                s.Add(nameof(v.ElectricDamageBonusPercent), "Electric Damage Bonus Percent", () => v.ElectricDamageBonusPercent);
                return;
            }
            case PhantomShipMetaContract v:
            {
                s.Add(nameof(v.PhaseDuration), "Phase Duration", () => v.PhaseDuration);
                s.Add(nameof(v.PurgeRadius), "Purge Radius", () => v.PurgeRadius);
                return;
            }
            case HuskarShipMetaContract v:
            {
                s.Add(nameof(v.HealFromDamagePercent), "Heal From Damage Percent", () => v.HealFromDamagePercent);
                s.Add(nameof(v.HealDuration), "Heal Duration", () => v.HealDuration);
                s.Add(nameof(v.MissingHealthFireRateBonus), "Missing Health Fire Rate Bonus", () => v.MissingHealthFireRateBonus);
                return;
            }
            case OrbitShipMetaContract v:
            {
                s.Add(nameof(v.ProjectileDestructionPolicy), "Projectile Destruction Policy", () => v.ProjectileDestructionPolicy);
                s.Add(nameof(v.ObjectCount), "Object Count", () => v.ObjectCount);
                s.Add(nameof(v.ObjectRadius), "Object Radius", () => v.ObjectRadius);
                s.Add(nameof(v.MaximumChargedObjectRadius), "Maximum Charged Object Radius", () => v.MaximumChargedObjectRadius);
                s.Add(nameof(v.BaseOrbitRadius), "Base Orbit Radius", () => v.BaseOrbitRadius);
                s.Add(nameof(v.MaximumOrbitRadius), "Maximum Orbit Radius", () => v.MaximumOrbitRadius);
                s.Add(nameof(v.BaseAngularSpeed), "Base Angular Speed", () => v.BaseAngularSpeed);
                s.Add(nameof(v.MaximumAngularSpeed), "Maximum Angular Speed", () => v.MaximumAngularSpeed);
                s.Add(nameof(v.Clockwise), "Clockwise", () => v.Clockwise);
                s.Add(nameof(v.ChargeUpDuration), "Charge Up Duration", () => v.ChargeUpDuration);
                s.Add(nameof(v.CollapseSpeed), "Collapse Speed", () => v.CollapseSpeed);
                s.Add(nameof(v.ContactDamage), "Contact Damage", () => v.ContactDamage);
                s.Add(nameof(v.DamageType), "Damage Type", () => v.DamageType);
                s.Add(nameof(v.DamageInterval), "Damage Interval", () => v.DamageInterval);
                s.Add(nameof(v.CooldownReductionPerProjectile), "Cooldown Reduction Per Projectile", () => v.CooldownReductionPerProjectile);
                s.Add(nameof(v.MaximumObjectRadius), "Maximum Object Radius", () => v.MaximumObjectRadius);
                s.Add(nameof(v.IsObjectRadiusLimited), "Is Object Radius Limited", () => v.IsObjectRadiusLimited);
                return;
            }
            case ProjectileDamageSource v:
            {
                s.Add(nameof(v.Id), "Id", () => v.Id);
                s.Add(nameof(v.Trigger), "Trigger", () => v.Trigger);
                s.Add(nameof(v.Damage), "Damage", () => v.Damage);
                s.Add(nameof(v.DamageType), "Damage Type", () => v.DamageType);
                s.Add(nameof(v.BypassesShield), "Bypasses Shield", () => v.BypassesShield);
                return;
            }
            case ProjectileFlightContract v:
            {
                s.Add(nameof(v.GrowDuringFlight), "Grow During Flight", () => v.GrowDuringFlight);
                s.Add("ScaleGrowthPerSecond.x", "Scale Growth Per Second X", () => v.ScaleGrowthPerSecond.x);
                s.Add("ScaleGrowthPerSecond.y", "Scale Growth Per Second Y", () => v.ScaleGrowthPerSecond.y);
                return;
            }
            case ProjectileResonanceSphereContract v:
            {
                s.Add(nameof(v.MaximumStoredDamage), "Maximum Stored Damage", () => v.MaximumStoredDamage);
                s.Add(nameof(v.Lifetime), "Lifetime", () => v.Lifetime);
                s.Add(nameof(v.ExplosionRadius), "Explosion Radius", () => v.ExplosionRadius);
                s.Add(nameof(v.WaveSpeed), "Wave Speed", () => v.WaveSpeed);
                s.Add(nameof(v.SlowdownStartY), "Slowdown Start Y", () => v.SlowdownStartY);
                s.Add(nameof(v.DetonationY), "Detonation Y", () => v.DetonationY);
                s.Add(nameof(v.SlowdownDuration), "Slowdown Duration", () => v.SlowdownDuration);
                s.Add(nameof(v.FullChargeDetonationDelay), "Full Charge Detonation Delay", () => v.FullChargeDetonationDelay);
                return;
            }
            case ProjectileHomingContract v:
            {
                s.Add(nameof(v.RotationSpeed), "Rotation Speed", () => v.RotationSpeed);
                return;
            }
            case ProjectileTargetingContract v:
            {
                s.Add(nameof(v.MaxTargets), "Max Targets", () => v.MaxTargets);
                s.Add(nameof(v.SearchRadius), "Search Radius", () => v.SearchRadius);
                return;
            }
            case ProjectileContactContract v:
            {
                s.Add(nameof(v.ContactMode), "Contact Mode", () => v.ContactMode);
                return;
            }
            case ProjectileContinuousDamageContract v:
            {
                s.Add(nameof(v.DamageTickInterval), "Damage Tick Interval", () => v.DamageTickInterval);
                return;
            }
            case ProjectileMovementSlowContract v:
            {
                s.Add(nameof(v.SlowPercentPerHit), "Slow Percent Per Hit", () => v.SlowPercentPerHit);
                s.Add(nameof(v.MaximumSlowPercent), "Maximum Slow Percent", () => v.MaximumSlowPercent);
                return;
            }
            case ProjectileEnemyDebuffsContract v:
            {
                if (v.Debuffs != null) foreach (var g in v.Debuffs) if (g != null) s.Nested(g, g.StatSourceId, "Debuff Application " + g.StatSourceId);
                return;
            }
            case ProjectileCircularChainContract v:
            {
                s.Add(nameof(v.HitsPerTarget), "Hits Per Target", () => v.HitsPerTarget);
                s.Add(nameof(v.DamagePerHit), "Damage Per Hit", () => v.DamagePerHit);
                s.Add(nameof(v.HitInterval), "Hit Interval", () => v.HitInterval);
                s.Add(nameof(v.MaximumTargets), "Maximum Targets", () => v.MaximumTargets);
                s.Add(nameof(v.SearchConeAngle), "Search Cone Angle", () => v.SearchConeAngle);
                s.Add(nameof(v.SearchRange), "Search Range", () => v.SearchRange);
                s.Add(nameof(v.RandomEscapeAngle), "Random Escape Angle", () => v.RandomEscapeAngle);
                return;
            }
            case ProjectileArcNodesContract v:
            {
                s.Add(nameof(v.DamagePerArc), "Damage Per Arc", () => v.DamagePerArc);
                s.Add(nameof(v.ConnectionRange), "Connection Range", () => v.ConnectionRange);
                s.Add(nameof(v.PulseInterval), "Pulse Interval", () => v.PulseInterval);
                s.Add(nameof(v.MaximumConnections), "Maximum Connections", () => v.MaximumConnections);
                s.Add(nameof(v.ArcHitRadius), "Arc Hit Radius", () => v.ArcHitRadius);
                s.Add(nameof(v.VisualDuration), "Visual Duration", () => v.VisualDuration);
                s.Add(nameof(v.VisualWidth), "Visual Width", () => v.VisualWidth);
                s.Add(nameof(v.VisualSegments), "Visual Segments", () => v.VisualSegments);
                s.Add(nameof(v.VisualJitter), "Visual Jitter", () => v.VisualJitter);
                return;
            }
            case ProjectileLifetimeContract v:
            {
                s.Add(nameof(v.Lifetime), "Lifetime", () => v.Lifetime);
                s.Add(nameof(v.DisableColliderAfterFirstPhysicsStep), "Disable Collider After First Physics Step", () => v.DisableColliderAfterFirstPhysicsStep);
                s.Add(nameof(v.FadeBeforeDespawn), "Fade Before Despawn", () => v.FadeBeforeDespawn);
                s.Add(nameof(v.FadeDuration), "Fade Duration", () => v.FadeDuration);
                return;
            }
            case ProjectileExplosionContract v:
            {
                s.Add(nameof(v.ExplosionRadius), "Explosion Radius", () => v.ExplosionRadius);
                s.Add(nameof(v.ExplodeAtMaximumRange), "Explode At Maximum Range", () => v.ExplodeAtMaximumRange);
                return;
            }
            case SpawnSecondaryProjectileContract v:
            {
                s.Add(nameof(v.SpawnTrigger), "Spawn Trigger", () => v.SpawnTrigger);
                s.Add(nameof(v.TravelDistance), "Travel Distance", () => v.TravelDistance);
                s.Add(nameof(v.IgnoreTriggeringEnemy), "Ignore Triggering Enemy", () => v.IgnoreTriggeringEnemy);
                return;
            }
            case ProjectileDamageSourcesContract v:
            {
                if (v.Sources != null) foreach (var g in v.Sources) if (g != null) s.Nested(g, g.Id, "Damage Source " + g.Id);
                return;
            }
            case ProjectileBeamContract v:
            {
                s.Add(nameof(v.Width), "Width", () => v.Width);
                s.Add(nameof(v.DamageTickInterval), "Damage Tick Interval", () => v.DamageTickInterval);
                return;
            }
            case ProjectileContactAreaContract v:
            {
                return;
            }
            case ProjectileData v:
            {
                s.Add(nameof(v.DeliveryType), "Delivery Type", () => v.DeliveryType);
                s.Add(nameof(v.SimulationBackend), "Simulation Backend", () => v.SimulationBackend);
                s.Add(nameof(v.UsesEntities), "Uses Entities", () => v.UsesEntities);
                s.Add(nameof(v.Damage), "Damage", () => v.Damage);
                s.Add(nameof(v.DamageType), "Damage Type", () => v.DamageType);
                s.Add(nameof(v.Range), "Range", () => v.Range);
                s.Add(nameof(v.Speed), "Speed", () => v.Speed);
                return;
            }
            case EnemyDebuffApplication v:
            {
                s.Add(nameof(v.AmountPerHit), "Amount Per Hit", () => v.AmountPerHit);
                s.Add(nameof(v.IsValid), "Is Valid", () => v.IsValid);
                if (v.Debuff != null) s.Nested(v.Debuff, "config", v.Debuff.name, v.Debuff);
                return;
            }
            case EnemyHeatDebuffConfig v:
            {
                s.Add(nameof(v.DamageShieldFirst), "Damage Shield First", () => v.DamageShieldFirst);
                s.Add(nameof(v.CoolingDelay), "Cooling Delay", () => v.CoolingDelay);
                s.Add(nameof(v.CoolingPercentPerSecond), "Cooling Percent Per Second", () => v.CoolingPercentPerSecond);
                s.Add(nameof(v.ExplosionRadius), "Explosion Radius", () => v.ExplosionRadius);
                s.Add(nameof(v.ExplosionDamagePercent), "Explosion Damage Percent", () => v.ExplosionDamagePercent);
                s.Add(nameof(v.TransferredHeatPercent), "Transferred Heat Percent", () => v.TransferredHeatPercent);
                s.Add(nameof(v.BurningDuration), "Burning Duration", () => v.BurningDuration);
                s.Add(nameof(v.BurningDamagePerTick), "Burning Damage Per Tick", () => v.BurningDamagePerTick);
                s.Add(nameof(v.BurningDamageInterval), "Burning Damage Interval", () => v.BurningDamageInterval);
                s.Add(nameof(v.ThresholdMode), "ThresholdMode", () => v.ThresholdMode);
                s.Add(nameof(v.ThresholdAction), "ThresholdAction", () => v.ThresholdAction);
                s.Add(nameof(v.TriggerOncePerEnemy), "TriggerOncePerEnemy", () => v.TriggerOncePerEnemy);
                return;
            }
            default: s.Warn("No stat declaration for " + value.GetType().FullName); return;
        }
    }
}
