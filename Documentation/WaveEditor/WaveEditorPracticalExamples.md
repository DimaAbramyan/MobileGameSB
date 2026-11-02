# Wave Editor: практическое создание Bullet Hell

Срез реализации: 24.10.2026. [Руководство](WaveEditorCompleteGuide.md) · [матрица поддержки](WaveEditorFeatureMatrix.md) · [файлы и точные поля](WaveEditorScriptsReference.md).

Числа в рецептах — **примеры новых авторских настроек**, не существующий баланс игры и не изменения готовых волн. Для опытов создайте собственный prefab/копию штатным Unity workflow. В этом исследовании такие копии не создавались. Названия полей соответствуют коду; Inspector может отображать nicified label (например, `shotGunMode` как Stack).

## 1. Базовая безопасная подготовка

1. Создайте Wave conductor; заполните `scheduledSubWaves`, а не одновременно два конкурирующих списка расписания.
2. Создайте DirectedEnemySubWave с FormationBehaviour. Назначьте подходящий Enemy prefab с `SimpleEnemyAttackExecutor`/совместимым `IWaveAttackExecutor` и корректными projectile prefab/authoring.
3. Выберите Formation HorizontalLine, count 5, center (0,2.5), spacing (0.8,0.8), spawnInterval 0.1. Проверьте slot IDs 0…4.
4. Для первого опыта не добавляйте входную петлю, Post Infinite, Multiple или Coordinator. Добавьте AttackBehaviour, Movement None, Fire Aimed, EnemySettings либо явный WaveOverride.
5. Включите Show Camera Bounds и Show All Enemy Silhouettes на Wave. Откройте Tools/Waves/Wave Preview Player; поставьте Duration 20, target (0,-3,0), Runtime Seed.

Результат: контролируемый пример, в котором ошибка ссылки prefab не маскируется сложным расписанием. Источники: [Wave](../../Assets/Scripts/Gameplay/Waves/Wave.cs), [configs](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveBehaviourConfigs.cs), [executor contract](../../Assets/Scripts/Gameplay/Enemies/IWaveAttackExecutor.cs).

## 2. Прицельный веер с фиксированным захватом

**Цель:** враги видят позицию игрока в начале очереди и выпускают читаемый веер; последующее движение игрока не поворачивает уже захваченную очередь.

1. Attack Fire Mode Aimed, Aim Mode AimOnBurst, Movement None.
2. В Wave Attack Pattern выберите Single, Burst Enabled, attackShotCount 2, burstShotCount 3, burstShotInterval 0.12, attackShotInterval 0.6, attackCooldown 1.2.
3. Emission Origin Enemy/Enemy, offsets 0; Layout Point.
4. Fan ON, count 7, angles -30…30. Stack OFF, Spread Fixed. Default projectile, override speed 3, lifetime 5, Destroy Outside Screen ON/Automatic.
5. В Preview двигайте цель во время очереди, затем между очередями. Должен обновляться новый capture в начале Burst, а не каждая пуля.

Один внутренний выстрел даёт 7 элементов; один внешний Burst — 3×7=21, без учёта нескольких врагов. AimOnBurst не тождественен непрерывному наведению; если Burst выключен, ожидаемое число обновлений будет другим. Источники: [AimState](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackModels.cs), [Schedule](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackPatternSchedule.cs), [Fan builder](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackProjectileBuilder.cs).

## 3. Последовательный сектор-маятник

**Цель:** «гребёнка», которая проходит слева направо, затем справа налево.

1. Stationary → Angle=180° для общего направления вниз. Для Aimed базовая середина сектора будет привязана к цели, поэтому результаты отличаются.
2. Layout Sector, StartAngle=-60, EndAngle=60, DirectionsCount=13, LaunchMode Sequential.
3. StartDelay=0.2, LaunchInterval=0.04, LaunchOrder=PingPong, InitialDirection=Forward, ResetOrderOnAttackStart=ON.
4. Задайте несколько внешних выстрелов одного pattern (например 4) с промежутком больше полного запуска: 0.2+12×0.04=0.68s. Fan/Stack сначала OFF.
5. Включите Fan 3 с узким ±4° только после проверки порядка. Каждое направление теперь выпускает 3 элемента одновременно, но PingPong меняется после всего сектора.

