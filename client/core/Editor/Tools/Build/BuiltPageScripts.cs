using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Internal
{
    // The scripts a package puts first into the <head> of the index.html of a web build (WebTemplate~/<file> of the package),
    // so they run before every script of the template of the game. The built page is changed, never the template: no file of
    // the game is written. A page that already has the script (its marker attribute) stays as it is; a page without a <head>
    // fails the build.
    public static class BuiltPageScripts
    {
        private static readonly Regex _head = new(@"<head(\s[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static void InjectIntoBuild(BuildReport report, string marker, string scriptPath)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            var page = Path.Combine(report.summary.outputPath, "index.html");
            File.WriteAllText(page, Inject(File.ReadAllText(page), marker, File.ReadAllText(scriptPath)));
        }

        public static string Inject(string html, string marker, string script)
        {
            if (html.Contains(marker))
                return html;

            var head = _head.Match(html);

            if (head.Success == false)
                throw new BuildFailedException($"index.html has no <head>: nowhere to put the script {marker}");

            var tag = $"\n<script {marker}>\n{script.Trim()}\n</script>";

            return html.Insert(head.Index + head.Length, tag);
        }

        // WebTemplate~/<file> of the package whose Editor folder holds the script of the type.
        public static string ScriptPath(Type editorType, string file)
        {
            var guid = AssetDatabase.FindAssets($"{editorType.Name} t:MonoScript").Single();
            var editor = Path.GetDirectoryName(Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid)));
            var path = Path.Combine(Path.GetDirectoryName(editor) ?? throw new InvalidOperationException(editor), "WebTemplate~", file);

            if (File.Exists(path) == false)
                throw new FileNotFoundException($"The script of the page of {editorType.Name} is missing", path);

            return path;
        }
    }
}
