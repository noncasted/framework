# Cluster

Service roles are strings: the project declares its `ServiceTag`s and the required set
(`ConfigureRequiredServices`); a game server calls `PublishGameServerUrl`. Game configs: `AddAddressableState` +
an `ICoordinatorSetupCompleted` with `ConfigsInitializer.Init`. Game switches: `IClusterFeatures.SetGameFlag` /
`IsGameFlagEnabled`. A replaced coordinator leaves its deploy (`IDeployContext.Leave()`; a host follows the deploy id pipe only to a deploy
created after its own, `DeployState.CreatedAt`): what runs on `DeployLifetime`
stops there (`OnlineTracker`, channel listeners); the rows of a deploy are written through `DeploymentStateRow.Change`.
A feature registers its own meter and trace source (`AddBackendMeter`, `AddTraceSource`) in its `Add*`.