Для продолжения порядка между разными атаками отключите Reset; прерывание незавершённого сектора не должно считаться его завершением. Для атак во время входа проверьте WaitUntilSectorReady: ON может отложить запуск до готовности формации. Источники: [Layout](../../Assets/Scripts/Gameplay/Enemies/EnemyEmissionLayoutSettings.cs), [Sector state](../../Assets/Scripts/Gameplay/Enemies/EnemySectorLaunchOrderState.cs).

## 4. Кольцо эмиттеров + вращающийся веер

**Цель:** четыре движущиеся точки вокруг врага, каждая выпускает собственный узкий веер.

1. Layout Circle: emitterCount=4, radius=0.7, centerOffset=(0,0,0), startAngle=0.
2. Fire Stationary Angle=180, Fan 5, -12…12, Stack OFF. Это 20 элементов за один залп до ограничителей.
3. Layout Rotation ON: Continuous, CounterClockwise, 30°/s, Linear progress.
4. Movement None; burst/shot interval 0.3, lifetime 4, base speed 2.5.
5. Scene shooting preview должен показать положения эмиттеров и все доступные лучи From/To, плюс область оборота. В анимированном Preview оцените текущие выпуски, а не только статическое покрытие.

Вариант: заменить Continuous на PingPongByAngle с From=-35, Angle=35. Первоначальный переход от 0 к From входит в движение. Circle radius не наследует неравномерный ship scale как эллипс; для эллиптической формации врагов есть отдельный shapeFlattening, это другая система. Источники: [GetPosition](../../Assets/Scripts/Gameplay/Enemies/EnemyEmissionLayoutSettings.cs), [Rotation.EvaluateAngle](../../Assets/Scripts/Gameplay/Enemies/EnemyRotationSettings.cs), [rotation guides](../../Assets/Editor/EnemyShootingPreview.Rotation.cs).

## 5. DNA-Like поток с дополнительным независимым паттерном

**Цель:** переплетающийся поток и редкие радиальные залпы от того же врага.

1. В `EnemyBurstAttackSettings` переключите Attack Patterns в Multiple; не назначайте один formation slot сразу двум Wave-группам.
2. Pattern 1: Layout Point, Fan 5 с -35…35, speed 3, MovementPattern Orbit, OrbitCenterMode DNA-Like, angularSpeed=90, DnaWeaveSpeed=1; интервал 0.15.
3. Pattern 2: Layout Radial, directionsCount=12, Fan OFF, interval/cooldown заметно длиннее (например 1.5s), speed 2.
4. Facing Fixed/Default для неподвижного спрайта; либо FollowPattern с выбором конкретного PatternId, если спрайт должен следовать одному из потоков.
5. Для случайной скорости DNA выберите seeded random и PerShot или PerBurst, чтобы согласованные fan-элементы делили sample. PerProjectile допустим как иной рисунок, но синхронное пересечение больше не следует считать сохранённым.

Путь настройки DNA находится в Projectile Settings → Movement Pattern Orbit → Orbit Center DNA-Like, а не в списке Fire Mode. Источники: [ShootingSettings/DNA](../../Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.cs), [DNA motion](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsComponents.cs), [builder](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackProjectileBuilder.cs).

## 6. Родитель за экраном → вход → боковое расщепление

**Цель:** снаряд появляется над экраном, входит в поле, затем на выбранной боковой линии создаёт веер.

1. Emission Origin X=Player с OnAttackStart; Y=World→Boundary→Top, Offset=-0.5. Layout Point.
2. Направление выберите Stationary Angle либо WorldPoint так, чтобы траектория сначала вошла в viewport и затем пересекла Left или Right. Одна строго вертикальная линия к боковой границе не придёт.
3. Projectile lifetime задайте с запасом на путь и задержку; DestroyOutsideScreen ON/Automatic.
4. Child ON: TriggerMode OnEvent, EventType OnScreenBoundaryReached, flags Left+Right, Top/Bottom OFF, BoundaryOffset=0.1, TriggerOnce=true, TimeOffset=0.15.
5. Child Origin CaptureOnStart создаст веер в точке пересечения; FollowParent — из сместившегося за 0.15s родителя. SpawnOffset local (0,0.1) сдвигает вперёд по движению.
6. Child Pattern Stationary/Default по желаемому направлению, Fan 9, -70…70, repeatCount=1. DestroyParent ON для удаления после полного дочернего залпа.

