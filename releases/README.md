# Релизы framework

Игра стоит на релизе, а не на случайном коммите, и получает список того, что менять у себя. Источник правды: тег `vX.Y.Z` и файл
`X.Y.Z.md` рядом. Обновляет игру скилл `update-framework`, выпускает релиз скилл `release-framework`.

| Версия | Дата | Содержание | Что делать игре |
|--------|------|------------|-----------------|
| [0.1.0](0.1.0.md) | 2026-11-02 | Аудит клиента, разрез больших файлов, нет публичных сеттеров | Да: API клиента, тесты бэкенда |
| [0.2.0](0.2.0.md) | 2026-11-02 | Исправления каналов, транзакций, лобби и закрытия соединений, разрез бэкенда, копии `control` | Да: тесты бэкенда, `.gitignore`, `control` |
| [0.2.1](0.2.1.md) | 2026-11-03 | Скилл `media-cleanup`, команды `update-framework` в zsh | Да: ссылка на скилл `media-cleanup` |
| [0.2.2](0.2.2.md) | 2026-11-03 | `deploy-client`: аргумент `--notest` | Нет |
| [0.3.0](0.3.0.md) | 2026-11-03 | Телеметрия клиентов (необязательно), стадии трейса запуска | Да: `GameProfiler.Stage` |
| [0.4.0](0.4.0.md) | 2026-11-04 | Телеметрия: кадр, WebGL, страница, прошлая сессия; сеть без аллокаций; экранные канвасы; стрип WebGL; `deploy-client` параллельно, `device-lab` | Да: `IScreenCanvasCamera`, шрифт `NetworkOptions`, API сети, стрип, `deploy-client.json`, `ILobbyMatches.Create` |
| [0.5.0](0.5.0.md) | 2026-11-06 | Модули `resolution` и `framerate`, `Internal.TestTools`, пакеты бэкенда, WebRTC с каналами и `PeerMesh`, спрайты из модулей, `audit-swarm` | Да: модули из pixel-swarm, `Aspire.AppHost.Sdk`, `InjectGameObject` |
| [0.5.1](0.5.1.md) | 2026-11-07 | `deploy-client`: аргумент `--platforms=` | Нет |
| [0.6.0](0.6.0.md) | 2026-11-09 | Консоль: онлайн по уникальным пользователям, Home без инфраструктурных карточек; попытки подключения к Postgres; offer пира в `PeerMesh`; хуки `deploy-client` | Да: `HomeInfrastructureSection` -> `HomeClusterSection`, `AddOnlineTracker`, `OnlineWidget`, тесты состояний |
| [0.7.0](0.7.0.md) | 2026-11-15 | Аудит бэкенда: контракт соединения, `Reserve` с причиной, состояния кластера через методы, метры у фич, `AddConsoleInfrastructure`; модуль `match-metrics`; окно реконнекта хоста; Regenerate All с валидатором | Да: шлюзы и `Closed`, `Reserve`, `OperationProgress`, `ServiceDefaults`, `RequeueStuck`, консоль, тесты бэкенда, bindings |
| [0.8.0](0.8.0.md) | 2026-11-16 | Контент на AssetBundles движка вместо Addressables (`ContentBundle`, `IContentLoader`, `ContentReference` / `ContentScene`); ядро без uGUI и `Framework.Shared`; телеметрия по HTTP; пакет `layout`; `CINGR017` | Да: контент и манифест, asmdef `Internal.UGUI` / `Internal.Layout` / `Internal.Telemetry.Socket`, шаблоны WebGL и nginx, `brotli` |
| [0.9.0](0.9.0.md) | 2026-11-17 | Контент: бандлы вне сборки (`build.Exclude`), общие бандлы игры (`IContentSharedBundles`), ошибка грязной сцены; `moves` в `update-override-packages` | Да: `BeforeContentBuild(ContentBuildContext)`, свой синк общих бандлов -> `IContentSharedBundles` |
| [0.9.1](0.9.1.md) | 2026-11-17 | `deploy-client`: проверка контента читает каталог из распакованного `data.unity3d` | Нет |
| [0.9.2](0.9.2.md) | 2026-11-17 | Раскладка контента: ассет, нужный ассетам и сценам одного бандла и другому бандлу, - дубль, а не скрытая зависимость от бандла со сценами | Да: пересинк общих бандлов, снять ручные корни preloaded-бандла |
| [0.9.3](0.9.3.md) | 2026-11-17 | Исправления аудитов бэкенда и клиента 2026-11-16: грязная запись вне транзакции - ошибка, `SharedViewableProperty<T>`, лобби упавшего сервера, генераторы каталогов падают на сломанном входе, сеть P2P, мёртвый код | Да: `DbLookup` -> `SideEffectTables`, подмены `IClusterParticipantContext`, `Game.Hosting.Collection`, `IsJoinable`, `TraceExtensions.Start`, `MockEntry.Process` -> `UniTask` |
| [0.10.0](0.10.0.md) | 2026-11-17 | Startup trace хранит backend trace id сокета, консоль рисует трейс бэкенда из Aspire dashboard; `GameProfiler.Background`; веб-загрузка контента только в WebGL-плеере | Да: `AcceptStartup(trace, backendTrace)` |
| [0.11.0](0.11.0.md) | 2026-11-18 | Код контента разложен по ролям, новые имена (`IContentLoadersCollection`, `ContentEntryPoint`, дистрибуции); фаза `IContentLayoutStep`; `update-framework` предлагает непривязанные общие скиллы | Да: `IContentBundles` -> `IContentLoadersCollection`, `ScopeLoadOptions.ServiceScene`, `ContentEntryPoint.RegisterMain` |
| [0.12.0](0.12.0.md) | 2026-11-20 | Оверлей пинга в своей сборке `Internal.Network.Overlay`: `Internal.Network` без uGUI / TMP | Да: `NetworkOverlayOptions`, `AddNetworkLatencyOverlay` в скоупе сессии |

