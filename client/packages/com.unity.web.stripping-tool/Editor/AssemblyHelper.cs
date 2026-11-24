using System;
using System.Reflection;
#if UNITY_6000_5_OR_NEWER
using UnityEngine.Assemblies;
#endif

namespace Unity.Web.Stripping.Editor
{
    static class AssemblyHelper
    {
        internal static Assembly FindLoadedAssembly(string assemblyName)
        {
#if UNITY_6000_5_OR_NEWER
            var assemblies = CurrentAssemblies.GetLoadedAssemblies();
#else
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
#endif
            foreach (var assembly in assemblies)
            {
                if (assembly.GetName().Name == assemblyName)
                    return assembly;
            }
            return null;
        }
    }
}