Пересечение игнорируемой Top не запускает событие и не лишает возможности позднего crossing Right. ScreenHold защищает этот сценарий от преждевременной screen cleanup, но короткий lifetime всё ещё завершит снаряд. Не ожидайте событие просто от появления за верхней линией. Источники: [Origin](../../Assets/Scripts/Gameplay/Enemies/EnemyEmissionOriginSettings.cs), [boundary](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackBoundary.cs), [trigger runner](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackBoundaryTrigger.cs).

## 7. Двухуровневый цветок Child Attack

**Цель:** летящий родитель через время распадается на радиальные лепестки, каждый позже выпускает короткий веер.

1. Root Point, speed 2, lifetime не менее 4s. Child AfterTime=1s, CaptureOnStart, DestroyParent ON.
2. Child level 1 Layout Radial 8, speed 1.5, RepeatCount=1.
3. В Projectile Settings первого ребёнка Child ON, AfterDistance=0.8, Fan 3 ±15°, speed 2, DestroyParent ON.
4. Все размеры/цвета настраивайте в соответствующей вложенной Projectile Configuration, а не в общем Wave-level Spawn.
5. Проверьте глубину 2 и бюджет: для одного корня это 8 детей + 24 внука, до дополнительного Fan/Stack/Burst. Умножайте также на число корневых эмиттеров/врагов.

Повторения Child идут после окончания прошлого полного исполнения; сложный Sequential Sector увеличивает это время. Фигура не требует отдельного кода генерации, но при массовом размножении вступают в силу 2048 descendants/root и 512 step spawns. Источники: [Child settings](../../Assets/Scripts/Gameplay/Enemies/EnemyChildAttackSettings.cs), [runner](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackSystem.cs).

## 8. Полигональный Composite с вращением и Homing

**Цель:** фигура из небольших снарядов летит как целое, мягко корректируя направление.

1. Projectile Type Composite, Shape RegularPolygon, sides=5, projectilesPerSide=4: ожидается 5×(4−1)=15 компонентов плюс центр.
2. Radius Automatic, spacingGap=0.03, componentScale=0.7. Либо Custom Radius=0.7 для явно заданной формы; Automatic не показывает неиспользуемый radius.
3. Composite Rotation AlignToMovement; InitialRotation можно случайно выбирать -20…20 PerProjectile. Для самостоятельного вращения выберите Continuous и angularSpeed, но тогда это другой эффект.
4. Явный component prefab должен иметь поддерживаемый Homing authoring и валидный visual/radius; BurstAtPoint не подходит.
5. Layout Point для одного центра или Sector 3 для трёх фигур. Учитывайте 3×16=48 entities вместо трёх обычных пуль.
6. Child AfterTime/AfterDistance/Expired можно назначить центру, чтобы он выпустил вторую фигуру. Child HitPlayer не выбирайте: центр без hitbox.

Урон настраивается в Composite Components Damage/DamageType. Свободные промежутки между компонентами — не сплошной центральный круг; это важно для bullet-hell проходов. Источники: [CompositeSpec](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyCompositeProjectileSettings.cs), [spawn checks](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSpawner.Composite.cs), [systems](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyCompositeProjectileSystems.cs).

## 9. Сегментированное вращающееся кольцо

1. Composite Shape SegmentedRing, segments=4, projectilesPerSegment=5, gapAngle=25° (<90°), radius Custom=1.
2. Rotation PingPong, minAngle=-45°, maxAngle=45°, initialRotation=0, angularSpeed=30.
3. Центр направьте вниз медленно (например speed=1), lifetime=8; DestroyOutsideAutomatic удалит целую фигуру после выхода envelope.
4. Снизьте плотность верхнего Layout: Radial 12 таких колец потребует 12×(20+1)=252 entities за один залп.
5. В Scene View проверьте геометрию каждого сегмента и реальные промежутки, затем анимированный Preview для движения отверстий.

Не ставьте gap ≥ 360/segmentCount: код ограничит фактический зазор, оставляя место для компонентов. При большом количестве perSegment максимум 256 распределяется равномерно, поэтому фигура сохранится целой, но плотность ограничится. Источник: [CompositeProjectileSpec.ComponentCount/EffectiveGapAngle/Offset](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyCompositeProjectileSettings.cs).

## 10. Telegraph → три лазера с зафиксированным направлением

