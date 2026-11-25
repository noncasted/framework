using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Internal
{
    [ProjectToolsTab(400)]
    public class AssetsTab : ProjectToolsTab
    {
        private static readonly ILog _log = Logs.Of<AssetsTab>();

        public override string Title => "Assets";

        protected override void BuildContent(VisualElement parent)
        {
            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("assets-button-row");

            buttonRow.Add(BuildActionButton("Generate Prefabs", OnGeneratePrefabsClicked));
            buttonRow.Add(BuildActionButton("Generate Scenes", OnGenerateScenesClicked));
            buttonRow.Add(BuildActionButton("Generate Sprites", OnGenerateSpritesClicked));
            buttonRow.Add(BuildActionButton("Generate Audio", OnGenerateAudioClicked));

            parent.Add(buttonRow);

            // The generators of the project, the [RegenerateAllStep] methods.
            var projectSteps = RegenerateAll.ProjectSteps();

            if (projectSteps.Count > 0)
            {
                var projectRow = new VisualElement();
                projectRow.AddToClassList("assets-button-row");

                foreach (var step in projectSteps)
                    projectRow.Add(BuildActionButton(step.Name, () => OnProjectStepClicked(step)));

                parent.Add(projectRow);
            }

            var runAllButton = new Button(OnRunAllClicked) { text = "Run All" };
            runAllButton.AddToClassList("assets-run-all-button");
            parent.Add(runAllButton);
        }

        private static Button BuildActionButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("assets-button");

            return button;
        }

        private void OnGeneratePrefabsClicked() => Run("Generating prefabs...", "Prefabs generated",
            "Prefab generation failed", PrefabCatalogGenerator.GenerateNow);

        private void OnGenerateScenesClicked() => Run("Generating scenes...", "Scenes generated",
            "Scene generation failed", SceneGenerator.Generate);

        private void OnGenerateSpritesClicked() => Run("Generating sprites...", "Sprites generated",
            "Sprite generation failed", SpriteGenerator.GenerateNow);

        private void OnGenerateAudioClicked() => Run("Generating audio...", "Audio generated",
            "Audio generation failed", AudioCatalogGenerator.GenerateNow);

        private void OnProjectStepClicked(RegenerateAll.ProjectStep step) => Run($"Running {step.Name}...",
            $"{step.Name} completed", $"{step.Name} failed", () => {
                // A step does not refresh: Regenerate All refreshes once after all of them.
                step.Run();
                AssetDatabase.Refresh();
            });

        private void OnRunAllClicked() => Run("Running all generators...", "All generators completed", "Run All failed",
            RegenerateAll.Run);

        private void Run(string startedMessage, string finishedMessage, string failedMessage, Action action)
        {
            try
            {
                Host.SetStatus(startedMessage, false);
                action();
                Host.SetStatus(finishedMessage, false);
            }
            catch (Exception ex)
            {
                Host.SetStatus(failedMessage, true);
                _log.Error(ex, "{Failed}", failedMessage);
            }
        }
    }
}