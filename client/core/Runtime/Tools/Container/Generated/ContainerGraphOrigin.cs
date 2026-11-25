namespace Internal
{
    public enum ContainerGraphOrigin
    {
        ConstructedType = 0,
        PrefabInstance = 1,
        PrefabAsset = 2,
        InstanceHole = 3,
        ParameterHole = 4,
        SwitchFactory = 5,
        Alternative = 6,
        InjectExisting = 7,
        ExternalInstaller = 8,
        SceneServices = 9,
    }
}