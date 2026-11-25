# Console

A Razor class library: the project keeps its home page and game pages in its own assembly (add both
assemblies to the router), sets role icons (`ServiceRoleIcons`), registers game flags (`AddConsoleGameFlag`) and
extra infrastructure cards (`AddConsoleInfrastructureCard`, shown on `/infrastructure`), puts `HomeClusterSection` (Features,
Client Telemetry) and `OnlineWidget` (online of the coordinator's `AddOnlineTracker`, page `/online`) on its home, and adds `tools/framework/backend/Console` to the
Tailwind content paths. The `app.css` of a game is committed and built by hand, so no build of the framework refreshes it: a page
of the framework uses only the classes that are in the `app.css` of every game. The infrastructure pages need
`AddConsoleInfrastructure(ConsoleInfrastructureOptions)` (`CoordinatorConfigFolder`: the folder of the coordinator configs, no
Write JSON button without it) and the table of the audit log, created by the deploy setup with `AuditLogTable.Create(connection)`.
