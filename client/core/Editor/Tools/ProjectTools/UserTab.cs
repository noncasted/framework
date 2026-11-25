using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    [ProjectToolsTab(500)]
    public class UserTab : ProjectToolsTab
    {
        private const string Empty = "<none>";

        private static readonly BackendEnvironment[] Environments = (BackendEnvironment[])Enum.GetValues(typeof(BackendEnvironment));
        private static readonly ILog _log = Logs.Of<UserTab>();

        // The guests of this project copy, one per backend environment (LocalStorageKeys.Guest).
        private static string GuestKey(BackendEnvironment environment) => LocalStorageKeys.Get(LocalStorageKeys.Guest, environment);

        private readonly Dictionary<BackendEnvironment, Label> _guestLabels = new();

        public override string Title => "User";

        protected override void BuildContent(VisualElement parent)
        {
            var section = BuildSubSection("Local User");

            foreach (var environment in Environments)
                _guestLabels[environment] = BuildValueRow(section, $"Guest {environment} ({GuestKey(environment)})");

            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("assets-button-row");

            buttonRow.Add(BuildActionButton("Copy ids", OnCopyClicked));
            buttonRow.Add(BuildActionButton("Refresh", Refresh));
            buttonRow.Add(BuildActionButton("Clear", OnClearClicked));

            section.Add(buttonRow);
            parent.Add(section);

            Refresh();
        }

        private static Button BuildActionButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("assets-button");

            return button;
        }

        private static Label BuildValueRow(VisualElement parent, string title)
        {
            var label = new Label(title);
            label.AddToClassList("user-id-title");
            parent.Add(label);

            var value = new Label(Empty) { selection = { isSelectable = true } };
            value.AddToClassList("user-id-value");
            parent.Add(value);

            return value;
        }

        private void Refresh()
        {
            foreach (var entry in _guestLabels)
                entry.Value.text = ReadGuestId(entry.Key);
        }

        // Only the ids: the secret stays in the storage.
        private void OnCopyClicked()
        {
            var lines = new List<string>();

            foreach (var environment in Environments)
                lines.Add($"{environment}: {ReadGuestId(environment)}");

            EditorGUIUtility.systemCopyBuffer = string.Join("\n", lines);
            Host.SetStatus("Guest ids copied", false);
        }

        private void OnClearClicked()
        {
            var confirmed = EditorUtility.DisplayDialog(
                "Clear Guests",
                "Delete the saved guests of every backend environment? Their accounts cannot be entered again: a new " +
                "guest is created on the next authentication.",
                "Clear",
                "Cancel");

            if (confirmed == false)
                return;

            foreach (var environment in Environments)
                PlayerPrefs.DeleteKey(GuestKey(environment));

            PlayerPrefs.Save();

            Refresh();
            Host.SetStatus("Guests cleared", false);
            _log.Info("Saved guests cleared");
        }

        private static string ReadGuestId(BackendEnvironment environment)
        {
            var raw = PlayerPrefs.GetString(GuestKey(environment), string.Empty);

            if (string.IsNullOrEmpty(raw) == true)
                return Empty;

            var malformed = $"<malformed: {raw.Length} chars>";

            try
            {
                var record = JsonUtility.FromJson<GuestIdRecord>(raw);
                return record == null || string.IsNullOrEmpty(record.Id) == true ? malformed : record.Id;
            }
            // Not a json at all: the tab shows what lies in the prefs of the player, whatever it is.
            catch (ArgumentException)
            {
                return malformed;
            }
        }

        // The id field of the guest record (the game stores {Id, Secret}).
        [Serializable]
        private class GuestIdRecord
        {
            public string Id;
        }
    }
}
