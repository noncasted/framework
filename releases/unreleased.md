# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Bindings: класс уже сгенерирован в другой папке

Кого касается: клиент, игры с префабами или сценами, чьи `*Bindings.g.cs` лежат не в `Generated` той сборки, куда их сейчас
определяет генератор (префаб перенесён в папку другой сборки, файл bindings перенесён руками).

Раньше генератор молча писал второй файл того же класса (CS0101 / два класса с одним именем), теперь отказывает с ошибкой
`... is already generated into <файл>, and this object resolves to <новый путь>`, а Regenerate All падает с
`N bindings failed to regenerate`.

Проверка: `Tools/Framework/Codegen/Regenerate All` (`-executeMethod Internal.RegenerateAll.Run`), затем
`grep -n "is already generated into" client/Logs/*.log` (или лог редактора). На каждую строку: вернуть ассет под сборку файла
(`git mv` вместе с `.meta`) либо перенести файл `*.g.cs` с его `.meta` по пути из ошибки.

Заодно Regenerate All заново привязывает bindings с пустой ссылкой (`{fileID: 0}`), у которых код не менялся: в логе
предупреждение `holds N empty references: binding again`, префаб пересохраняется. Действий не требует, изменённые префабы закоммитить.

### Bindings: Regenerate All заканчивается валидатором

Кого касается: клиент, все игры с bindings.

После прохода привязки Regenerate All прогоняет `HierarchyBindingsValidator` по всем префабам с bindings и загруженным сценам.
Раньше валидатор вызывался только из `RegeneratePrefabs` и из меню, поэтому старые находки (вариант убрал компонент базы, пустая
ссылка, устаревшая структура, bindings не в списке view) теперь появятся ошибками в консоли; последняя строка
`Regenerate All: N prefabs and M scenes validated in T ms, E validation errors` - ошибка при `E > 0`.

Проверка: Regenerate All, в batchmode или без фокуса редактора затем `HierarchyBindingsGenerator.PumpPending()` до `Idle.`;
`grep -n "HierarchyBindingsValidator\|validation errors" <лог редактора>`. Каждую находку исправить, как сказано в её тексте
(Regenerate в ассете, Scan на view, вернуть компонент в варианте). Скрипты игры, которые ждут конца Regenerate All по
`HierarchyBindingsGenerator.HasPending()`, учитывают, что он остаётся `true` до этой проверки.
