# Рандомизация AttackBehaviour

Реализация расширяет существующие `RandomFloatRange`, `RandomIntRange`, `NumericRandomStream` и кнопку ⚄. Фиксированные сериализованные поля сохранены. Новые диапазоны по умолчанию выключены.

## Настройка

1. Откройте AttackBehaviour, Attack Pattern либо Projectile Preset.
2. Нажмите ⚄ справа от используемого числового поля и задайте Min/Max. Количества используют целочисленные диапазоны.
3. Если параметр допускает несколько моментов генерации, ниже появится `Randomize On`. Выключение ⚄ сохраняет диапазон и возвращает прежнее фиксированное значение.
4. Используйте существующий `Numeric Random Seed` в Projectile Settings. Для точного сравнения с Runtime установите в Preview исходный Seed Offset; кнопки Preview позволяют повторить его или выбрать новый.

Порядок Min/Max нормализуется при выборке; равные границы дают постоянное значение. Допустимые минимумы соответствуют параметру: количество не меньше 1, задержки неотрицательны, Lifetime/Final Size положительны. Нормализация при выборке не переписывает исходный пресет.

## Поддерживаемые параметры

| Область | Параметры | Момент генерации |
| --- | --- | --- |
| Fan | Count, Start/End Angle | По умолчанию Per Shot; доступны применимые Per Attack / Direction / Projectile |
| Stack | Count, Direction X/Y | Count: Per Attack / Shot / Direction, по умолчанию Per Shot |
| Расписание паттерна | Attack Shot Count, Attack Shot Interval, Attack Start Delay, Burst Shot Count, Burst Shot Interval, существующий Attack Cooldown | Количество один раз на атаку; интервал при планировании следующего события; задержка при запуске цикла; cooldown один раз на период |
| Расписание группы | Attack Start Delay, Attacks Per Enemy Per Cycle, Attacks Per Second, Delay After Attack | При запуске/построении цикла, планировании следующей атаки и после завершения атаки соответственно |
| Radial / Circle / Sector | Directions Count / Emitter Count, Start/End Angle, Radius, Center Offset X/Y, Position Offset X/Y, Sector Start Delay / Launch Interval | По умолчанию Per Shot; для радиуса/смещений доступен Per Direction |
| Spread | Min/Max Angle, в том числе существующий Random Spread | По выбранной частоте; существующий способ разброса сохранён |
| Layout Rotation | Rotation Speed, From Angle, Target Angle | Per Attack по умолчанию, также Per Shot |
| Скорость | Существующий Base Speed, Consistent Initial Speed Change Per Volley | Существующие режимы и приоритеты Base Speed сохранены; дельта Consistent — по залпам |
| Движение снаряда | Lifetime, Speed Change Y, Size Change Y/Speed, Final Size, Angular Speed, Lateral Speed, Orbit Radius / Radial Speed / Fixed Center X/Y, DNA Weave Speed | Per Projectile по умолчанию; разрешены применимые более широкие частоты |
| Child Attack | Trigger Value, Repeat Count/Interval, Spawn Offset X/Y, Boundary Offset, Time Offset | Один раз для каждого родительского снаряда, сохраняются до окончания его дочерних атак |

Поля показываются в существующих разделах только при использовании соответствующей механики. Для Child Repeat Count сохранён прежний предел 32. Защита от чрезмерного расширения Fan/Stack и дочерних атак использует существующие бюджеты генерации.

Когда включён Attack Order, его порядок/интервал сохраняют приоритет над отключёнными в Inspector полями старого расписания группы.

## Поведение и совместимость

- Геометрия залпа сохраняется для отложенного Sequential Sector. Per Direction вычисляется из идентификатора соответствующего направления. Новые залпы не меняют уже запланированный Sector.
- Значения движения записываются при создании снаряда и не генерируются заново в Update.
- PingPong сохраняет исходный ключ паттерна даже у снимков случайной геометрии. Источники и паттерны не делят состояние очередности.
- Дочерние атаки используют прежние планировщик, генератор, лимиты вложенности и завершения. Aim On Start / Burst сохраняет захваченную цель при изменении случайной геометрии.
- Single и Multiple используют общий планировщик паттерна; Sequence и координатор продолжают запускать существующие контроллеры. При восстановлении контроллера сохраняются cooldown и состояние случайного расписания.
- Linked/Copy Presets, вложенные Child Patterns и Local Overrides копируют диапазоны и частоту. Во время атаки исходный Asset не меняется: геометрия использует снимок, движение — значения в запросе создания.
- Для совпадения Runtime и Preview нужны одинаковые Seed, слот источника, паттерн, последовательность атак и условия наведения. Новые каналы не меняют существующие потоки Origin/Base Speed/Cooldown.
- Старые конфигурации продолжают использовать фиксированные значения и прежние режимы Random Speed/Spread. Существующие волны не перенастраивались.

## Примеры

Новые примеры не назначены существующим волнам:

- `Assets/Configs/AttackPresets/Examples/RandomFan_AttackPattern.asset`: случайные Fan, количество залпов, интервалы, начальная задержка и cooldown.
- `Assets/Configs/AttackPresets/Examples/RandomOrbit_Projectile.asset`: случайные скорость, Orbit, Lifetime и параметры вложенного Child Attack.

## Проверено

- Unity 6000.3.2f1: recompile и domain reload завершились, текущих ошибок Console/компиляции нет.
- Обе обязательные сборки `dotnet build --no-restore --verbosity:quiet --nologo`: 0 ошибок; Runtime содержит прежние предупреждения проекта.
- `VerifyExpandedNumericRandomization`: 3053 проверки; четыре частоты, Fan/Stack, вращение, Child, расписание, пресеты; реальные ECS-снаряды Runtime/Preview для Point/Radial/Circle/Sequential Sector, включая Multiple Circle/Sector.
- `VerifyExpandedNumericEditor`: 12 проверок; Layout/Repaint существующего Custom Inspector, Undo/Redo, Prefab Overrides, сохранение фиксированного значения/диапазона, библиотечный Preview. После domain reload — ещё 5 проверок сохранённых Assets.
- Регрессии: прежняя рандомизация 2815, координатор 50, Sequence 32, PingPong 585, выбор границ Child 3496, переиспользуемые пресеты 55, дочерние Seed/Overrides 64 и 12 сценариев смерти участников фаз — пройдены.
- Через `PrefabUtility.LoadPrefabContents` прочитаны семь разрешённых волн: 29 подволнов, 259 слотов, 3515 конечных значений позиций маршрутов. Ссылки слотов корректны, все новые диапазоны выключены; префабы при проверке не сохранялись.
- EditMode Test Runner не обнаружил зарегистрированных тестов; проверки выполнены через Pipeline `run_script`.

Не проверены полное прохождение боя на устройстве, производительность Android и все возможные сочетания пользовательских диапазонов. Автоматическая проверка Inspector не заменяет ручную оценку компоновки на каждом разрешении окна.

Исходники расширения находятся в `*.Numeric.cs`, `EnemyNumericSample.cs`, `EnemyAttackNumericState.Timing.cs`; общие точки интеграции — `EnemyAttackPatternSchedule`, `EnemyAttackProjectileBuilder`, `SimpleEnemyAttackExecutor`, контроллер волны и существующие Preview. Проверочные скрипты — `AgentScripts/VerifyExpandedNumeric*.cs`, результаты — `AgentArtifacts/attack-randomization-*.json`.
