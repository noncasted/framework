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
    // so they run before every script of the template of the game. They go right after the <meta charset> of the template when
    // it comes before the first script (the browser looks for the encoding in the first 1024 bytes only, and these scripts are
    // kilobytes), otherwise right after <head>. Each script goes to the same place, so a later one stands before the earlier
    // ones. The built page is changed, never the template: no file of the game is written. A page that already has the script
    // (its marker attribute) stays as it is; a page without a <head> fails the build.
    public static class BuiltPageScripts
    {
        private static readonly Regex _head = new(@"<head(\s[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex _charset = new(@"<meta\s[^>]*charset[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex _script = new(@"<script[\s>]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

            return html.Insert(Place(html, head.Index + head.Length), tag);
        }

        // After the <meta charset> that comes before the first script of the head, otherwise right after <head>.
        private static int Place(string html, int headEnd)
        {
            var charset = _charset.Match(html, headEnd);

            if (charset.Success == false)
                return headEnd;

            var script = _script.Match(html, headEnd);

            if (script.Success == true && script.Index < charset.Index)
                return headEnd;

            return charset.Index + charset.Length;
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
