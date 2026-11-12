## Консоль: статистика онлайна, Home без инфраструктурных карточек

### Что сделано
- Учёт онлайна во фреймворке: `OnlineTracker` на координаторе (`AddOnlineTracker`) подписан на `ConnectedUsersLiveData`, считает различные `UserId` (две вкладки / два гейтвея = 1); по часам (48) и дням (30): пик одновременных и уникальные пользователи; уникальные за 7 / 30 дней. История в `state_online_days` (не deploy-scoped), живые данные в `OnlineStatsLiveData`.
- Консоль: страница `/online`, виджет `OnlineWidget` для Home (Hours / Days), секция `HomeClusterSection` (Features, Client Telemetry). `HomeInfrastructureSection` удалён (BREAKING), все карточки — на `/infrastructure`.
- pixel-swarm: Home переведён на новые компоненты, `ConnectedUsersWidget` удалён, `AddUserCollection` убран из роли Console (не закоммичено в игре на момент закрытия).
- Коммит `345586c8`, релиз v0.6.0 (`4166a635`, шаг игр в `releases/0.6.0.md`).

### Ключевые файлы
- `backend/Cluster/Monitoring/Online/` — `OnlineTracker`, `OnlineCounter`, `OnlineDayState`, `OnlineStatsLiveData`, `OnlineExtensions`
- `backend/Console/Home/OnlineWidget.razor`, `HomeClusterSection.razor`
- `backend/Console/Infrastructure/Online/OnlineStats.razor`, `InfrastructureDashboard.razor`
- `backend/Tests/Cluster/Online/`

### Заметки
- Часы и дни — в UTC. Трекер один на деплой (координатор); пока он перезапускается, заходы не учитываются.
- Строки дней старше 30 суток (со списками id) не удаляются — при большом DAU нужна очистка.
- При нулевых данных `BbAreaChart` виджета ничего не рисует (даже осей).
- Без замены `HomeInfrastructureSection` сборка игры не падает (только RZ10012), секция просто пустая.
