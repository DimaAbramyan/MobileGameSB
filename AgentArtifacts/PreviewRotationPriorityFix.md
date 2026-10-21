# Скорость поворота 1_4_extrime

Причина повторного расхождения: у локального override группы FourWay сохранено 22.5°/с, а активный Wave Pattern содержит 8.3°/с и PingPong. Runtime применяет старую настройку к базовому профилю врага, затем заменяет его активным Wave Override. Preview разрешал профиль правильно, но дополнительно передавал старый rotationOverride в вычисление углов. Он перекрывал скорость, режим и кривую активного паттерна.

В WaveShootingPreviewSession.BuildSlot legacy RotationOverride теперь передаётся только при использовании базовых Enemy Settings/автономной атаки. Wave Override использует собственные разрешённые настройки. Значения в Inspector и префабе не изменены.

Новый тест VerifyPreviewRotationPriority воспроизвёл ошибку до исправления. После исправления он проверяет реальный путь local.ApplyTo → Runtime ResolveForWave → SetWaveAttackSettings → Update → выпуск ECS-снарядов. patternRotationTime не назначается вручную. Проверены более 7.5 секунд внутренних часов: полный PingPong и обратный ход. В Runtime и Preview скорость профиля равна 8.3, старое значение 22.5 сохраняется для Enemy Settings. Множитель Sequence 2 даёт 16.6 без повторного наложения legacy override.

Также повторно прошли три режима вращения (265 проверок), транспорт/Sequence/Child/Laser/координатор (41) и существующая волна с перемоткой и сохранностью исходных данных (125). Обе последовательные dotnet-сборки и Unity recompilation завершились без ошибок. MCP переиндексирован.

Открытый Prefab Stage оставлен на 5-й секунде. Новый Play Mode бой не запускался; сравнивался Runtime executor через Pipeline в Edit Mode. Итоговый протокол: preview-rotation-priority-validation.json.
