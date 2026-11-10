using System;
using System.Collections.Generic;
using System.Globalization;

namespace ContainerGenerator {
    internal static class EntityAssets {
        public static void Bind(GraphDocument document, EntityAssetIndex index) {
            if (document == null || index == null)
                return;

            BindViews(document, index);
            BindScenes(document, index);
        }

        // Entity anchor: the components of the view asset are merged in as calls of their IEntityComponent.Register.
        private static void BindViews(GraphDocument document, EntityAssetIndex index) {
            var registers = MembersOf(document, ".Register");

            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                if (string.IsNullOrEmpty(method.ViewType))
                    continue;

                var asset = index.Find(method.ViewType);
                if (asset == null)
                    continue;

                var ordinal = NextOrdinal(method);
                for (var c = 0; c < asset.ComponentTypes.Count; c++) {
                    var component = EntityAssetIndex.Normalize(asset.ComponentTypes[c]);
                    if (string.IsNullOrEmpty(component))
                        continue;
                    if (registers.TryGetValue(component, out var callId) == false)
                        continue;
                    if (method.Calls.Contains(callId))
                        continue;

                    method.AddCall(callId, ordinal++);
                }
            }
        }

        // Scene anchor: FindOrLoadSceneWithServices(Scenes.X) (a ContentScene) merges in ISceneService.Create of every
        // service of the SceneServicesFactory of that scene, at the call site and in the order of the factory.
        private static void BindScenes(GraphDocument document, EntityAssetIndex index) {
            var creates = MembersOf(document, ".Create");

            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                for (var r = 0; r < method.Registrations.Count; r++) {
                    var registration = method.Registrations[r];
                    if (registration.Origin != "SceneServices")
                        continue;

                    var scene = FindScene(index, registration.Hole);
                    if (scene == null) {
                        document.Diagnostics.Add(new DiagnosticInfo(
                            GraphDescriptors.UncoveredSyntax,
                            registration.Location,
                            MissingScene(document, index, registration.Hole),
                            method.Id,
                            registration.File ?? "",
                            registration.Line.ToString(CultureInfo.InvariantCulture)));
                        continue;
                    }

                    for (var c = 0; c < scene.ComponentTypes.Count; c++) {
                        var component = EntityAssetIndex.Normalize(scene.ComponentTypes[c]);
                        if (creates.TryGetValue(component, out var callId) == false)
                            continue;
                        if (method.Calls.Contains(callId))
                            continue;

                        method.AddCall(callId, registration.Ordinal);
                    }
                }
            }
        }

        // Without a single scene the map itself is out of sight: it lives in the content assembly of the project, and an
        // assembly that walks the scopes of a game without a reference to it reads them with an empty map.
        private static string MissingScene(GraphDocument document, EntityAssetIndex index, string hole) {
            const string map = "the asset map (ContainerGraph.AssetMap.cs)";

            for (var i = 0; i < index.Assets.Count; i++) {
                if (EntityAssetIndex.Normalize(index.Assets[i].HolderType).EndsWith("SceneServicesFactory", StringComparison.Ordinal))
                    return "the services of scene '" + hole + "': " + map + " has no SceneServicesFactory of this scene - " +
                           "regenerate the map (Tools/Framework/Codegen/Container Asset Map) or put a SceneServicesFactory into the scene";
            }

            return "the services of scene '" + hole + "': assembly '" + document.AssemblyName + "' sees no scene of " + map +
                   " - most likely it does not reference the content assembly of the project, which holds the map " +
                   "(FrameworkSettings.ContentAssembly, Internal.Content by default): add it to the references of the asmdef";
        }

        private static EntityAsset? FindScene(EntityAssetIndex index, string hole) {
            var field = SceneField(hole);
            if (string.IsNullOrEmpty(field))
                return null;

            for (var i = 0; i < index.Assets.Count; i++) {
                var asset = index.Assets[i];
                if (EntityAssetIndex.Normalize(asset.HolderType).EndsWith("SceneServicesFactory", StringComparison.Ordinal) == false)
                    continue;
                if (string.Equals(SceneName(asset.AssetPath), field, StringComparison.OrdinalIgnoreCase))
                    return asset;
            }

            return null;
        }

        // Scenes.GameField -> GameField
        private static string SceneField(string hole) {
            const string marker = "Scenes.";
            if (string.IsNullOrEmpty(hole))
                return "";
            var start = hole.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return "";

            start += marker.Length;
            var end = start;
            while (end < hole.Length && (char.IsLetterOrDigit(hole[end]) || hole[end] == '_'))
                end++;
            return hole.Substring(start, end - start);
        }

        // Assets/Scenes/Main_Menu.unity -> MainMenu (the scene catalogue generator writes the names this way)
        private static string SceneName(string assetPath) {
            if (string.IsNullOrEmpty(assetPath))
                return "";
            var slash = Math.Max(assetPath.LastIndexOf('/'), assetPath.LastIndexOf('\\'));
            var name = slash >= 0 ? assetPath.Substring(slash + 1) : assetPath;
            var dot = name.LastIndexOf('.');
            if (dot > 0)
                name = name.Substring(0, dot);
            return name.Replace("_", "");
        }

        private static Dictionary<string, string> MembersOf(GraphDocument document, string suffix) {
            var members = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < document.Methods.Count; i++) {
                var id = document.Methods[i].Id;
                if (string.IsNullOrEmpty(id) || id.EndsWith(suffix, StringComparison.Ordinal) == false)
                    continue;
                var type = id.Substring(0, id.Length - suffix.Length);
                if (string.IsNullOrEmpty(type) == false)
                    members[EntityAssetIndex.Normalize(type)] = id;
            }

            return members;
        }

        private static int NextOrdinal(GraphMethod method) {
            var max = -1;
            for (var i = 0; i < method.CallOrdinals.Count; i++) {
                if (method.CallOrdinals[i] > max)
                    max = method.CallOrdinals[i];
            }

            for (var i = 0; i < method.Registrations.Count; i++) {
                if (method.Registrations[i].Ordinal > max)
                    max = method.Registrations[i].Ordinal;
            }

            return max + 1;
        }
    }
}
