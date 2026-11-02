using UnityEditor;
using UnityEngine;

public static class EnemyAttackInspectorHints
{
    public static GUIContent FireMode(SerializedProperty property)
    {
        string description = ((DirectedWaveAttackFireMode)property.intValue) switch
        {
            DirectedWaveAttackFireMode.Aimed => "Aimed: направление на игрока. У Sector на цель направлена середина сектора. Aim Mode определяет, когда направление обновляется; Layout Rotation и Fan применяются после наведения.",
            DirectedWaveAttackFireMode.Stationary => "Stationary: базовое направление по мировому углу или в сторону неподвижной точки мира.",
            DirectedWaveAttackFireMode.None => "None: управляемая волной стрельба выключена.",
            DirectedWaveAttackFireMode.ForwardWhenPlayerAhead => "Forward When Player Ahead: выстрелы вперёд, только когда игрок находится в заданном секторе перед врагом.",
            _ => "Default: направления из Attack Pattern, без наведения на игрока."
        };
        return new GUIContent("Fire Mode", description + "\nEmission Layout определяет точки/направления; Fan и Stack применяются к каждому из них.");
    }

    public static string Field(SerializedProperty property)
    {
        return property.name switch
        {
            "shotGunMode" => "Off: один снаряд или Fan на направление. Stack: несколько совпадающих снарядов/Fan на направление; Random может развести их по углам.",
            "fanMode" => "Off: одно направление на элемент Stack. Fan: равномерный веер между Min Angle и Max Angle для каждого направления layout.",
            "fanMinAngle" => "Нижняя граница Fan в градусах относительно базового направления emitter.",
            "fanMaxAngle" => "Верхняя граница Fan в градусах. Снаряды равномерно занимают весь диапазон между границами.",
            "fanProjectileCount" => "Число снарядов в каждом Fan. Итог: Emission Count × Stack Count × Fan Count.",
            "projectileType" => "Тип создаваемого снаряда: Default, Homing, Burst At Point, Laser или Composite. Для Composite выберите Composite Shape; prefab компонентов задаётся в Projectile Settings или References & Emitter Binding.",
            "initialSpeedMode" => "Default: базовая скорость. Random: случайная скорость из диапазона. Consistent: базовая скорость с изменением после каждого залпа.",
            "randomInitialSpeedMinimum" => "Минимальная начальная скорость в режиме Random.",
            "randomInitialSpeedMaximum" => "Максимальная начальная скорость в режиме Random. DNA-Like использует одну скорость на Fan для одновременных пересечений.",
            "consistentInitialSpeedChangePerVolley" => "Прибавка к начальной скорости после каждого залпа этой атаки; отрицательная величина уменьшает скорость.",
            "projectileBaseSpeed" => "Начальная скорость снарядов этого паттерна. Random использует собственный диапазон.",
            "projectileLifetime" => "Время жизни созданного снаряда в секундах.",
            "randomMinAngle" => "Минимальное случайное угловое смещение относительно базового направления.",
            "randomMaxAngle" => "Максимальное случайное угловое смещение. Fan применяется после этого смещения.",
            "projectileMovementPattern" => "Default: прямо. Angular Turn: непрерывный поворот. Lateral: боковое движение. Orbit: вращение вокруг выбранного центра или DNA-Like переплетение.",
            "projectileSpeedBehavior" => "Default: поведение prefab. Customized: изменение скорости по мировой Y и кривой расстояния.",
            "projectileSpeedChangeY" => "Мировая координата Y, от которой применяется изменение скорости.",
            "burstAttack" => "Enabled: очередь из нескольких выстрелов; количество очередей и интервалы настраиваются ниже.",
            "attackShotCount" => "Количество событий залпа в атаке; при очередях — число очередей.",
            "attackShotInterval" => "Интервал между залпами; при очередях — между началами очередей с учётом завершения предыдущей.",
            "burstShotCount" => "Количество событий залпа внутри каждой очереди.",
            "burstShotInterval" => "Интервал между событиями залпа внутри очереди.",
            "attackCooldown" => "Перезарядка после окончания атаки, включая отложенные запуски Sector.",
            "useAttackStartDelay" => "Включить задержку перед началом этого паттерна.",
            "attackStartDelay" => "Задержка начала паттерна в секундах.",
            "layoutRotationEnabled" => "Вращение Emission Layout во времени. Не поворачивает самого врага; Enemy Facing настраивается отдельно.",
            "rotation" => "Continuous: постоянное вращение. By Angle: до заданного угла. Ping Pong By Angle: вращение туда и обратно с заданной кривой.",
            _ => property.tooltip
        };
    }
}
