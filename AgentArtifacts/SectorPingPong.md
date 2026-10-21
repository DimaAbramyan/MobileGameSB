# Sequential Sector: PingPong

В `Emission Layout → Sector → Sequential` вместо Reverse Order показывается
`Launch Order`: Forward / Reverse / PingPong.

Forward выпускает направления от Start Angle к End Angle, Reverse — наоборот.
PingPong чередует направления целых секторов; Fan и Stack одного направления
выпускаются вместе. Геометрия, задержки и скорострельность не меняются.

Только для PingPong:
- `Initial Direction`: Forward / Reverse.
- `Reset Order On Attack Start`: включено по умолчанию. Каждая новая атака
  начинается с Initial Direction; внутри атаки, включая Burst, направления чередуются.
  Если выключить — следующее направление сохраняется между атаками, cooldown и шагами Sequence.

Порядок принадлежит физическому врагу и источнику паттерна. Копии настроек,
создаваемые Runtime/Preview для Sequence, скорости и Local Overrides, сохраняют
идентичность источника. Разные паттерны, в том числе использующие один preset,
не разделяют состояние. Child Attack хранит его на родительском снаряде.

Перекрывающиеся Sector резервируют чередующиеся направления. Завершение последнего
запуска подтверждает переключение. Прерывание/отмена незавершённого сектора
не переключает направление. При Reset старые отложенные завершения не меняют
порядок новой атаки. Отключение/уничтожение физического врага очищает его состояние.
Preview при повторном запуске начинает с исходного направления.

Старое сериализованное поле `reverseOrder` сохранено и остаётся источником значения
Forward/Reverse, включая старые Prefab Overrides. Inspector синхронизирует его с
новым выбором Launch Order. Автоматическая перезапись сцен/префабов не требуется.

Основные файлы:
- `EnemyEmissionLayoutSettings.cs`: настройки и прежняя геометрия.
- `EnemySectorLaunchOrderState.cs`: общий учёт резервирования/завершения/отмены.
- `SimpleEnemyAttackExecutor.cs`: Runtime очереди Sector.
- `EnemyChildAttackSystem.cs`: дочерние атаки.
- `WaveShootingPreviewSession.cs`: Preview с тем же состоянием порядка.
- `EnemyEmissionLayoutSettingsDrawer.cs`: Inspector.

Статический Layout Preview показывает первый сектор с Initial Direction.
Полное чередование видно в анимированном Preview Wave.
