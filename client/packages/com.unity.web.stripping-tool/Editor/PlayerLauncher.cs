using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Unity.Web.Stripping.Editor
{
    // NOTE: requires Web Build Support module for SimpleWebServer. Functions that access SimpleWebServer
    // throw NotSupportedException if called when Web Build Support module is not present.
    class PlayerLauncher
    {
        // NOTE: UnityEditor.WebGL.Browser is IDisposable but we don't take that into consideration right now;
        // disposability is used to close the browser process but we have no need for it.
        internal object m_Browser;
        static WeakReference<MethodInfo> s_CreateMethodRef;
        static WeakReference<MethodInfo> s_LaunchMethodRef;
        static WeakReference<MethodInfo> s_QuitMethodRef;

        internal static MethodInfo s_CreateMethod => GetCachedMethod(ref s_CreateMethodRef, nameof(s_CreateMethod));
        internal static MethodInfo s_LaunchMethod => GetCachedMethod(ref s_LaunchMethodRef, nameof(s_LaunchMethod));
        internal static MethodInfo s_QuitMethod => GetCachedMethod(ref s_QuitMethodRef, nameof(s_QuitMethod));

        public string BuildPath { get; private set; }
        public string ClientUrl { get; private set; }

        public bool IsServing =>
            SimpleWebServerHelper.s_IsRunning && !string.IsNullOrEmpty(BuildPath) && !string.IsNullOrEmpty(ClientUrl);

        public void OpenInPreferredBrowser(string query = "")
        {
            if (string.IsNullOrEmpty(ClientUrl))
                return;

            var createMethod = s_CreateMethod;
            var launchMethod = s_LaunchMethod;
            if (createMethod == null || launchMethod == null)
                return;

            // if configurable browser is not worth of the reflection shenanigans, could simply do
            //Application.OpenURL(ClientUrl);


            // Append search params to client url
            var url = ClientUrl;
            if (!string.IsNullOrEmpty(query))
            {
                // Workaround: We need to manually add "" around the url
                // because of a bug in the Unity Browser class.
                url = $"\"{url}?{query}\"";
            }

            if (createMethod.GetParameters().Length == 5)
            {
                m_Browser = createMethod.Invoke(
                    null,
                    new object[]
                    {
                        0, // UnityEditor.WebGLClientPlatform.Desktop
                        EditorUserBuildSettings.webGLClientBrowserType,
                        EditorUserBuildSettings.webGLClientBrowserPath,
                        url,
                        null // WebProxy
                    }
                );
            }
            else if (createMethod.GetParameters().Length == 6)
            {
                // Since Unity 6000.7.0a5 this expects 6 parameters
                m_Browser = createMethod.Invoke(
                    null,
                    new object[]
                    {
                        0, // UnityEditor.WebGLClientPlatform.Desktop
                        EditorUserBuildSettings.webGLClientBrowserType,
                        EditorUserBuildSettings.webGLClientBrowserPath,
                        url,
                        null, // WebProxy,
                        false // forAutomation
                    }
                );
            }
            else
            {
                throw new Exception($"UnityEditor.WebGL.Browser::Create() expects unsupported number of parameters: {createMethod.GetParameters().Length}. Please update the package.");
            }
            launchMethod.Invoke(m_Browser, null);
        }

        public string ServeBuild(string buildPath)
        {
            string clientHostname = "localhost";
            string acceptedHostname = "localhost";
            // NOTE: Uncomment to enable sharing in local network
            // Use the LAN IP address for platforms running on machines separate from host
            //if (EditorUserBuildSettings.webGLClientPlatform != WebGLClientPlatform.Desktop)
            //{
            //    clientHostname = SimpleWebServer.GetLocalIPAddress();
            //    acceptedHostname = "+";
            //}

            StopServing();

            BuildPath = buildPath;
            int port = SimpleWebServerHelper.GetUnusedPort();
            SimpleWebServerHelper.Start(BuildPath, $"http://{acceptedHostname}:{port}/");
            ClientUrl = $"http://{clientHostname}:{port}/";
            return ClientUrl;
        }

        public void StopServing()
        {
            if (SimpleWebServerHelper.s_IsRunning)
                SimpleWebServerHelper.Stop();
            QuitBrowser();
            BuildPath = string.Empty;
            ClientUrl = string.Empty;
        }

        static MethodInfo GetCachedMethod(ref WeakReference<MethodInfo> weakRef, string name)
        {
            if (weakRef != null && weakRef.TryGetTarget(out var method))
                return method;
            if (!BuildToolsLocator.IsWebBuildSupportInstalled)
                return null;

            ResolveMethods();
            if (weakRef != null && weakRef.TryGetTarget(out method))
                return method;

            Debug.LogError($"Failed to resolve UnityEditor.WebGL.Browser.{name} via reflection.");
            return null;
        }

        static void ResolveMethods()
        {
            const string assemblyName = "UnityEditor.WebGL.Extensions";
            const string className = "UnityEditor.WebGL.Browser";
            var assembly = AssemblyHelper.FindLoadedAssembly(assemblyName);
            if (assembly == null)
                return;
            var classType = assembly.GetType(className);
            if (classType == null)
                return;
            s_CreateMethodRef = new WeakReference<MethodInfo>(GetMethod(classType, "Create", BindingFlags.Static | BindingFlags.Public));
            s_LaunchMethodRef = new WeakReference<MethodInfo>(GetMethod(classType, "Launch"));
            s_QuitMethodRef = new WeakReference<MethodInfo>(GetMethod(classType, "Quit"));
        }

        static MethodInfo GetMethod(Type classType, string methodName, BindingFlags? bindingFlags = null)
        {
            var methodInfo = (bindingFlags.HasValue)
                ? classType.GetMethod(methodName, bindingFlags.Value)
                : classType.GetMethod(methodName);

            if (methodInfo == null)
                throw new TargetException($"Method '{methodName}' not found in '{classType.FullName}'.");

            return methodInfo;
        }

        void QuitBrowser()
        {
            if (m_Browser == null)
                return;

            try
            {
                s_QuitMethod?.Invoke(m_Browser, null);
            }
            catch (TargetInvocationException e)
            {
                // Process is likely already stopped. We can ignore InvalidOperationExceptions.
                if (e.InnerException is not InvalidOperationException)
                    throw;
            }
            finally
            {
                m_Browser = null;
            }
        }
    }
}
