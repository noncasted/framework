namespace Internal
{
    public static class ServiceLoadResultExtensions
    {
        public static T Resolve<T>(this ILoadedScope loadResult)
        {
            return loadResult.Container.Resolve<T>();
        }
    }
}
