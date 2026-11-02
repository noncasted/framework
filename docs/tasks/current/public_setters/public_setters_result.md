# public_setters: результат

## Статус
Не закрыта: F2, `SignalingEnd.Other` и P6 ждут внешних условий (см. `_progress.md`).

Подсчёт на шаге 15: 790 -> 358; после шага 15a D3 без `HierarchyBindingsQueue.Requests`. Подробно: `public_setters_inventory.md`, раздел «Финальный подсчёт без F2».

Кандидаты на удаление по P13, не трогались: `ObserverData.UpdateDate`, `TransactionContext.ExceptionMessage`.

Нестабильный тест вне задачи: `WebRtcPeerSocketTests.Connect_NoAnswer_FailsInTimeout`.

## Изменённые файлы
| Файл | Изменение | Коммит |
|------|-----------|--------|
| A1, 21 файл: `common`, `shared`, `backend` | `ViewableDictionary.Set`, `SetHandler`, `LifetimeTracker.Install`, конструктор `SessionData`, `FileLoggerOptions` на `init`, `ObserverData`. BREAKING | `ea8ab4c` |
| A2, 22 файла: `client/core/Runtime` | options только для чтения, `OptionsTab` через `SerializedProperty`, `Set*`-методы статики, `ButtonSounds.Bind`, `UpdaterSample`. BREAKING | `d776a42` |
| A3, 3 файла: модули | `GamepadState` через `Set*`, `ClientOutdatedNoticeStyle` через конструктор. BREAKING | `a844b37` |
| `CODE_STYLE_FULL.md` | полный список исключений, `init` недоступен в Unity | `ad5f411` |
| B1, 22 файла: модели каталогов | конструкторы и intent-методы, тесты `userData`. BREAKING | `0f14b31` |
| B2, 8 файлов: инструменты редактора | конструкторы, `SetEnabled`. BREAKING | `2ccd844` |
| D1, 6 файлов: `SettingsSave` | `private set`, `Set*Volume`, тесты формата. BREAKING, игры пока не задеты | `afe99e8` |
| D2, 20 файлов: записи каталогов | `[SerializeField] private` + `FormerlySerializedAs`, константы ключей. BREAKING | `96bb8ff` |
| E1, 13 файлов: сообщения Orleans | `init`, `private set`, методы, тесты round-trip. BREAKING | `c739b68` |
| E2, 28 файлов: stored-состояния, редакторы консоли | `init`, `private set` + `[JsonProperty]`, `record`, тесты формата. BREAKING | `19ba916` |
| G, 25 файлов: `[Inject]`, тулбары, options, interop | `private`, явная реализация интерфейса, `init`, конструктор. BREAKING | `0bf4243` |
| F1, 33 файла: тесты | конструкторы, `Of`, методы; детерминированный `WaitTrue...` | `7c04dbe` |
| `IEventStateValue.Id`, 5 файлов | `{ get; }` в интерфейсе, `SetIdFromStream` удалён. BREAKING | `4d39a49` |
| `EventStorage`, 4 файла | чтение снимка бросает, если `Id` не восстановился | `73dda7d` |
| `HierarchyBindingsQueue`, 3 файла | `[SerializeField] private _requests`, уточнение исключения в `CODE_STYLE_FULL.md` | `2395e8e` |