1. Fire Aimed, AimOnStart; Layout Point + Fan 3 ±15°. Projectile Type Laser.
2. Laser Geometry ToScreenEdge, lengthOffset=0, width=0.12; ConstantWidth ON.
3. ActivationDelay=0.2, Telegraph ON, TelegraphDuration=0.8, Opacity=0.3, fadeIn/out=0.1.
4. LaserDuration=1.5, laser fadeOut=0.2. DamageMode Periodic с interval=0.5 для повторного контактного урона.
5. FollowOrigin ON, если начало должно двигаться вместе с источником; направление выпущенного луча при этом остаётся locked. SurviveSourceDestruction выбирайте сознательно.
6. Override Telegraph Geometry OFF для совпадения предупреждения с геометрией луча; ON нужен только при намеренно иной визуальной подсказке.

Полный жизненный интервал здесь 0.2+0.8+1.5+0.2=2.7s; последние 0.2s не наносят урон. Слишком короткий Attack Cooldown создаст несколько одновременно живых лучей, а не автоматически продлит один. Preview покажет фазы, но не проверит настоящий player damage. Источники: [settings](../../Assets/Scripts/Gameplay/Enemies/EnemyLaserSettings.cs), [runtime](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyLaserRuntime.cs).

## 11. Две группы с перекрытием и Sequence внутри группы

**Цель:** A делает серию вееров, B — кольца, между окнами управляемая пауза/overlap.

1. В Wave Schedule добавьте две Directed-подволны на разные scheduleIndex, с необходимыми задержками входа.
2. У A Attack Behaviour Mode Sequence: RepeatBlock 3×Attack(Aimed Fan), Pause(0.6), Attack(Radial). Sequence Once, чтобы один проход имел ясный цикл.
3. У B простой Pattern Sector либо Composite; Post может продолжать движение независимо от стрельбы.
4. Добавьте Coordinator на Wave через нижнюю панель Player. Group A ссылается на schedule slot A; Group B — на B. WaitUntilReady ON, maxWaitDuration достаточный для входа.
5. Для понятного повтора выберите AttackCycles=1 в обеих, DelayAfterGroup=0.5, GlobalCooldown=0, loopSequence=true. Для короткого одновременного drain задайте overlapDuration, учитывая различие Timed и cycle modes.
6. SwitchMode FinishCurrentAttack сохраняет завершение уже начатых атак; Restart reactivation начинает следующий проход заново. Resume используйте, когда действительно хотите сохранить незавершённый progression.
7. Previous/Next Event проверяют A → пустой набор → B и возврат; при overlap возможны A → A+B → B. Внутренний шаг Sequence не создаёт отдельный Coordinator marker, пока active groups те же.

Не задавайте «Loop Count=1» в надежде остановить Coordinator с loopSequence=true: это флаг бесконечного повтора. Не ожидайте от Preview проверки гибели части группы. Источники: [Sequence](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackSequence.cs), [CoordinatorPlayer](../../Assets/Scripts/Gameplay/Waves/WaveAttackCoordinatorPlayer.cs), [navigation](../../Assets/Editor/WavePreviewPlayerCoordinatorNavigation.cs).

## 12. Движущаяся матрица + Post Parallel + Attack Order

1. Создайте Grid/CustomPoints через матрицу 7×3 с несколькими пустыми клетками. Apply Matrix To Points фиксирует занятые позиции.
2. Phase Entry Checkpoints: сверху в центр через Bezier Auto; settle в формацию.
3. Post pipeline: Parallel Background с Wobble, затем LocalMovement к новому центру, затем FormationReorder Mirror с batches=2, затем Patrol CompleteRoute.
4. Attack Order включите CenterToOutside, EnemiesPerBatch=2, interval=0.15, orderSource=FormationSlotOrder; wave movement не должен менять slot IDs.
5. Для меняющегося пространственного порядка вместо этого выберите SpatialOrder и resetOrderEachCycle=ON: следующий цикл строится по новому снимку, не каждый кадр.
6. Включите All Silhouettes, проверьте route и post preview. В Player сравните кадры до/после перестроения и сделайте обратный seek.

Background Wobble добавляет движение, а не запускает второй независимый Wave runtime. Infinite blocking command перед Reorder может не дать до него дойти. Источники: [simulation](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveSimulation.cs), [handlers](../../Assets/Scripts/Gameplay/Waves/DirectedWavePostCommandHandlers.cs), [OrderPlan](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackOrder.cs).

