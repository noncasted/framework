# Инвентаризация публичных сеттеров и полей

Шаг 1 задачи `public_setters`. Код не менялся. Таблицы поимённого списка в конце файла построены скриптом, проза написана вручную.

## Итоги

| Группа | Что это | Всего | Сеттеры | Поля | Типы |
|---|---|---:|---:|---:|---:|
| A | runtime, настоящие нарушения | 58 | 55 | 3 | 20 |
| B | editor, настоящие нарушения | 118 | 29 | 89 | 25 |
| C | `[MemoryPackable]`, разрешено правилом | 99 | 99 | 0 | 44 |
| D | сериализуемые поля клиента | 85 | 4 | 81 | 15 |
| E | Orleans-состояния и JSON-модели backend | 110 | 110 | 0 | 33 |
| F | тесты и исходники генератора | 240 | 44 | 196 | 63 |
| G | контракты внешних механизмов | 80 | 66 | 14 | 30 |
| | **Всего** | **790** | **407** | **383** | |

Не вошло в счёт:

- `internal` поля и `internal` сеттеры: 0 и 0 (`grep 'internal set'` тоже даёт 0).
- Публичные члены `private`-вложенных типов: 135 (клиент 123, backend 12).
- `set` в интерфейсах: 2, см. конец файла. Они держат сеттеры реализаций.
- `init`, `const`, `readonly`, `event`.

Вошло в счёт, но это решение для оркестратора: 287 членов с `public` внутри `internal`-типа (B 90, D 10, F 173, G 14). Правило говорит «нет публичных сеттеров нигде», а такой член виден всей сборке, поэтому я его считаю. Если считать только реально публичные снаружи сборки, останется 503 члена, а B сократится со 118 до 28. В таблицах такие типы помечены `internal`.

### Сравнение с аудитом от 2026-11-01

| | Аудит | Сейчас |
|---|---:|---:|
| Сеттеры | 81 (3 в тестах) | клиент, `common` и `shared` 185, из них C 99, остаётся 86; backend 222 |
| Поля | 459 кандидатов (201 в тестах и генераторе) | клиент, `common` и `shared` 377 (190 в тестах и генераторе); backend 6 |
| `client/core/Editor` | 34 и 186 | 33 и 126 |

Область аудита нигде не записана. Судя по близости чисел (81 и 86, 34 и 33), backend в него не входил, но это мой вывод, а не проверенный факт. Разница по полям объясняется шаблоном аудита: Roslyn отбрасывает ещё 123 поля `private`-вложенных типов клиента, а также локальные переменные и текст внутри строк.

## Как считалось

Скрипт `docs/tasks/current/public_setters/count_public_members.cs` работает на Roslyn и запускается как file-based app:

```
dotnet run docs/tasks/current/public_setters/count_public_members.cs -- /projects/framework [--md out.md] [--json out.json]
```

- **Что ему нужно:** Unity из `ProjectVersion.txt`, `client/project/Library` и собранный backend (`.build/bin/Tests/Debug/net10.0`). Всё это используется только как ссылки для разрешения символов. Без них скрипт падает с понятным сообщением.
- **Препроцессор:** клиент разбирается с тремя наборами символов (editor, WebGL, standalone), backend с двумя; результаты объединяются. Так видны обе ветки `#if`.
- **`.razor`:** блок `@code` разбирается как класс на тех же строках. Запись в разметке (`@bind`) ищется регулярным выражением по имени.
- **Записи:** колонка таблиц `ext prod / tests / self / by name`. Первые три числа точные, по символу. `by name` означает совпадение только по имени, когда символ не разрешился, или запись в разметке Razor. Это шум у частых имён вроде `Value`, таких записей всего 74. Точное решение по ним требует просмотра места.
- **Записи из игр скрипт не видит.** Они вынесены в отдельный раздел ниже.

Границы групп, которые стоит знать:

- **Unity и `JsonUtility` не сериализуют свойства.** Свойство с телом на `[Serializable]`-типе (`BackendOptions`, `PlatformOptions`) попадает в A или B, а не в D. Исключение: свойство с `[field: SerializeField]`.
- **Сериализатор определяется по вызовам** и по замыканию типов полей от корня. Обобщённые вызовы, где тип статически неизвестен, перечислены в конце файла.
- **`Stored` в колонке сериализатора** означает запись в PostgreSQL через `StateSerializer`: `[GrainState]`, `IStateValue` и всё, что они содержат.

## Проверка

- **Детерминизм:** два запуска, из скретчпада и из репозитория, дали побайтно одинаковый markdown.
- **Сверка с грубым regex:** из 984 совпадений 207 не стали членами. Из них 178 объяснены (`private`-вложенные типы, `readonly`, `operator`, `event`). Оставшиеся 29 лежат в `ContainerGenerator.Verify` (`AuditCases.cs`, `Program.cs`, `RegressionCases.cs`), это код-образцы внутри строковых литералов.
- **Глазами проверены 20 случайных членов (seed 20), все классифицированы верно:**
  - A: `SessionData.Lifetime`, `ClientOutdatedNoticeStyle.PanelColor`, `.ButtonTextSize`, `.ButtonTextColor`
  - B: `SpritePropertyDefinition.Color`, `HierarchyBindingsField.FieldName`, `HierarchyBindingsNode.Target`
  - C: `SharedSessionRequests.Result.ServerUrl`, `ResponseMessageFromServer.RequestId`
  - D: `SettingsSave.MasterVolume`, `ProfilerFramesData.FirstFrame`, `AudioAssetEntry.Encoded`
  - E: `HeapSnapshotRequest.Deep`, `LobbyRegistryState.ListingVersion`, `DeployState.CoordinatorReady`
  - F: `GraphRegistration.Origin`, `GraphMethod.ParentAssembly`
  - G: `PageHeader.Title`, `ClusterHealthWidget.StartupConfig`, `FeaturesStatusWidget.GameFlags`
- **Сгенерированное:** исключён один файл, `client/core/Tests/EditorTools/Fixtures/Generated/ShopBindings.g.cs`. Вне шаблонов сгенерированного кода не найдено. Папка `client/core/Runtime/Tools/Container/Generated` написана вручную и членов в список не даёт.
- **Параллельные правки:** во время работы в дереве появились чужие незакоммиченные изменения (`.claude/docs/ARCHITECTURE_PRINCIPLES.md`, `ContainerGenerator.Verify`, `BenchmarkGraph.cs`, `WebRtcPeerSocket.cs` и другие). Подсчёт сделан по дереву с этими правками. После их коммита или отката числа нужно получить заново.

## Примеры из задачи

| Пример | Группа | Что выяснилось |
|---|---|---|
| `AssetGroupDefinition`, `AssetPropertyDefinition` | B | Это не сериализуемые данные, а `internal`-модели генератора без `[Serializable]`. Чинятся напрямую. `Address` держит `set` в `ICatalogGroupDefinition.cs:7`, интерфейс меняется вместе с ними. |
| `AudioCatalogMetadata.cs:14` | B | `Included`, `Group`, `Volume`. Типом ничего не сериализуется: JSON в `userData` разбирается вручную через `JObject`. Ключи на диске заданы строками и от имён свойств не зависят. |
| `PlatformOptions` | A | Подтверждено: `PlatformType` имеет сеттер с телом, и пишет его `OptionsTab.cs:64` в окне настроек, сборка editor. На диске лежит `[SerializeField] private _platform`, поэтому смена сеттера данные не трогает. `MobileCheck` статический. Так же устроены `BackendOptions`, `DebugOptions`, `VersionOptions`. |
| `SettingsSave` | D | `JsonUtility`, свойства с `[field: SerializeField]`. Самый простой случай в D, см. ниже. |

## Проверенные факты о сериализаторах

Проверено запуском (Orleans 10.3.1, Newtonsoft 13.0.4, `Configuration.Binder` 10.0.5, тест в скретчпаде):

| Механизм | public set | `{ get; private set; }` | `{ get; init; }` |
|---|---|---|---|
| Orleans `[GenerateSerializer]` + `[Id]` | да | да, значение сохраняется и без `[JsonProperty]` | да |
| Newtonsoft | да | **значение теряется** без `[JsonProperty]`, с ним сохраняется | да |
| `ConfigurationBinder` | да | **значение теряется, без ошибки** | да |

Проверено декомпиляцией `Microsoft.AspNetCore.Components` 10.0.11:

- **`[Parameter]` требует публичный сеттер.** `ComponentProperties` бросает исключение «Parameters must be public», если `SetMethod` не публичный.
- **`[Inject]` не требует.** `ComponentFactory` ищет свойства с `Public | NonPublic`, а `PropertySetter` проверяет только наличие сеттера. Все 38 `[Inject] public` можно сделать непубличными.
- **`@bind="Value.X"` компилируется в присваивание,** значит сеттеру нужна доступность из сборки компонента. Это следует из механики Razor, сборкой не проверялось.

Проверено компиляцией: `{ get; set; }` и `{ get; private set; }` дают одно и то же имя поля, `<X>k__BackingField`.

Проверено по репозитории и играм:

- **`JsonUtility` пишет `[field: SerializeField]` как `<Name>k__BackingField`.** Об этом говорит комментарий в `ItchSaves.cs:11`; из-за этого там однажды уже сбрасывали сохранения (`save_v2`).
- **Ключи YAML совпадают с именами полей:** `Name`, `Entries`, `Value` в `ColorCatalog.asset`, `Name`, `Clip`, `Volume` в `AudioGroupAsset`.
- **Файлы, которых переименование в D не касается:** `Framework.json` (ключи `ContentAssembly` и другие) и json-списки каталогов (`groups`, `nonAddressable`). Их читает Newtonsoft через `private`-вложенные типы `FrameworkSettings.Data` и `RegistryFile`, эти типы в счёт не входят.

Число ассетов в играх, в которых лежат типы D:

| Контейнер | mines-leader | peace-data-reborn | pixel-swarm | storytale-workshop |
|---|---:|---:|---:|---:|
| `EnvAssetGroupAsset` | 3 | 3 | 3 | 5 |
| `AudioGroupAsset` | 2 | 5 | 3 | 0 |
| `PrefabGroupAsset` | 3 | 4 | 3 | 1 |
| `SpriteGroupAsset` | 12 | 10 | 3 | 0 |
| `ColorCatalog` | 1 | 1 | 1 | 0 |
| `LocalizationOptions` | 0 | 0 | 0 | 0 |

## Риск: переименование поля ломает сохранённые данные

