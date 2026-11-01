using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Internal
{
    [ProjectToolsTab(300)]
    public class OptionsTab : ProjectToolsTab
    {
        // The fields of the asset: the options have no setters, the tab writes what the serializer stores.
        internal const string EnableGizmosPath = "_debug._enableGizmos";
        internal const string EnableLogsPath = "_debug._enableLogs";
        internal const string VersionPath = "_version._value";
        internal const string EnvironmentPath = "_backend._environment";
        internal const string ProductionApiUrlPath = "_backend._productionApiUrl";
        internal const string LocalApiUrlPath = "_backend._localApiUrl";
        internal const string TimeoutSecondsPath = "_backend._timeoutSeconds";
        internal const string PlatformPath = "_platform._platform";

        public override string Title => "Options";

        protected override void BuildContent(VisualElement parent)
        {
            var options = Host.Options;

            if (options == null)
            {
                parent.Add(new Label("OptionsContainer asset is missing"));
                return;
            }

            var debugSection = BuildSubSection("Debug Options");
            var gizmos = new Toggle("Enable Gizmos") { value = options.DebugOptions.EnableGizmos };
            gizmos.RegisterValueChangedCallback(e => Write(options, EnableGizmosPath, p => p.boolValue = e.newValue));
            debugSection.Add(gizmos);

            var logs = new Toggle("Enable Logs") { value = options.DebugOptions.EnableLogs };
            logs.RegisterValueChangedCallback(e => Write(options, EnableLogsPath, p => p.boolValue = e.newValue));
            debugSection.Add(logs);
            parent.Add(debugSection);

            var versionSection = BuildSubSection("Version Options");
            var field = new TextField("Version") { value = options.VersionOptions.Value };
            field.RegisterValueChangedCallback(e => Write(options, VersionPath, p => p.stringValue = e.newValue));
            versionSection.Add(field);
            parent.Add(versionSection);

            var backendSection = BuildSubSection("Backend Options");
            var envField = new EnumField("Environment", options.BackendOptions.Environment);

            envField.RegisterValueChangedCallback(e =>
                Write(options, EnvironmentPath, p => p.intValue = (int)(BackendEnvironment)e.newValue));
            backendSection.Add(envField);

            var prodUrl = new TextField("Production URL") { value = options.BackendOptions.ProductionApiUrl };

            prodUrl.RegisterValueChangedCallback(e =>
                Write(options, ProductionApiUrlPath, p => p.stringValue = e.newValue));
            backendSection.Add(prodUrl);

            var localUrl = new TextField("Local URL") { value = options.BackendOptions.LocalApiUrl };
            localUrl.RegisterValueChangedCallback(e => Write(options, LocalApiUrlPath, p => p.stringValue = e.newValue));
            backendSection.Add(localUrl);

            var timeout = new IntegerField("Timeout (sec)") { value = options.BackendOptions.TimeoutSeconds };
            timeout.RegisterValueChangedCallback(e => Write(options, TimeoutSecondsPath, p => p.intValue = e.newValue));
            backendSection.Add(timeout);
            parent.Add(backendSection);

            var platformSection = BuildSubSection("Platform Options");
            var platformField = new EnumField("Platform Type", options.PlatformOptions.PlatformType);

            platformField.RegisterValueChangedCallback(e =>
                Write(options, PlatformPath, p => p.intValue = (int)(PlatformType)e.newValue));
            platformSection.Add(platformField);
            parent.Add(platformSection);
        }

        // Options now live in an asset, so an edit must mark it dirty (the apply does, with the undo step):
        // otherwise the domain reload rolls it back before Save is pressed.
        internal static void Write(OptionsContainer options, string path, Action<SerializedProperty> write)
        {
            using var serialized = new SerializedObject(options);
            write(serialized.FindProperty(path));
            serialized.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Project Options");
        }
    }
}