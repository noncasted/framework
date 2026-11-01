# Публичные сеттеры и поля

## Контекст

Вынесено из второго этапа аудита (`docs/audits/2026-11-02_client_stage2.md`), пункт P6. Правило: `ARCHITECTURE_PRINCIPLES.md` P6, данные меняются только через методы, исключение: `[MemoryPackable]`.

Подсчёт аудита от 2026-11-01 устарел: 81 публичный сеттер (3 в тестах) и 459 кандидатов на публичное изменяемое поле (201 в тестах и генераторе), больше всего в `client/core/Editor` (34 и 186). Считалось по шаблону, поэтому туда вошли сериализуемые данные редактора и `[Serializable]`. После этого изменилось правило для `[MemoryPackable]`, а часть кода удалена. Менять API можно, такой коммит помечается `BREAKING:`.

## Что сделать

1. Пересчитать и разделить на группы: настоящие нарушения, `[MemoryPackable]`, поля, которых требует `JsonUtility` или `[Serializable]`, тесты и исходники генератора.
2. Для `JsonUtility` и `[Serializable]` решить общий способ: `[SerializeField] private` с геттером либо записанное исключение.
3. Исправлять по одной группе на коммит, начиная с runtime.

Критерий готовности: новый подсчёт без настоящих нарушений; исключения записаны в правиле; все тесты проходят.

## Примеры

`client/core/Editor/Catalogues/Assets/AssetCatalogGenerator.cs` (`AssetGroupDefinition`, `AssetPropertyDefinition`), `client/core/Editor/Catalogues/Audio/AudioCatalogMetadata.cs:14`, `client/core/Runtime/Options/PlatformOptions.cs` (`PlatformType`, сеттер для окна настроек), `client/modules/settings/Runtime/SettingsSave.cs` (`JsonUtility`).
