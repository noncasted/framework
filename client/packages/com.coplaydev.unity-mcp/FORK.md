# FORK: com.coplaydev.unity-mcp 10.3.0

Base: `MCPForUnity` of github.com/CoplayDev/unity-mcp at tag v10.3.0 (aa5fc638).

## Patches

| File | Why |
|---|---|
| `Editor/Services/Transport/Transports/StdioBridgeHost.cs` | CoreCLR editor: `OperationCanceledException` on bridge shutdown is a normal stop, not an error |
| `Editor/Tools/ExecuteCode.cs` | CoreCLR editor refuses a second assembly with the same name: unique `MCPDynamic_<guid>` per compile |
| `Editor/Setup/SetupWindowService.cs` | the setup window does not open by itself on an editor start, only from the menu `Window/MCP for Unity/Local Setup Window` |