## 13. Повторно используемые Projectile и Attack Pattern Presets

1. Настройте обычный projectile с override speed, lifetime, Normal/Fragile, screen cleanup и Child. Сохраните **Projectile Preset**.
2. Отдельно сохраните **Attack Pattern Preset** с Emission Layout/Fan/Rotation/timing. Не ожидайте, что он заменит самостоятельный тип снаряда так же, как Projectile Preset.
3. В другом потоке назначьте оба. Linked получает будущие изменения assets; Copy оставляет локальные значения.
4. Для особого врага отметьте override только нужного пути (например projectile speed или color), сохранив ссылку на остальную конфигурацию.
5. В библиотеке Repeat Same Seed повторяет генерацию, New Seed меняет тестовый seed. В Wave Preview вернитесь Runtime Seed для сравнения authored numeric streams.
6. При Export Resolved Settings As Preset явно решите, хотите ли сохранить уже разрешённый Wave-level speed override. Это отличается от редактирования исходного linked asset.

Циклические ссылки через Child/Preset запрещены resolver; не пытайтесь сделать бесконечную генерацию ссылкой asset на себя. Используйте конечные Repeat/Loop в разрешённом контуре и общие лимиты. Источники: [PresetFields](../../Assets/Scripts/Gameplay/Enemies/EnemyPresetFields.cs), [resolve](../../Assets/Scripts/Gameplay/Enemies/EnemyBurstAttackSettings.Presets.cs), [library](../../Assets/Editor/EnemyPresetLibraryWindow.cs).

## 14. Фазовый босс отдельным encounter

1. Создайте BossFight prefab с BossController prefab reference, spawnPoint/offset и включённым destroyEncounterOnDefeat. Поместите его как encounter в LevelConfig.waves, не как Directed cue внутри Wave.
2. На BossController задайте phases: первая next threshold 70%, вторая 35%, третья последняя. Threshold последней не управляет дальнейшим переходом.
3. Создайте Boss/Attacks/Radial Attack: Circle 18 элементов, speed 3, lifetime 6. Второй asset — Arc 9, arcDegrees 90, TowardPlayer. Третий — Spiral 12, volleyCount 6, rotationPerVolleyDegrees 12.
4. Назначьте arrays phases: Circle → Arc → Spiral либо несколько assets с Sequential/Random. Паузы задаются фазой и delayBetweenVolleys pattern, это не Directed Attack Order.
5. Назначьте BossProjectile prefab и корректный BossProjectilePool через боевую DI сцену. Проверьте геометрию собственным Attack Preview в Inspector asset.

Это пример уже имеющегося отдельного инструментария. Wave Preview Player/Fast Seek для BossFight не заявлены; health-triggered transitions требуют настоящего боя. Настройки Child/Composite обычного ECS projectile к BossProjectile не применяются автоматически. Источники: [BossFight](../../Assets/Scripts/Gameplay/Bosses/BossFight.cs), [BossController](../../Assets/Scripts/Gameplay/Bosses/BossController.cs), [BossRadialAttackPattern](../../Assets/Scripts/Gameplay/Bosses/BossRadialAttackPattern.cs).

## Контроль перед последующим реальным боем

- Проверены ссылки prefab, collision radius, source scopes, slot IDs, schedule indices.
- Визуальные From/To не перепутаны с одновременными активными выстрелами.
- Не превышены бюджеты; нет сообщений omitted/unsupported/missing projectile.
- Lifetime учитывает ожидание Child и путь из-за экрана.
- Sequence/Coordinator имеют намеренные infinity/timeout/overlap, а не случайную блокировку.
- Сопоставлены общий Wave fireRate, enemy prefab cadence и возможный Level difficulty multiplier.
- Сохранены только авторские prefab/preset настройки; цели и cache Preview не принимаются за прогрессию игрока.
- Пикирование, реальные смерти/контактный урон/дропы проверяются отдельно в бою, потому что Player их не доказывает.

Имена и условия полей: [полное руководство](WaveEditorCompleteGuide.md). Точные исходники/enum/defaults: [Scripts Reference](WaveEditorScriptsReference.md). Поддержка и запреты комбинаций: [Feature Matrix](WaveEditorFeatureMatrix.md).