## Версии

`0.MINOR.PATCH`, пока API меняется свободно:

- `MINOR`: игре есть что делать (API, ассет, настройка, новая обязательная зависимость, поведение, на которое опираются).
- `PATCH`: обновление без действий, поменять только указатель.

## Как обновить игру

Если скилл уже привязан: `/update-framework` (или `/update-framework 0.2.0`, `/update-framework main`).

В игре, чей указатель старше скилла, из корня проекта:

```
git -C tools/framework fetch --tags origin
git -C tools/framework show v0.1.0:.claude/skills/update-framework/SKILL.md
```

Это инструкция для агента: дать ему этот вывод и попросить выполнить. Шаг 6 скилла создаст ссылку.

## Как записывать изменения

Изменение, из-за которого игре надо что-то сделать, в том же коммите дописывает шаг в [unreleased.md](unreleased.md). Для файла включён
`merge=union`, параллельные ветки не конфликтуют. `BREAKING:` в сообщении коммита остаётся (правило 8), но игры читают шаги.

## Формат файла релиза

```markdown
---
version: 0.2.0
date: 2026-11-10
base: v0.1.0
---
# 0.2.0

Одной-двумя фразами: что вошло и зачем обновляться.

## Состояние игр
Таблица: игра, указатель, что осталось (из поиска по HEAD игры), что собрано реально.

## Шаги
### 1. Заголовок
Кого касается: клиент / бэкенд, какие игры.
Поиск: `git grep -nE '...' -- 'client/Assets/*.cs'`
Сделать: было -> стало, ссылка на пример.

## Проверить поведение
Что изменилось без ошибки компиляции и как это заметить.

## Без действий
Что вошло и ничего не требует, по одной строке.
```

`base` читает `status.sh`: коммит или тег перед первым коммитом релиза. Если указатель игры старше него, файл релиза говорит, что читать
дополнительно. Поиск запускается из корня игры.
