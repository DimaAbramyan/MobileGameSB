# Layout Rotation: On Attack Finished

Настройка находится в существующем разделе Layout Rotation.

- Continue Rotation (по умолчанию): прежнее поведение между атаками.
- Pause Rotation: сохранить достигнутый угол и остановиться; следующая атака продолжает вращение.
- Reset Rotation: вернуть угол к исходному и остановиться; следующая атака начинает вращение заново.

Границы обычной атаки берутся из BeginAttackSequence/EndAttackSequence. Остановка
ждёт завершения оставшихся эмиссий и других активных атак этого врага. Sequence
дополнительно удерживает программу до её полного завершения: пауза между шагами
не останавливает Layout Rotation. Attack Groups используют тот же учёт отдельно
на каждом исполнителе. При разных политиках у одновременно исполняемых паттернов
общий Layout clock применяет Reset перед Pause, а Continue не отменяет запрос
другого паттерна. Это не меняет порядок или тайминг выстрелов.

Reset Rotation On Enable остаётся настройкой повторного включения врага.
Новая политика не управляет отдельным счётчиком Facing Rotation.

Preview использует тот же расчёт угла и существующие границы атак. Состояние
Layout clock хранится в VisualPose и автоматически входит в WavePreviewStateGraph:
Timeline, snapshots, перемотка и Fast Seek восстанавливают его вместе с атакой.

Целевые проверки: AgentScripts/VerifyLayoutRotationFinish.cs (28 проверок),
регрессия Attack Groups: AgentScripts/VerifyAttackGroupSources.cs (17 проверок).