Если поле становится `_camelCase`, меняется его ключ на диске. Сейчас `FormerlySerializedAs` не используется ни разу: ни в коде framework, ни в коде mines-leader.

**Что не проверено.** Эти пункты требуют редактора, а на этом шаге его запускать нельзя:

1. **`JsonUtility` с `[SerializeField] private`.** Обычно считается, что он работает так же, как для публичных полей, но в репозитории такого примера нет, а поиск подтверждения не дал.
2. **`[FormerlySerializedAs]` с `JsonUtility.FromJson`.** Прямых данных нет. Для `FromJsonOverwrite` в трекере Unity есть [баг со статусом Won't Fix](https://issuetracker.unity3d.com/issues/jsonutility-dot-fromjsonoverwrite-doesnt-overwrite-objects-data-when-given-a-formerlyserialized-name): старое имя не применяется. Рассчитывать на этот атрибут в JSON нельзя.
3. **`[field: FormerlySerializedAs("Name")]` для YAML.** В [документации атрибута](https://docs.unity3d.com/es/ScriptReference/Serialization.FormerlySerializedAsAttribute.html) этот случай с backing-полем не разобран.
4. **`EditorJsonUtility`** в коде framework не встречается, проверять нечего.

Перед исправлением D2 нужен EditMode-тест на пункты 1-3 в Unity проекта.

**Что сломается молча, кроме данных.** Строковые ключи `FindPropertyRelative("Name")` и подобные: 28 мест в `AudioCatalogGenerator`, `AssetCatalogGenerator`, `SpriteGenerator`, `PrefabCatalogGenerator`, ещё 2 в `LocalizerTests`. При смене имени поля они начнут получать `null`.

## Варианты для группы D (решает оркестратор)

D распадается на три разных случая, и общий способ здесь не подходит:

| Часть | Типы | Членов | Хранение |
|---|---|---:|---|
| D1 | `SettingsSave` | 4 | JSON сохранений игроков, ключ `<Name>k__BackingField` |
| D2 | `EnvAssetEntry`, `AudioAssetEntry`, `ColorEntry`, `ColorGroup`, `PrefabAssetEntry`, `SpriteEntry`, `SystemLanguageMapping` | 20 | YAML ассетов игр (`SystemLanguageMapping` в играх пока не найден) |
| D3 | `ProfilerTraceData`, `ProfilerSpanData`, `ProfilerFramesData`, `ProfilerFrameData`, `ProfilerSampleData`, `HierarchyBindingsRequest`, `HierarchyBindingsQueue` | 61 | JSON трасс профайлера на диске и `SessionState` редактора |

**D1: заменить `{ get; set; }` на `{ get; private set; }` и добавить метод.** Имя поля и ключ JSON не меняются, что проверено выше. Запись идёт из 4 мест в коде и 2 в тестах.

**D2, варианты:**

- **(а) Рекомендую:** `[SerializeField] private _name`, геттер `Name` и `[FormerlySerializedAs("Name")]`. Это уже записано в правиле стиля («A Unity field shown in the inspector is `[SerializeField] private`»), и этот атрибут создан как раз для YAML. Цена: правка 28 строковых ключей и прогон генераторов каталогов на ассетах игры. Атрибут нельзя удалять, пока все игры не пересохранили ассеты.
- **(б)** `[field: SerializeField] public X { get; private set; }`. Кода меньше, но ключи становятся `<Name>k__BackingField`, и возникает риск по пункту 3.
- **(в)** Записанное исключение. Самое дешёвое, но расходится с правилом стиля.

**D3, варианты:**

- **(а) Рекомендую:** записанное исключение для `[Serializable]`-типов, которые читает и пишет только `JsonUtility`. Причины: `JsonUtility` не видит свойства и `init`, имена полей здесь и есть формат файла, а `FormerlySerializedAs` для JSON ненадёжен. Запись вне сериализатора у этих типов есть (заполнение при сборе трассы), поэтому исключение нужно ограничить именно DTO без логики.
- **(б)** `[field: SerializeField]` + `private set` и конструктор. Старые трассы перестанут читаться, что допустимо. В `.claude` и `docs` упоминаний ключей трасс (`TotalMs`, `DurationMs`) нет, но внешние читатели этим не исключены.

## Заметки по остальным группам

**E (110: stored 51, сообщения 59).**

- Сообщения можно смело переводить на `init` или `private set`: Orleans работает с обоими.
- Для stored безопаснее `init` там, где значение не меняется. Для `private set` обязателен `[JsonProperty]`, иначе значение молча потеряется при загрузке. Сейчас `[JsonProperty]` нет ни на одном члене E, потому что сеттеры публичные.
- Перед исправлением stored нужен тест загрузки из JSON на старом коде.
- **Ловушка:** `TaskBalancerOptions` (5), `SideEffectsOptions` (5), `TransactionOptions` (2) и `DurableQueueOptions` (1) редактируются в консоли через `@bind="Value.X"`, всего 13 привязок в `backend/Console/Infrastructure/Options`. С `private set` или `init` такие редакторы не скомпилируются, значит, им нужна своя модель формы или исключение. У `RuntimeChannelOptions` и `RuntimePipeOptions` редактора в этой папке нет.

**G (80).** Внешний механизм реально требует public не везде:

| Подгруппа | Членов | Нужен ли публичный сеттер |
|---|---:|---|
| Blazor `[Parameter]` | 15 | да, проверено; записать исключением |
| Blazor `[Inject]` | 38 | нет, проверено; чинить |
| Именованный аргумент атрибута (`DropdownAttribute.VisibleIf`) | 1 | да, требование языка C#; записать исключением |
| `Response` из Orleans (`TransactionResponse.Result`, `.Exception`) | 2 | да, это `override` чужого публичного свойства |
| `IAccessContainerWindow` (4 тулбара) | 4 | нет, по правилам языка возможна явная реализация интерфейса (не собиралось) |
| Options: `ClusterStartupOptions`, `ServiceDiscoveryOptions`, `CoordinatorHealthOptions` | 6 | нет: двое пишутся лямбдой `Configure<T>`, у третьего внешних записей в коде нет, только 1 в тестах. Вызовов `AddEnvironmentOptions<T>` (binder) с типами framework нет; при binder подойдёт `init`, но не `private set` |
| `NativeWebRtcApi.Configuration` | 14 | маршалинг публичности не требует (общее знание, не запускалось); поля пишет только владелец |

**F (240).** Правило прямо включает тесты и бенчмарки, поэтому тесты (68) остаются нарушениями. Из 172 членов исходников генератора 163 лежат в двух файлах моделей: `ScopePlan.cs` (100) и `GraphModel.cs` (63). Правка большая, а на генерируемый код она влиять не должна; это проверит `ContainerGenerator.Verify`. Оркестратору стоит решить: чинить или записать исключение.

## Записи из игр

Скрипт видит только этот репозиторий. Поиск по именам типов в `mines-leader`, `peace-data-reborn`, `pixel-swarm`, `storytale-workshop` (без `tools/framework`) показал, сколько файлов упоминает тип. Это верхняя оценка, а не число записей.

| Тип | Файлов | Заметка |
|---|---:|---|
| `SessionData` | 38 | самый заметный `BREAKING` в A |
| `ViewableDictionary` | 20 | индексатор |
| `TaskBalancerOptions` | 21 | |
| `ClusterFeaturesState` | 20 | |
| `SettingsSave` | 14 | **имя неоднозначно:** в трёх играх есть свой `SettingsSave` в `Common/Global/Settings`, 51 найденная запись по именам полей относится в основном к ним |
| `BackendOptions` | 8 | |
| `FileLoggerOptions` | 7 | **читается из конфигурации игры** (ключи `FileLogging:*` в тестах pixel-swarm). Подойдёт только `init`: `private set` binder молча потеряет. В коде framework binder для него не найден, поэтому тип стоит в A |
| `PlatformOptions`, `GamepadState`, `ButtonSounds` | по 6 | |
| `ClientOutdatedNoticeStyle` | 3 | игра собирает стиль, подойдёт `init` |

Часть файлов может быть копией кода framework, ещё не перенесённой в подмодуль. По памяти проекта storytale находится в середине миграции.

## Порядок коммитов

Один коммит на группу. Коммиты, меняющие API, помечаются `BREAKING:`.

| # | Коммит | Членов | Размер | Зависит от |
|---|---|---:|---|---|
| 1 | A1: `common`, `shared`, `backend` (`SessionData`, статические `Handler`, `IsEnabled`, `ObserverData`, `FileLoggerOptions`, индексатор). `BREAKING` | 16 | средний: `SessionData` задевает игры | - |
| 2 | A2: `client/core/Runtime` (options + `OptionsTab`, `UpdaterSample`, статические поля) | 17 | средний | - |
| 3 | A3: модули (`ClientOutdatedNoticeStyle`, `GamepadState`) | 25 | малый | - |
| 4 | Запись правила: исключения C, `[Parameter]`, чужие контракты, D3 и решение по `public` внутри `internal` | - | малый | решение оркестратора |
| 5 | B1: модели каталогов (вместе с `ICatalogGroupDefinition`) | 99 | большой, механический | 4 |
| 6 | B2: остальные инструменты редактора | 19 | малый | 4 |
| 7 | D1: `SettingsSave` | 4 | малый | - |
| 8 | D2: записи каталогов, с EditMode-тестом и прогоном генераторов | 20 | средний, самый рискованный | 4, тест в редакторе |
| 9 | E1: сообщения Orleans | 59 | средний | - |
| 10 | E2: stored-состояния, `[JsonProperty]`, тест загрузки, редакторы консоли | 51 | большой | 9 |
| 11 | G: `[Inject]`, тулбары, options, interop | 62 | средний | 4 |
| 12 | F1: тесты | 68 | средний | после всех выше: тесты правятся вслед за API |
| 13 | F2: генератор, если не исключение | 172 | большой | 4 |

Числа A1-A3 в сумме дают 58, B1 и B2 дают 118. В G оставшиеся 18 из 80 подпадают под исключения или требуют отдельного решения (15 `[Parameter]`, 1 атрибут, 2 `Response`).

## Поимённый список

Колонка `members`: `f` означает поле, без пометки свойство с сеттером. Колонка записей: `внешние в коде / внешние в тестах / изнутри типа / только по имени`.

### A. Runtime, настоящие нарушения: 58

#### backend/Common: 1

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `backend/Common/Extensions/TaskExtensions.cs:8` | `NoAwaitExceptions` | `Handler` | 1 / 4 / 0 / 0 | - | static |

#### backend/Infrastructure: 8

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `backend/Infrastructure/Logging/FileLoggerProvider.cs:14` | `FileLoggerOptions` | `Enabled`, `Directory`, `MinLevel` | 0 / 3 / 0 / 0 | - |  |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs:322` | `RuntimeChannel.ObserverData` | `Observer`, `UpdateDate`, `DeliveryTask` | 5 / 0 / 0 / 2 | - |  |
| `backend/Infrastructure/Messaging/Queues/DurableQueue.cs:181` | `DurableQueue.ObserverData` | `Observer`, `UpdateDate` | 2 / 0 / 0 / 2 | - |  |

#### client/core/Runtime: 17

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `client/core/Runtime/Catalogues/Audio/ButtonSounds.cs:14` | `ButtonSounds` | `Clicked`, `Hovered` | 0 / 0 / 0 / 0 | - | static |
| `client/core/Runtime/Common/Reactive/Lifetimes/Analytics/LifetimeAnalytics.cs:86` | `LifetimeAnalytics` | `IsEnabled` | 4 / 0 / 0 / 4 | - | static |
| `client/core/Runtime/Options/BackendOptions.cs:20` | `BackendOptions` | `Environment`, `ProductionApiUrl`, `LocalApiUrl`, `TimeoutSeconds` | 4 / 7 / 0 / 0 | Unity | [Serializable], body |
| `client/core/Runtime/Options/DebugOptions.cs:12` | `DebugOptions` | `EnableGizmos`, `EnableLogs` | 2 / 0 / 0 / 0 | Unity | [Serializable], body |
| `client/core/Runtime/Options/PlatformOptions.cs:14` | `PlatformOptions` | `PlatformType`, `MobileCheck` | 2 / 0 / 0 / 0 | Unity | [Serializable], body, static |
| `client/core/Runtime/Options/VersionOptions.cs:11` | `VersionOptions` | `Value` | 1 / 0 / 0 / 17 | Unity | [Serializable], body |
| `client/core/Runtime/Services/Updaters/UpdaterSampling.cs:75` | `UpdaterSample` | `Ticks` f, `Calls` f | 4 / 0 / 0 / 0 | - |  |
| `client/core/Runtime/Tools/Container/Abstract/ContainerRegistryDebug.cs:20` | `ContainerRegistryDebug` | `IsEnabled` | 0 / 2 / 0 / 4 | - | static |
| `client/core/Runtime/Tools/HierarchyBindings/ObjectBindings.cs:42` | `ObjectBindings` | `RegenerateHandler` f | 1 / 0 / 0 / 1 | Unity | static |
| `client/core/Runtime/Tools/Profiling/ProfilerTraceStorage.cs:30` | `ProfilerTraceStorage` | `ColdDomain` | 1 / 0 / 1 / 0 | - | static |

#### client/modules/inputs: 5

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `client/modules/inputs/Runtime/GamepadReader.cs:34` | `GamepadState` | `LeftStick`, `RightStick`, `LeftTrigger`, `RightTrigger`, `Buttons` | 4 / 0 / 2 / 0 | - |  |

#### client/modules/ui: 20

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `client/modules/ui/Runtime/ClientOutdated/ClientOutdatedNoticeStyle.cs:11` | `ClientOutdatedNoticeStyle` | `Font`, `Title`, `Text`, `Button`, `ReferenceResolution`, `PanelSize`, `ButtonSize`, `Padding`, `Spacing`, `Border`, `TitleSize`, `TextSize`, `ButtonTextSize`, `ShadeColor`, `PanelBorderColor`, `PanelColor`, `TitleColor`, `TextColor`, `ButtonColor`, `ButtonTextColor` | 0 / 1 / 0 / 0 | - |  |

#### common: 3

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `common/Reactive/DataTypes/Dictionaries/ViewableDictionary.cs:21` | `ViewableDictionary` | `this[]` | 1 / 3 / 0 / 0 | - | body |
| `common/Reactive/Events/ReactiveExceptions.cs:13` | `ReactiveExceptions` | `Handler` | 1 / 6 / 0 / 0 | - | static |
| `common/Reactive/Lifetimes/LifetimeTracker.cs:16` | `LifetimeTracker` | `Current` | 1 / 0 / 0 / 0 | - | static |

#### shared: 4

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `shared/SessionCore/Root/SessionData.cs:19` | `SessionData` | `Type`, `ExpectedUsers`, `Id`, `Lifetime` | 8 / 0 / 0 / 7 | - |  |

### B. Editor, настоящие нарушения: 118

#### client/core/Editor: 118

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `client/core/Editor/Catalogues/Assets/AssetCatalogGenerator.cs:316` | `AssetCatalogGenerator.AssetSource` | `Path` f, `Group` f, `PropertyName` f, `Asset` f | 4 / 8 / 0 / 1 | - | internal |
| `client/core/Editor/Catalogues/Assets/AssetCatalogGenerator.cs:325` | `AssetGroupDefinition` | `Name`, `Address`, `ClassName` f, `TargetNamespace` f, `GeneratedFolder` f, `SkipCodegen` f, `IsAddressable` f, `ResourcePath` f, `Properties` f | 10 / 8 / 0 / 2 | - | internal, iface:ICatalogGroupDefinition |
| `client/core/Editor/Catalogues/Assets/AssetCatalogGenerator.cs:338` | `AssetPropertyDefinition` | `PropertyName` f, `FieldName` f, `Asset` f, `TypeFullName` f, `TypeAssembly` f | 5 / 7 / 0 / 0 | - | internal |
| `client/core/Editor/Catalogues/Assets/AssetCatalogMetadata.cs:12` | `AssetCatalogMetadata` | `Included`, `Group` | 2 / 0 / 2 / 0 | - |  |
| `client/core/Editor/Catalogues/Audio/AudioCatalogGenerator.cs:435` | `AudioCatalogGenerator.ClipSource` | `Path` f, `Group` f, `PropertyName` f, `Variation` f, `Clip` f, `Encoded` f, `Volume` f | 7 / 6 / 0 / 1 | - | internal |
| `client/core/Editor/Catalogues/Audio/AudioCatalogGenerator.cs:459` | `AudioGroupDefinition` | `Name`, `Address`, `ClassName` f, `Properties` f, `Variations` f | 3 / 4 / 0 / 2 | - | internal, iface:ICatalogGroupDefinition |
| `client/core/Editor/Catalogues/Audio/AudioCatalogGenerator.cs:468` | `AudioPropertyDefinition` | `PropertyName` f, `FieldName` f, `Variation` f, `Clip` f, `Encoded` f, `Volume` f | 6 / 2 / 0 / 0 | - | internal |
| `client/core/Editor/Catalogues/Audio/AudioCatalogGenerator.cs:478` | `AudioVariationDefinition` | `PropertyName` f, `FieldName` f, `Entries` f | 2 / 0 / 0 / 0 | - | internal |
| `client/core/Editor/Catalogues/Audio/AudioCatalogMetadata.cs:14` | `AudioCatalogMetadata` | `Included`, `Group`, `Volume` | 3 / 2 / 3 / 0 | - |  |
| `client/core/Editor/Catalogues/Prefabs/PrefabCatalogGenerator.cs:471` | `PrefabCatalogGenerator.PrefabSource` | `Path` f, `Group` f, `PropertyName` f, `Prefab` f, `ComponentType` f | 5 / 8 / 0 / 1 | - | internal |
| `client/core/Editor/Catalogues/Prefabs/PrefabCatalogGenerator.cs:481` | `PrefabGroupDefinition` | `Name`, `Address`, `ClassName` f, `GeneratedFolder` f, `SkipCodegen` f, `IsAddressable` f, `ResourcePath` f, `Properties` f | 8 / 3 / 0 / 2 | - | internal, iface:ICatalogGroupDefinition |
| `client/core/Editor/Catalogues/Prefabs/PrefabCatalogGenerator.cs:493` | `PrefabPropertyDefinition` | `PropertyName` f, `FieldName` f, `Prefab` f, `ComponentType` f, `ComponentDisplayName` f, `IsGameObject` f, `TypeFullName` f, `TypeAssembly` f, `CodeTypeName` f | 13 / 4 / 0 / 0 | - | internal |
| `client/core/Editor/Catalogues/Prefabs/PrefabCatalogMetadata.cs:13` | `PrefabCatalogMetadata` | `Included`, `Group`, `ComponentType`, `ComponentGuid` | 6 / 5 / 4 / 0 | - |  |
| `client/core/Editor/Catalogues/Sprites/SpriteCatalogMetadata.cs:20` | `SpriteCatalogAnimation` | `Tag` f, `Layers` f | 4 / 0 / 2 / 0 | - |  |
| `client/core/Editor/Catalogues/Sprites/SpriteCatalogMetadata.cs:33` | `SpriteCatalogTagSpeed` | `Tag` f, `FramesPerSecond` f | 4 / 0 / 0 / 0 | - |  |
| `client/core/Editor/Catalogues/Sprites/SpriteCatalogMetadata.cs:40` | `SpriteCatalogCustomAnimation` | `Name` f, `Source` f, `Layers` f | 8 / 0 / 3 / 2 | - |  |
| `client/core/Editor/Catalogues/Sprites/SpriteCatalogMetadata.cs:64` | `SpriteCatalogMetadata` | `Included`, `Group`, `Kind`, `Time`, `Color`, `FramesPerSecond`, `Animations`, `CustomAnimations`, `TagSpeeds` | 12 / 4 / 13 / 0 | - |  |
| `client/core/Editor/Catalogues/Sprites/SpriteGenerator.cs:665` | `SpriteGroupDefinition` | `Name`, `Address`, `Path` f, `ClassName` f, `Properties` f | 6 / 5 / 0 / 3 | - | internal, iface:ICatalogGroupDefinition |
| `client/core/Editor/Catalogues/Sprites/SpriteGenerator.cs:674` | `SpritePropertyDefinition` | `PropertyName` f, `FieldName` f, `Owner` f, `MemberName` f, `Kind` f, `Sprites` f, `Time` f, `Color` f | 9 / 3 / 0 / 0 | - | internal |
| `client/core/Editor/Tools/Codegen/CodeIdentifiers.cs:97` | `IdentifierScope` | `EnclosingTypeName` | 1 / 0 / 1 / 0 | - |  |
| `client/core/Editor/Tools/HierarchyBindings/HierarchyBindingsBatch.cs:600` | `HierarchyBindingsBatch.PrefabItem` | `VariantDepth` f, `HierarchyDepth` f, `FullTypeName` f, `Request` f | 4 / 2 / 0 / 0 | - | internal |
| `client/core/Editor/Tools/HierarchyBindings/HierarchyBindingsNode.cs:9` | `HierarchyBindingsNode` | `Target` f, `HierarchyPath` f, `TypeName` f, `PropertyName` f, `FieldName` f, `Members` f, `IgnoreChildren` f | 11 / 0 / 0 / 0 | - | internal |
| `client/core/Editor/Tools/HierarchyBindings/HierarchyBindingsNode.cs:40` | `HierarchyBindingsField` | `Target` f, `PropertyName` f, `FieldName` f, `CodeTypeName` f, `Comment` f | 19 / 0 / 0 / 0 | - | internal |
| `client/core/Editor/Tools/LifetimeGraph/LifetimeTracking.cs:64` | `LifetimeTracking` | `IsTrackingEnabled` | 0 / 0 / 1 / 0 | - | body, static |
| `client/core/Editor/Tools/Profiling/ProfilerFrameCapture.cs:69` | `ProfilerFrameCapture` | `Enabled` | 1 / 0 / 0 / 0 | - | body, static |

### C. `[MemoryPackable]` (разрешено): 99

#### shared: 99

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `shared/Lobbies/LobbyCountdownState.cs:12` | `LobbyCountdownState` | `IsActive`, `SecondsLeft` | 8 / 2 / 0 / 0 | - |  |
| `shared/Lobbies/LobbyGameState.cs:12` | `LobbyGameState` | `ServerUrl`, `SessionId`, `PlayersCount` | 9 / 2 / 0 / 0 | - |  |
| `shared/Lobbies/LobbyStartState.cs:10` | `LobbyStartState` | `IsStarting` | 0 / 1 / 0 / 0 | - |  |
| `shared/Lobbies/LobbyState.cs:10` | `LobbyState` | `Code`, `OwnerIndex`, `IsRunning`, `IsRunOver` | 6 / 2 / 0 / 0 | - |  |
| `shared/Lobbies/LobbyVisibilityState.cs:11` | `LobbyVisibilityState` | `IsOpen` | 0 / 1 / 0 / 0 | - |  |
| `shared/Lobbies/SharedLobbies.cs:24` | `SharedLobbies.JoinLobby` | `Code` | 1 / 0 / 0 / 0 | - |  |
| `shared/Lobbies/SharedLobbies.cs:33` | `SharedLobbies.JoinMatch` | `Code` | 1 / 0 / 0 / 0 | - |  |
| `shared/Lobbies/SharedLobbies.cs:54` | `SharedLobbies.List` | `Entries` | 0 / 0 / 0 / 0 | - |  |
| `shared/Lobbies/SharedLobbies.cs:60` | `SharedLobbies.Entry` | `Code`, `LeaderName`, `PlayersCount`, `MaxPlayers`, `IsRunning` | 0 / 0 / 0 / 0 | - |  |
| `shared/Matchmaking/SharedSessionRequests.cs:31` | `SharedSessionRequests.Result` | `ServerUrl`, `SessionId`, `Code`, `Status`, `Ticket`, `IsLobby` | 11 / 4 / 0 / 0 | - |  |
| `shared/Matchmaking/SharedSessionRequests.cs:58` | `SharedSessionRequests.TicketRequest` | `SessionId` | 1 / 0 / 0 / 0 | - |  |
| `shared/Matchmaking/SharedSessionRequests.cs:64` | `SharedSessionRequests.TicketResponse` | `Ticket` | 0 / 0 / 0 / 0 | - |  |
| `shared/Protocol/EmptyResponse.cs:22` | `EmptyResponse` | `HasError`, `Message` | 0 / 0 / 3 / 0 | - |  |
| `shared/Protocol/ErrorResponse.cs:23` | `ErrorResponse` | `Code`, `Message` | 4 / 4 / 4 / 0 | - |  |
| `shared/Protocol/Messages.cs:16` | `OneWayMessageFromServer` | `Context` | 2 / 0 / 0 / 0 | - |  |
| `shared/Protocol/Messages.cs:22` | `ResponseMessageFromServer` | `Context`, `RequestId` | 6 / 0 / 0 / 0 | - |  |
| `shared/Protocol/Messages.cs:29` | `RequestMessageFromServer` | `Context`, `RequestId` | 2 / 0 / 0 / 0 | - |  |
| `shared/Protocol/Messages.cs:44` | `OneWayMessageFromClient` | `Context` | 1 / 4 / 0 / 0 | - |  |
| `shared/Protocol/Messages.cs:50` | `RequestMessageFromClient` | `Context`, `RequestId` | 2 / 4 / 0 / 0 | - |  |
| `shared/Protocol/Messages.cs:57` | `ResponseMessageFromClient` | `Context`, `RequestId` | 2 / 2 / 0 / 0 | - |  |
| `shared/Session/SharedSessionAuth.cs:16` | `SharedSessionAuth.Request` | `Ticket` | 1 / 4 / 0 / 0 | - |  |
| `shared/Session/SharedSessionAuth.cs:22` | `SharedSessionAuth.Response` | `Status` | 1 / 12 / 0 / 0 | - |  |
| `shared/Session/SharedSessionEntity.cs:11` | `SharedSessionEntity.CreateRequest` | `Id`, `Properties`, `Payload` | 3 / 32 / 0 / 3 | - |  |
| `shared/Session/SharedSessionEntity.cs:19` | `SharedSessionEntity.CreateResponse` | `EntityId` | 1 / 0 / 0 / 0 | - |  |
| `shared/Session/SharedSessionEntity.cs:25` | `SharedSessionEntity.CreatedOverview` | `OwnerId`, `EntityId`, `Properties`, `Payload` | 4 / 0 / 0 / 0 | - |  |
| `shared/Session/SharedSessionEntity.cs:34` | `SharedSessionEntity.Destroy` | `EntityId` | 1 / 1 / 0 / 0 | - |  |
| `shared/Session/SharedSessionEntity.cs:40` | `SharedSessionEntity.DestroyUpdate` | `EntityId` | 1 / 0 / 0 / 0 | - |  |
| `shared/Session/SharedSessionLatency.cs:17` | `SharedSessionLatency.Ping` | `Id` | 1 / 10 / 0 / 3 | - |  |
| `shared/Session/SharedSessionLatency.cs:23` | `SharedSessionLatency.Pong` | `Id` | 1 / 5 / 0 / 3 | - |  |
| `shared/Session/SharedSessionLatency.cs:29` | `SharedSessionLatency.PeerProbe` | `Id`, `SenderIndex`, `ServerTimestamp` | 3 / 0 / 0 / 3 | - |  |
| `shared/Session/SharedSessionLatency.cs:40` | `SharedSessionLatency.PeerEcho` | `Id`, `SenderIndex`, `ServerTimestamp`, `ResponderPingMs`, `ResponderIndex`, `ResponderLegMs` | 6 / 0 / 0 / 3 | - |  |
| `shared/Session/SharedSessionObject.cs:11` | `SharedSessionObject.SetProperty` | `ObjectId`, `PropertyId`, `Value` | 6 / 21 / 0 / 17 | - |  |
| `shared/Session/SharedSessionObject.cs:21` | `SharedSessionObject.SetProperties` | `Entries` | 1 / 2 / 0 / 0 | - |  |
| `shared/Session/SharedSessionObject.cs:27` | `SharedSessionObject.PropertyUpdate` | `ObjectId`, `PropertyId`, `Value`, `Version` | 21 / 22 / 0 / 17 | - |  |
| `shared/Session/SharedSessionObject.cs:36` | `SharedSessionObject.Event` | `ObjectId`, `Value` | 2 / 4 / 0 / 17 | - |  |
| `shared/Session/SharedSessionPeer.cs:26` | `SharedSessionPeer.Signal` | `TargetIndex`, `SenderIndex`, `Kind`, `Type`, `Value`, `Mid` | 6 / 0 / 0 / 21 | - |  |
| `shared/Session/SharedSessionPeer.cs:41` | `SharedSessionPeer.Tunnel` | `TargetIndex`, `SenderIndex`, `Frame` | 3 / 0 / 0 / 0 | - |  |
| `shared/Session/SharedSessionPlayer.cs:14` | `SharedSessionPlayer.LocalUpdate` | `Index`, `Id`, `EntityCounter`, `IsReturn`, `SeatEntities` | 5 / 0 / 0 / 3 | - |  |
| `shared/Session/SharedSessionPlayer.cs:34` | `SharedSessionPlayer.RemoteUpdate` | `Index`, `Id` | 6 / 0 / 0 / 3 | - |  |
| `shared/Session/SharedSessionPlayer.cs:43` | `SharedSessionPlayer.RemoteDisconnect` | `Index` | 1 / 0 / 0 / 0 | - |  |
| `shared/Session/SharedSessionPlayer.cs:51` | `SharedSessionPlayer.RemoteAway` | `Index` | 2 / 0 / 0 / 0 | - |  |
| `shared/Session/SharedSessionService.cs:11` | `SharedSessionService.GetRequest` | `Key`, `PropertiesIds` | 4 / 18 / 0 / 3 | - |  |
| `shared/Session/SharedSessionService.cs:18` | `SharedSessionService.GetResponse` | `Key`, `Id`, `Properties` | 3 / 0 / 0 / 6 | - |  |
| `shared/Session/SharedSessionService.cs:26` | `SharedSessionService.Overview` | `Key`, `Properties` | 2 / 0 / 0 / 3 | - |  |

### D. Сериализуемые поля клиента (`JsonUtility`, Unity, `[Serializable]`): 85

#### editor: 37

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `client/core/Editor/Tools/HierarchyBindings/HierarchyBindingsRequest.cs:13` | `HierarchyBindingsRequest` | `AssetPath` f, `ObjectPath` f, `ObjectNames` f, `TypeName` f, `Namespace` f, `IsPrefabAsset` f, `IsSceneService` f, `IsEntityComponent` f, `StructureHash` f | 17 / 20 / 0 / 0 | JsonUtility | internal, [Serializable] |
| `client/core/Editor/Tools/HierarchyBindings/HierarchyBindingsRequest.cs:40` | `HierarchyBindingsQueue` | `Requests` f | 0 / 0 / 0 / 0 | JsonUtility | internal, [Serializable] |
| `client/core/Editor/Tools/Profiling/ProfilerFramesData.cs:14` | `ProfilerFramesData` | `TraceName` f, `TraceId` f, `FirstFrame` f, `LastFrame` f, `Frames` f, `MissedFrames` f, `HeadGapMs` f, `TailGapMs` f, `DeepProfiling` f, `ThreadName` f, `MinSampleMs` f, `Warning` f | 14 / 0 / 0 / 0 | JsonUtility | [Serializable] |
| `client/core/Editor/Tools/Profiling/ProfilerFramesData.cs:48` | `ProfilerFrameData` | `Frame` f, `ProfilerFrame` f, `StartMs` f, `StartNs` f, `EndNs` f, `DurationMs` f, `GcAlloc` f, `Samples` f, `Truncated` f | 10 / 0 / 0 / 0 | JsonUtility | [Serializable] |
| `client/core/Editor/Tools/Profiling/ProfilerFramesData.cs:74` | `ProfilerSampleData` | `Depth` f, `Name` f, `TotalMs` f, `SelfMs` f, `Calls` f, `GcAlloc` f | 6 / 0 / 0 / 2 | JsonUtility | [Serializable] |

#### runtime: 48

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `client/core/Runtime/Catalogues/Assets/EnvAssetGroupAsset.cs:9` | `EnvAssetEntry` | `Name` f, `Asset` f | 0 / 0 / 0 / 2 | Unity | [Serializable] |
| `client/core/Runtime/Catalogues/Audio/AudioGroupAsset.cs:12` | `AudioAssetEntry` | `Name` f, `Clip` f, `Encoded` f, `Volume` f | 1 / 4 / 0 / 2 | Unity | [Serializable] |
| `client/core/Runtime/Catalogues/Colors/ColorCatalog.cs:13` | `ColorEntry` | `Name` f, `Value` f | 0 / 2 / 0 / 19 | Unity | [Serializable] |
| `client/core/Runtime/Catalogues/Colors/ColorCatalog.cs:20` | `ColorGroup` | `Name` f, `Entries` f | 0 / 2 / 0 / 2 | Unity | [Serializable] |
| `client/core/Runtime/Catalogues/Prefabs/PrefabGroupAsset.cs:9` | `PrefabAssetEntry` | `Name` f, `Prefab` f, `ComponentType` f | 0 / 0 / 0 / 2 | Unity | [Serializable] |
| `client/core/Runtime/Catalogues/Sprites/SpriteGroupAsset.cs:15` | `SpriteEntry` | `Name` f, `Kind` f, `Sprites` f, `Time` f, `Color` f | 0 / 0 / 0 / 2 | Unity | [Serializable] |
| `client/core/Runtime/Tools/Profiling/ProfilerTraceData.cs:13` | `ProfilerTraceData` | `Name` f, `Id` f, `StartedAt` f, `DurationMs` f, `Frames` f, `StartFrame` f, `EndFrame` f, `StartNs` f, `EndNs` f, `Cold` f, `Spans` f | 11 / 0 / 0 / 5 | JsonUtility | [Serializable] |
| `client/core/Runtime/Tools/Profiling/ProfilerTraceData.cs:39` | `ProfilerSpanData` | `Id` f, `ParentId` f, `Depth` f, `Name` f, `StartMs` f, `DurationMs` f, `Frames` f, `StartFrame` f, `EndFrame` f, `StartNs` f, `EndNs` f, `Unfinished` f, `AllocBytes` f | 13 / 0 / 0 / 5 | JsonUtility | [Serializable] |
| `client/modules/localization/Runtime/LocalizationOptions.cs:30` | `SystemLanguageMapping` | `System` f, `Language` f | 0 / 0 / 0 / 0 | Unity | [Serializable] |
| `client/modules/settings/Runtime/SettingsSave.cs:13` | `SettingsSave` | `MasterVolume`, `SoundsVolume`, `MusicVolume`, `WasChanged` | 4 / 2 / 3 / 0 | JsonUtility+Unity | [Serializable], field:SerializeField |

### E. Orleans-состояния и JSON-модели backend: 110

#### backend/Cluster: 53

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `backend/Cluster/Deploy/DeployIdPipe.cs:18` | `DeployIdResponse` | `DeployId` | 0 / 5 / 0 / 1 | - | [Id] |
| `backend/Cluster/Deploy/DeployState.cs:11` | `DeployState` | `DeployId`, `LastHeartbeat`, `CoordinatorReady` | 0 / 5 / 0 / 6 | Stored | [Id] |
| `backend/Cluster/Discovery/ServiceDiscoveryStorageState.cs:12` | `ServiceDiscoveryStorageState` | `Members` | 0 / 2 / 0 / 0 | Stored | [Id] |
| `backend/Cluster/Lobbies/LobbyRegistryState.cs:11` | `LobbyRegistryEntry` | `ServerId`, `SessionId`, `MatchSessionId`, `Listing` | 2 / 2 / 0 / 4 | Stored | [Id] |
| `backend/Cluster/Lobbies/LobbyRegistryState.cs:27` | `LobbyListing` | `IsOpen`, `LeaderUserId`, `PlayersCount`, `IsRunning`, `IsJoinable`, `MaxPlayers` | 16 / 10 / 0 / 0 | Stored | [Id] |
| `backend/Cluster/Lobbies/LobbyRegistryState.cs:55` | `OpenLobbies` | `Version`, `Entries` | 2 / 2 / 0 / 0 | - | [Id] |
| `backend/Cluster/Lobbies/LobbyRegistryState.cs:62` | `OpenLobby` | `Code`, `ServerId`, `Listing` | 3 / 3 / 0 / 2 | - | [Id] |
| `backend/Cluster/Lobbies/LobbyRegistryState.cs:72` | `LobbyRegistryState` | `Entries`, `ListingVersion` | 0 / 1 / 0 / 2 | Stored | [Id] |
| `backend/Cluster/Monitoring/ConnectedUsersLiveData.cs:15` | `ConnectedUsersLiveData` | `Slices` | 0 / 1 / 0 / 0 | Stored | [Id] |
| `backend/Cluster/Monitoring/ConnectedUsersLiveData.cs:28` | `ConnectedUserEntry` | `UserId` | 0 / 7 / 0 / 0 | Stored | [Id] |
| `backend/Cluster/Monitoring/LiveSessionsData.cs:15` | `LiveSessionsData` | `Slices` | 0 / 1 / 0 / 0 | Stored | [Id] |
| `backend/Cluster/Monitoring/LiveSessionsData.cs:23` | `LiveSessionEntry` | `Id`, `UserIds`, `CreatedAt` | 3 / 4 / 0 / 3 | Stored | [Id] |
| `backend/Cluster/Monitoring/SideEffectsLiveData.cs:9` | `SideEffectsSnapshotResponse` | `ServiceTag`, `ServiceId`, `Data` | 3 / 0 / 0 / 0 | - | [Id] |
| `backend/Cluster/Monitoring/SideEffectsLiveData.cs:17` | `SideEffectsLiveData` | `QueueCount`, `ProcessingCount`, `RetryCount`, `DeadLetterCount`, `ThroughputHistory`, `RetryEntries` | 6 / 0 / 0 / 0 | - | [Id] |
| `backend/Cluster/Monitoring/SideEffectsLiveData.cs:28` | `SideEffectsThroughputEntry` | `Timestamp`, `Processed`, `Failed` | 6 / 0 / 0 / 0 | - | [Id] |
| `backend/Cluster/Monitoring/SideEffectsLiveData.cs:36` | `SideEffectsRetryEntry` | `Id`, `TypeName`, `RetryCount`, `RetryAfter`, `CreatedAt` | 5 / 0 / 0 / 3 | - | [Id] |
| `backend/Cluster/State/ClusterFeatures.cs:131` | `ClusterFeaturesPatch` | `AcceptingConnections`, `SideEffectsEnabled`, `CoordinatorUnhealthy`, `GameFlags` | 4 / 0 / 0 / 0 | - | [Id] |
| `backend/Cluster/State/ClusterFeatures.cs:230` | `ClusterFeaturesState` | `AcceptingConnections`, `SideEffectsEnabled`, `CoordinatorUnhealthy`, `GameFlags` | 3 / 7 / 0 / 0 | Stored | [Id] |

#### backend/Infrastructure: 57

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `backend/Infrastructure/Data/State/AddressableState.cs:12` | `AddressableStateValue` | `Value`, `IsInitialized`, `UpdateDate` | 18 / 15 / 0 / 18 | Stored | [Id] |
| `backend/Infrastructure/Diagnostics/HeapSnapshot.cs:6` | `HeapSnapshotRequest` | `Deep` | 1 / 0 / 0 / 0 | - | [Id] |
| `backend/Infrastructure/Diagnostics/HeapSnapshot.cs:12` | `HeapSnapshotResponse` | `ServiceName`, `ServiceId`, `Timestamp`, `WorkingSetBytes`, `GcTotalBytes`, `Gen0SizeBytes`, `Gen1SizeBytes`, `Gen2SizeBytes`, `LohSizeBytes`, `PohSizeBytes`, `TopTypes`, `Error`, `CollectDurationMs`, `Deep` | 24 / 0 / 0 / 0 | - | [Id] |
| `backend/Infrastructure/Diagnostics/HeapSnapshot.cs:31` | `HeapTypeEntry` | `TypeName`, `Count`, `TotalBytes` | 3 / 0 / 0 / 0 | - | [Id] |
| `backend/Infrastructure/Execution/TaskScheduling/TaskBalancerOptions.cs:10` | `TaskBalancerOptions` | `EmptyDelayMs`, `NextDelayMs`, `IterationScore`, `ExceptionPenalty`, `ConcurrentTasks` | 0 / 20 / 0 / 5 | Stored |  |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs:31` | `SequencedMessage` | `Sequence`, `Payload`, `Epoch` | 3 / 14 / 0 / 0 | - | [Id] |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs:41` | `CatchUpResult` | `Messages`, `GapDetected`, `CurrentSequence`, `Epoch` | 9 / 1 / 0 / 0 | - | [Id] |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelOptions.cs:10` | `RuntimeChannelOptions` | `ObserverKeepAliveMinutes`, `CatchUpBufferSize`, `DeliveryTimeoutSeconds` | 0 / 0 / 0 / 1 | Stored |  |
| `backend/Infrastructure/Messaging/Pipes/RuntimePipeOptions.cs:9` | `RuntimePipeOptions` | `ObserverKeepAliveMinutes`, `SendTimeoutSeconds`, `SendRetryCount`, `SendRetryBaseDelayMs` | 0 / 0 / 0 / 2 | Stored |  |
| `backend/Infrastructure/Messaging/Queues/DurableQueueOptions.cs:10` | `DurableQueueOptions` | `ObserverKeepAliveMinutes` | 0 / 0 / 0 / 1 | Stored |  |
| `backend/Infrastructure/Messaging/Queues/DurableQueueSideEffect.cs:12` | `DurableQueueSideEffect` | `QueueName`, `Message`, `CorrelationId`, `MessageId` | 6 / 14 / 0 / 0 | - | [Id] |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsOptions.cs:9` | `SideEffectsOptions` | `ScanDelay`, `EmptyScanDelay`, `ConcurrentExecutions`, `MaxRetryCount`, `IncrementalRetryDelay`, `StuckCheckIntervalSeconds`, `StuckThresholdMinutes` | 0 / 7 / 0 / 7 | Stored |  |
| `backend/Infrastructure/Orleans/Transactions/TransactionAttribute.cs:31` | `TransactionRequestBase` | `Context`, `Target` | 0 / 0 / 2 / 0 | - | [Id] |
| `backend/Infrastructure/Orleans/Transactions/TransactionContext.cs:24` | `TransactionContext` | `ExceptionMessage` | 2 / 0 / 0 / 0 | - | [Id] |
| `backend/Infrastructure/Orleans/Transactions/TransactionOptions.cs:9` | `TransactionOptions` | `LockWaitSeconds`, `StuckGraceSeconds` | 0 / 6 / 0 / 4 | Newtonsoft+Stored |  |

### F. Тесты и исходники генератора: 240

#### backend/Tests: 48

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `backend/Tests/Cluster/DeploymentStateLoadTests.cs:22` | `DeploymentStateLoadTests.Marker` | `Value` | 0 / 3 / 0 / 17 | Newtonsoft |  |
| `backend/Tests/Cluster/DeploymentStateUpdateTests.cs:118` | `DeploymentStateUpdateTests.Marker` | `Id` | 0 / 1 / 0 / 3 | - |  |
| `backend/Tests/Common/MovedTypesBinderTests.cs:71` | `MovedState` | `Value` | 0 / 1 / 0 / 17 | Newtonsoft |  |
| `backend/Tests/Grains/MigrationTestGrains.cs:14` | `MigrationTestState_0` | `Value` | 0 / 0 / 0 / 17 | Stored | [Id] |
| `backend/Tests/Grains/MigrationTestGrains.cs:23` | `MigrationTestState_1` | `Value`, `Label` | 0 / 4 / 0 / 18 | Stored | [Id] |
| `backend/Tests/Grains/MigrationTestGrains.cs:89` | `MigrationTestState_2` | `Value`, `Label`, `DoubledValue` | 0 / 3 / 0 / 18 | Stored | [Id] |
| `backend/Tests/Grains/TestGrains.cs:15` | `SimpleTestState` | `Counter`, `Label` | 0 / 6 / 0 / 2 | Stored | [Id] |
| `backend/Tests/Grains/TestGrains.cs:65` | `CollectionTestState` | `Id`, `Name` | 0 / 2 / 0 / 5 | Stored | [Id] |
| `backend/Tests/Grains/TestGrains.cs:105` | `TxTestState` | `Value` | 0 / 5 / 0 / 17 | Stored | [Id] |
| `backend/Tests/Grains/TestGrains.cs:159` | `TestSideEffect` | `TargetGrainId` | 0 / 15 / 0 / 0 | - | [Id] |
| `backend/Tests/Grains/TestGrains.cs:175` | `TransactionalTestSideEffect` | `TargetGrainId` | 0 / 3 / 0 / 0 | - | [Id] |
| `backend/Tests/Grains/TestGrains.cs:188` | `FailingTestSideEffect` | `TargetGrainId`, `FailCount` | 0 / 4 / 0 / 0 | - | [Id] |
| `backend/Tests/Grains/TestGrains.cs:234` | `FailingTransactionalTestSideEffect` | `TargetGrainId`, `FailCount` | 0 / 2 / 0 / 0 | - | [Id] |
| `backend/Tests/Grains/TestGrains.cs:257` | `AlwaysFailingSideEffect` | `TrackingId` | 0 / 6 / 0 / 0 | - | [Id] |
| `backend/Tests/Grains/TestGrains.cs:346` | `EventTestAggregate` | `Id`, `Counter`, `Label` | 0 / 1 / 2 / 5 | Stored | [Id], iface:IEventStateValue |
| `backend/Tests/Grains/TestGrains.cs:357` | `CounterIncremented` | `Amount` | 0 / 23 / 0 / 0 | - |  |
| `backend/Tests/Grains/TestGrains.cs:362` | `LabelChanged` | `Label` | 0 / 2 / 0 / 1 | - |  |
| `backend/Tests/Grains/TestGrains.cs:367` | `BadEvent` | `Data` | 0 / 0 / 0 / 0 | - |  |
| `backend/Tests/Grains/TestGrains.cs:372` | `NoApplyEvent` | `Value` | 0 / 1 / 0 / 17 | - |  |
| `backend/Tests/Hosting/HostingTestWorld.cs:93` | `HostingTestWorld` | `LobbyCreationFailure` f | 0 / 1 / 0 / 0 | - |  |
| `backend/Tests/Hosting/HostingTestWorld.cs:301` | `TestLobbyEntryPolicy` | `CanEnter` f | 0 / 4 / 0 / 0 | - |  |
| `backend/Tests/Hosting/HostingTestWorld.cs:352` | `FakeLobbyCodes` | `Generator` f, `MarkStartedGate` f | 0 / 4 / 0 / 5 | - |  |
| `backend/Tests/Messaging/MessagingTestMessages.cs:8` | `TestMessage` | `Text`, `Sequence` | 0 / 92 / 0 / 0 | - | [Id] |
| `backend/Tests/Messaging/MessagingTestMessages.cs:15` | `TestRequest` | `Question` | 0 / 19 / 0 / 0 | - | [Id] |
| `backend/Tests/Messaging/MessagingTestMessages.cs:21` | `TestResponse` | `Answer` | 0 / 12 / 0 / 0 | - | [Id] |
| `backend/Tests/Protocol/TestNetworkContextUnion.cs:74` | `TestEntityPayload` | `Kind` | 0 / 0 / 0 / 0 | - |  |
| `backend/Tests/Protocol/TestNetworkContextUnion.cs:80` | `TestEventPayload` | `Value` | 0 / 0 / 0 / 17 | - |  |
| `backend/Tests/Session/SessionTestWorld.cs:71` | `TestSession` | `RunTask` f | 0 / 0 / 1 / 0 | - |  |
| `backend/Tests/Session/SessionTestWorld.cs:267` | `TestConnection` | `RunFailure` f | 0 / 1 / 0 / 0 | - |  |
| `backend/Tests/State/AddressableStateStorageTests.cs:18` | `AddressableStorageTestOptions` | `Label`, `Limit` | 0 / 4 / 0 / 1 | Stored |  |
| `backend/Tests/State/AddressableStateTests.cs:16` | `AddressableStateTests.TestConfig` | `Label`, `MaxRetries` | 0 / 6 / 0 / 1 | - |  |
| `backend/Tests/State/SideEffectsWorkerTests.cs:153` | `SlowSideEffect` | `TrackingId` | 0 / 1 / 0 / 0 | - | [Id] |
| `backend/Tests/State/StateCollectionIdempotencyTests.cs:61` | `TestCollectionValue` | `Id`, `Name` | 0 / 3 / 0 / 5 | Stored | [Id] |
| `backend/Tests/State/StorageFailFastTests.cs:142` | `StorageFailFastTests.PlainDocument` | `Id` | 0 / 1 / 0 / 3 | - |  |

#### client/core/Tests: 18

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `client/core/Tests/Editor/BehaviourTrees/Docs_BehaviourTree.cs:37` | `Docs_BehaviourTree` | `Hunger`, `SeesIntruder` | 0 / 0 / 1 / 0 | - |  |
| `client/core/Tests/Editor/Catalogues/AssetGroupRequestTests.cs:97` | `AssetGroupRequestRoots` | `First` f, `Second` f | 0 / 4 / 0 / 0 | - | static |
| `client/core/Tests/Editor/Container/ScopeCodegenFixtures.cs:52` | `ScopeCodegenSingleton` | `Instances` f | 0 / 2 / 1 / 0 | - | static |
| `client/core/Tests/Editor/Container/ScopeCodegenFixtures.cs:63` | `ScopeCodegenScoped` | `Instances` f | 0 / 2 / 1 / 0 | - | static |
| `client/core/Tests/Editor/Container/ScopeCodegenFixtures.cs:74` | `ScopeCodegenTransient` | `Instances` f | 0 / 2 / 1 / 0 | - | static |
| `client/core/Tests/Editor/Container/ScopeRuntimeFixtures.cs:11` | `ScopeRuntimeRoots` | `FirstConfig` f, `SecondConfig` f, `FirstDep` f, `SecondDep` f, `FirstAsset` f, `SecondAsset` f, `LoopDeps` f, `IsOptionalRegistered` f | 0 / 10 / 0 / 0 | - | static |
| `client/core/Tests/Editor/Scopes/ScopeLoaderFixtures.cs:14` | `ScopeLoaderProbe` | `Container` f, `Gate` f | 0 / 3 / 2 / 0 | - | static |
| `client/core/Tests/EditorTools/SerializeReferencePickerTests.cs:11` | `SerializeReferencePickerHolder` | `Value` f | 0 / 0 / 0 / 17 | Unity |  |

#### client/modules/network: 1

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `client/modules/network/Tests/Editor/PeerHostTests.cs:204` | `PeerHostTests.SignalingEnd` | `Other` f | 0 / 4 / 0 / 0 | - | internal |

#### client/platforms/itch: 1

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `client/platforms/itch/Tests/Editor/ItchSavesTests.cs:108` | `ItchSavesTests.SavedData` | `Value` f | 0 / 1 / 0 / 17 | - | [Serializable] |

#### generator sources: 172

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `client/core/Source~/ContainerGenerator/Entity/EntityAssetIndex.cs:9` | `EntityAsset` | `AssetPath` f, `HolderType` f, `ComponentTypes` f | 0 / 6 / 0 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Entity/EntityAssetIndex.cs:15` | `EntityAssetIndex` | `Assets` f, `Diagnostics` f | 0 / 0 / 0 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Graph/GraphModel.cs:5` | `GraphRegistration` | `Kind` f, `ImplementationType` f, `ServiceTypes` f, `Lifetime` f, `Origin` f, `Source` f, `Hole` f, `File` f, `Line` f, `Ordinal` f, `TypeMap` f, `ServiceMaps` f, `Location` f, `Arms` f, `Dependencies` f, `InjectMethod` f, `BranchPath` f, `ParameterTypes` f | 0 / 50 / 12 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Graph/GraphModel.cs:53` | `GraphSwitchArm` | `Discriminant` f, `ImplementationType` f, `ParameterExpression` f, `ParameterType` f | 0 / 10 / 4 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Graph/GraphModel.cs:69` | `GraphEdge` | `ParameterName` f, `ParameterType` f, `Source` f, `Method` f, `Kind` f, `Arm` f, `TargetIndex` f, `ParentRootId` f, `CollectionIndices` f, `Fallbacks` f | 0 / 26 / 0 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Graph/GraphModel.cs:85` | `GraphMethod` | `Id` f, `OriginId` f, `ParentHint` f, `ParentId` f, `ParentAssembly` f, `AssemblyName` f, `ParentUnresolved` f, `IsRoot` f, `File` f, `Line` f, `ViewType` f, `Variant` f, `Registrations` f, `Calls` f, `CallOrdinals` f, `CallTypeArguments` f, `CallAsServices` f, `CallHoles` f, `CallParameters` f, `ReturnedOrdinal` f | 0 / 50 / 0 / 3 | - | internal |
| `client/core/Source~/ContainerGenerator/Graph/GraphModel.cs:204` | `GraphDocument` | `AssemblyName` f, `Methods` f, `Diagnostics` f | 0 / 1 / 0 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Graph/GraphModel.cs:210` | `ScopeGraph` | `RootId` f, `ParentId` f, `ParentMissing` f, `Variant` f, `ViewType` f, `Registrations` f, `ConstructionOrder` f, `Diagnostics` f | 0 / 7 / 0 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Scope/ManifestHarvest.cs:10` | `ManifestHarvest` | `Source` f, `Mappings` f | 0 / 0 / 1 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Scope/ManifestHarvest.cs:14` | `ManifestHarvest.Mapping` | `HarvestName` f, `OriginalId` f | 0 / 2 / 0 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Scope/ScopePlan.cs:7` | `ScopePlan` | `RootId` f, `Namespace` f, `ClassName` f, `HintName` f, `LifetimeType` f, `LifetimeParam` f, `LifetimeField` f, `ParentType` f, `ParentParam` f, `ParentField` f, `OriginRootId` f, `ViewType` f, `NeedsRequest` f, `HasContainerInterface` f, `HasUnityGameObject` f, `HasProvides` f, `HasDiagnosticsType` f, `HasRegistrationInfo` f, `HasServiceLifetime` f, `HasRuntimeInitialize` f, `HasContainerDiagnostics` f, `ProvidesDefinition` f, `DiagnosticRegistrations` f, `CtorParams` f, `Fields` f, `Slots` f, `AlternativeGroups` f, `Markers` f, `Exports` f, `Factories` f, `Provides` f, `DisposeOrder` f, `Providers` f, `DisposableSlots` f | 0 / 0 / 18 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Scope/ScopePlan.cs:44` | `ScopePlan.CtorParam` | `Type` f, `Name` f, `Slot` f, `FieldName` f, `IsLifetime` f, `IsParent` f, `IsRequest` f, `IsAlternative` f, `AlternativeGroup` f, `IsInjections` f, `ElementType` f, `IsInstance` f, `Occurrence` f, `IsParameter` f, `IsAlternativeParameter` f, `ParameterImplementation` f, `OwnerSlot` f, `IsParentHole` f, `Edges` f | 0 / 27 / 0 / 6 | - | internal |
| `client/core/Source~/ContainerGenerator/Scope/ScopePlan.cs:72` | `ScopePlan.Field` | `Type` f, `Name` f, `Slot` f, `IsMarker` f, `IsExports` f, `IsLifetime` f, `IsDiagnostics` f | 0 / 15 / 0 / 6 | - | internal |
| `client/core/Source~/ContainerGenerator/Scope/ScopePlan.cs:82` | `ScopePlan.Slot` | `Index` f, `RegisteredField` f, `Registration` f, `ImplementationType` f, `FieldName` f, `FieldType` f, `CtorParamName` f, `InjectionsParamName` f, `InjectionsIndexName` f, `IsInjectionsRepeat` f, `CreateMethod` f, `HasField` f, `IsTransient` f, `IsHole` f, `IsAlternative` f, `IsSwitch` f, `SkipEmit` f, `IsUnity` f, `AlternativeGroup` f, `IsGroupPrimary` f, `GroupMembers` f | 0 / 30 / 0 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Scope/ScopePlan.cs:110` | `ScopePlan.Marker` | `ElementType` f, `FieldName` f, `Slots` f | 0 / 2 / 0 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Scope/ScopePlan.cs:116` | `ScopePlan.Export` | `ServiceType` f, `Slot` f, `Value` f | 0 / 3 / 0 / 17 | - | internal |
| `client/core/Source~/ContainerGenerator/Scope/ScopePlan.cs:122` | `ScopePlan.Factory` | `Slot` f, `MethodName` f, `ReturnType` f, `DiscriminantType` f, `DiscriminantName` f | 0 / 4 / 0 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Scope/ScopePlan.cs:130` | `ScopePlan.Provide` | `TargetType` f, `Slot` f | 0 / 2 / 0 / 0 | - | internal |
| `client/core/Source~/ContainerGenerator/Scope/ScopePlan.cs:136` | `ScopePlan.DiagnosticRegistration` | `ImplementationType` f, `ServiceTypes` f, `Lifetime` f, `Dependencies` f, `IsInstantiated` f, `IsExternal` f | 0 / 6 / 0 / 0 | - | internal |

### G. Контракты внешних механизмов: 80

#### Blazor: 53

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `backend/Console/Common/OperationView.razor:68` | `OperationView.razor` | `Name`, `StartCallback`, `Class` | 0 / 0 / 0 / 2 | - |  |
| `backend/Console/Common/PageHeader.razor:11` | `PageHeader.razor` | `Title` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Common/PayloadForm.razor:19` | `PayloadForm.razor` | `Payload`, `PayloadChanged` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Home/ClusterHealthWidget.razor:39` | `ClusterHealthWidget.razor` | `ServiceDiscovery`, `StartupConfig`, `RoleIcons` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Home/FeaturesStatusWidget.razor:21` | `FeaturesStatusWidget.razor` | `ClusterFeatures`, `GameFlags` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Home/HomeInfrastructureSection.razor:58` | `HomeInfrastructureSection.razor` | `ProjectCards` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Home/StartupStatusTile.razor:20` | `StartupStatusTile.razor` | `ClusterParticipantContext` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Infrastructure/AuditLog/AuditLog.razor:76` | `AuditLog.razor` | `AuditStorage`, `ToastService` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Infrastructure/Debug/StateExplorer.razor:107` | `StateExplorer.razor` | `Storage`, `ToastService`, `Clipboard` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Infrastructure/Deploy/Deploy.razor:49` | `Deploy.razor` | `DeployContext`, `DeployCleanup`, `ToastService` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Infrastructure/Discovery/Discovery.razor:78` | `Discovery.razor` | `ServiceDiscovery`, `Orleans`, `DeployContext`, `StartupConfig`, `RoleIcons`, `ToastService` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Infrastructure/Features/FeatureToggle.razor:29` | `FeatureToggle.razor` | `Label`, `Description`, `Value`, `OnToggle`, `Saving` | 0 / 0 / 0 / 18 | - |  |
| `backend/Console/Infrastructure/Features/Features.razor:63` | `Features.razor` | `ClusterFeatures`, `ToastService`, `AuditLog`, `GameFlags` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Infrastructure/Monitoring/Monitoring.razor:88` | `Monitoring.razor` | `Collector`, `Storage`, `JS`, `ToastService` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Infrastructure/Options/DurableQueueOptionsEditor.razor:20` | `DurableQueueOptionsEditor.razor` | `Value` | 0 / 0 / 0 / 17 | - |  |
| `backend/Console/Infrastructure/Options/InfrastructureOptions.razor:32` | `InfrastructureOptions.razor` | `SideEffectsConfig`, `DurableQueueConfig`, `TaskBalancerConfig`, `TransactionConfig`, `ToastService`, `AuditLog` | 0 / 0 / 0 / 0 | - |  |
| `backend/Console/Infrastructure/Options/SideEffectsOptionsEditor.razor:44` | `SideEffectsOptionsEditor.razor` | `Value` | 0 / 0 / 0 / 17 | - |  |
| `backend/Console/Infrastructure/Options/TaskBalancerOptionsEditor.razor:44` | `TaskBalancerOptionsEditor.razor` | `Value` | 0 / 0 / 0 / 17 | - |  |
| `backend/Console/Infrastructure/Options/TransactionOptionsEditor.razor:26` | `TransactionOptionsEditor.razor` | `Value` | 0 / 0 / 0 / 17 | - |  |
| `backend/Console/Infrastructure/SideEffects/SideEffectsMonitor.razor:199` | `SideEffectsMonitor.razor` | `Aggregator`, `Storage`, `ToastService` | 0 / 0 / 0 / 0 | - |  |

#### attribute named argument: 1

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `client/core/Runtime/Common/Attributes/DropdownAttribute.cs:18` | `DropdownAttribute` | `VisibleIf` | 0 / 0 / 0 / 0 | - |  |

#### foreign interface or base: 6

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `backend/Infrastructure/Orleans/Transactions/TransactionAttribute.cs:149` | `TransactionResponse` | `Result`, `Exception` | 0 / 0 / 0 / 0 | - | body, foreign-base:Response, override |
| `client/core/Editor/Tools/ContainerGraph/ContainerGraphToolbar.cs:14` | `ContainerGraphRefreshButton` | `containerWindow` | 0 / 0 / 0 / 0 | - | foreign-iface:IAccessContainerWindow |
| `client/core/Editor/Tools/ContainerGraph/ContainerGraphToolbar.cs:35` | `ContainerGraphStatusLabel` | `containerWindow` | 0 / 0 / 0 / 0 | - | foreign-iface:IAccessContainerWindow |
| `client/core/Editor/Tools/LifetimeGraph/LifetimeGraphToolbar.cs:14` | `LifetimeGraphClearButton` | `containerWindow` | 0 / 0 / 0 / 0 | - | foreign-iface:IAccessContainerWindow |
| `client/core/Editor/Tools/LifetimeGraph/LifetimeGraphToolbar.cs:35` | `LifetimeGraphStatusLabel` | `containerWindow` | 0 / 0 / 0 / 0 | - | foreign-iface:IAccessContainerWindow |

#### native interop struct: 14

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `client/modules/network/Runtime/Sockets/WebRtc/NativeWebRtcApi.cs:41` | `NativeWebRtcApi.Configuration` | `IceServers` f, `IceServersCount` f, `ProxyServer` f, `BindAddress` f, `CertificateType` f, `IceTransportPolicy` f, `EnableIceTcp` f, `EnableIceUdpMux` f, `DisableAutoNegotiation` f, `ForceMediaTransport` f, `PortRangeBegin` f, `PortRangeEnd` f, `Mtu` f, `MaxMessageSize` f | 2 / 0 / 0 / 0 | - | internal |

#### options: IOptions: 6

| file:line | type | members | ext prod / tests / self / by name | serializer | notes |
|---|---|---|---|---|---|
| `backend/Cluster/Coordination/ClusterStartupConfig.cs:18` | `ClusterStartupOptions` | `RequiredServices` | 1 / 2 / 0 / 0 | - | options: IOptions |
| `backend/Cluster/Deploy/CoordinatorHealthOptions.cs:6` | `CoordinatorHealthOptions` | `StaleThreshold`, `HealthPollInterval`, `HeartbeatInterval`, `MaxConsecutiveHeartbeatFailures` | 0 / 1 / 0 / 0 | - | options: IOptions |
| `backend/Cluster/Discovery/ServiceDiscovery.cs:27` | `ServiceDiscoveryOptions` | `PublishGameServerUrl` | 1 / 0 / 0 / 0 | - | options: IOptions |

### Вызовы сериализаторов с типом, неизвестным статически

- `JsonUtility client/modules/settings/Runtime/Settings.cs:43`
- `JsonUtility client/modules/settings/Runtime/Settings.cs:75`
- `JsonUtility client/platforms/itch/Runtime/ItchSaves.cs:107`
- `JsonUtility client/platforms/itch/Runtime/ItchSaves.cs:86`
- `Newtonsoft backend/Common/Extensions/JsonUtils.cs:19`
- `Newtonsoft backend/Common/Extensions/JsonUtils.cs:24`
- `Newtonsoft backend/Common/Extensions/PostgresJsonbConverter.cs:26`
- `Newtonsoft backend/Common/Extensions/PostgresJsonbConverter.cs:29`
- `Newtonsoft backend/Common/Extensions/PostgresJsonbConverter.cs:42`
- `Newtonsoft backend/Infrastructure/Orleans/State/StateSerializer.cs:56`
- `Newtonsoft backend/Infrastructure/Orleans/State/StateSerializer.cs:61`
- `Newtonsoft backend/Infrastructure/Orleans/State/StateSerializer.cs:66`
- `Newtonsoft backend/Infrastructure/Orleans/State/StateSerializer.cs:71`
- `Newtonsoft backend/Infrastructure/Orleans/State/StateSerializer.cs:76`
- `Newtonsoft backend/Tests/Cluster/DeployCleanupTests.cs:219`
- `Newtonsoft backend/Tests/Fixtures/InMemoryChannelClient.cs:52`
- `Newtonsoft backend/Tests/Fixtures/InMemoryChannelClient.cs:53`
- `Newtonsoft backend/Tests/Fixtures/InMemoryChannelClient.cs:54`
- `Newtonsoft backend/Tests/Fixtures/InMemoryChannelClient.cs:55`
- `Newtonsoft backend/Tests/Fixtures/InMemoryChannelClient.cs:56`

### `set` в интерфейсах (не считается, но держит сеттер реализации)

- `backend/Infrastructure/Orleans/State/StateInterfaces.cs:12 Id`
- `client/core/Editor/Catalogues/Shared/ICatalogGroupDefinition.cs:7 Address`

### Исключено как сгенерированное

- `client/core/Tests/EditorTools/Fixtures/Generated/ShopBindings.g.cs`


## Финальный подсчёт без F2

Коммит `73dda7d`, ветка `public_setters`. Счётчик: `count_public_members.cs`, область 1022 файла. Тесты на этом коммите: backend 836 прошло, common 92 прошло, Unity EditMode 394: 390 прошло, 4 пропущено, упало 0.

| Группа | Что это | Было: всего (сеттеры / поля / типы) | Стало: всего (сеттеры / поля / типы) |
|---|---|---:|---:|
| A | runtime, настоящие нарушения | 58 (55 / 3 / 20) | 0 |
| B | editor, настоящие нарушения | 118 (29 / 89 / 25) | 0 |
| C | `[MemoryPackable]`, разрешено правилом | 99 (99 / 0 / 44) | 99 (99 / 0 / 44) |
| D | сериализуемые поля клиента | 85 (4 / 81 / 15) | 61 (0 / 61 / 7) |
| E | Orleans-состояния и JSON-модели backend | 110 (110 / 0 / 33) | 0 |
| F | тесты и исходники генератора | 240 (44 / 196 / 63) | 176 (2 / 174 / 23) |
| G | контракты внешних механизмов | 80 (66 / 14 / 30) | 22 (22 / 0 / 10) |
| | **Всего** | **790 (407 / 383)** | **358 (123 / 235)** |

Без учёта по-прежнему: `internal` поля и сеттеры 0, `set` в интерфейсах 0 (было 2), публичные члены `private`-вложенных типов 135 (выборочно проверено 7 таких `{ get; set; }`: все во вложенных `private`-классах).

### Что осталось поимённо

Исключения `CODE_STYLE_FULL.md`: (1) `[MemoryPackable]`, (2) `[Serializable]` без логики, который читает и пишет только `JsonUtility`, (3) Blazor `[Parameter]`, (4) именованный аргумент атрибута, (5) `override` публичного свойства чужого типа.

**A, B, E:** пусто.

**D, 61 член, 7 типов.** Файлы всех семи типов за задачу не менялись (`git diff ea8ab4c^ HEAD` по трём файлам пуст). Везде `[Serializable]`. Других сериализаторов у этих типов нет.

| Тип | Членов | Сериализатор | Исключение | Итог |
|---|---:|---|---|---|
| `HierarchyBindingsRequest` | 9 | `JsonUtility`, внутри очереди, `SessionState` | (2) | покрыт |
| `ProfilerFramesData` | 12 | `JsonUtility.ToJson`, `ProfilerFramesStorage:19`; чтения в C# нет | (2) | покрыт |
| `ProfilerFrameData` | 9 | то же, внутри `ProfilerFramesData` | (2) | покрыт |
| `ProfilerSampleData` | 6 | то же, внутри `ProfilerFrameData` | (2) | покрыт |
| `ProfilerTraceData` | 11 | `JsonUtility`, `ProfilerTraceStorage:90,135` | (2) | покрыт |
| `ProfilerSpanData` | 13 | то же, внутри `ProfilerTraceData` | (2) | покрыт с оговоркой: есть вычисляемый геттер `EndMs => StartMs + DurationMs`. Состояние он не меняет, `JsonUtility` его не видит |
| `HierarchyBindingsQueue` | 1 (`Requests`) | `JsonUtility`, `SessionState` | нет | **нарушение** |

`HierarchyBindingsQueue` не отвечает условию «без логики»: в типе статические `Enqueue`, `Load`, `IsEmpty`, `HasData`, `Clear`, `Read`. Снаружи к `Requests` никто не обращается (0 внешних обращений), так что публичным поле быть не обязано.

**F без генератора, 4 члена.**

| Член | Исключение | Итог |
|---|---|---|
| `backend/Tests/Protocol/TestNetworkContextUnion.cs:74` `TestEntityPayload.Kind` | (1), тип `[MemoryPackable]` | покрыт |
| `backend/Tests/Protocol/TestNetworkContextUnion.cs:80` `TestEventPayload.Value` | (1), тип `[MemoryPackable]` | покрыт |
| `client/platforms/itch/Tests/Editor/ItchSavesTests.cs:108` `ItchSavesTests.SavedData.Value` | (2), `[Serializable]`, без логики, `ItchSaves` пишет и читает через `JsonUtility` | покрыт |
| `client/modules/network/Tests/Editor/PeerHostTests.cs:204` `PeerHostTests.SignalingEnd.Other` | нет | **нарушение**, известное: ждёт чужую правку `WebRtcPeerSocketFailureTests.cs` |

**F, исходники генератора, 172 члена, 19 типов** (`client/core/Source~/ContainerGenerator`): исключения нет, **нарушение**, известное: ждёт ветку `file_splits`. Поимённо: `final.md` счётчика и раздел F выше.

**G, 22 члена, 10 типов.**

| Члены | Исключение | Итог |
|---|---|---|
| `OperationView.razor`: `Name`, `StartCallback`, `Class` | (3) | покрыт |
| `PageHeader.razor`: `Title` | (3) | покрыт |
| `PayloadForm.razor`: `Payload`, `PayloadChanged` | (3) | покрыт |
| `FeatureToggle.razor`: `Label`, `Description`, `Value`, `OnToggle`, `Saving` | (3) | покрыт |
| `DurableQueueOptionsEditor.razor`: `Value`, `ValueChanged` | (3) | покрыт |
| `SideEffectsOptionsEditor.razor`: `Value`, `ValueChanged` | (3) | покрыт |
| `TaskBalancerOptionsEditor.razor`: `Value`, `ValueChanged` | (3) | покрыт |
| `TransactionOptionsEditor.razor`: `Value`, `ValueChanged` | (3) | покрыт |
| `DropdownAttribute.VisibleIf` | (4); именованно используется в `storytale-workshop` (`LevelMock.cs`), читает `DropdownDrawer` | покрыт |
| `TransactionResponse.Result`, `TransactionResponse.Exception` | (5), `override` свойств `Response` из Orleans | покрыт |

Все 19 членов razor проверены скриптом: у каждого есть `[Parameter]`.

### Итог: нарушения вне исключений

1. `PeerHostTests.SignalingEnd.Other`, известное.
2. 172 члена генератора, известное.
3. `HierarchyBindingsQueue.Requests`, **новое**: тип записан в D3, но логику содержит.
