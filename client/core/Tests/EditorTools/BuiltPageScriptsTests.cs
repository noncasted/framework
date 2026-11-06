using System.Text;
using NUnit.Framework;
using UnityEditor.Build;

namespace Internal.EditorTests
{
    // The place of a script put into the built index.html: after the <meta charset> of the template, so that the declaration of
    // the encoding stays in the first 1024 bytes the browser looks into, and before every script of the template.
    public class BuiltPageScriptsTests
    {
        private const string Template =
            "<!DOCTYPE html>\n<html lang=\"en-us\">\n<head>\n<meta charset=\"utf-8\">\n" +
            "<meta name=\"viewport\" content=\"width=device-width, height=device-height, initial-scale=1.0\">\n" +
            "<title>Игра</title>\n<link rel=\"stylesheet\" href=\"TemplateData/style.css\">\n" +
            "<script src=\"TemplateData/web-shell.js\"></script>\n</head>\n" +
            "<body><script>createUnityInstance();</script></body>\n</html>";

        // As big as render-resolution.js and startup-trace.js.
        private static readonly string _big = "// " + new string('x', 6300);

        [Test]
        public void Inject_KeepsTheCharsetInTheFirst1024Bytes()
        {
            var html = BuiltPageScripts.Inject(Template, "data-big", _big);

            var charset = html.IndexOf("<meta charset=\"utf-8\">") + "<meta charset=\"utf-8\">".Length;
            Assert.LessOrEqual(Encoding.UTF8.GetByteCount(html.Substring(0, charset)), 1024,
                "<meta charset> of the template must end in the first 1024 bytes");
            Assert.Less(html.IndexOf("<script data-big>"), html.IndexOf("web-shell.js"));
        }

        [Test]
        public void Inject_Several_KeepTheCharsetAndTheirOrder()
        {
            var html = BuiltPageScripts.Inject(Template, "data-first", _big);
            html = BuiltPageScripts.Inject(html, "data-second", _big);
            html = BuiltPageScripts.Inject(html, "data-third", _big);

            var charset = html.IndexOf("<meta charset=\"utf-8\">");
            var first = html.IndexOf("<script data-first>");
            var second = html.IndexOf("<script data-second>");
            var third = html.IndexOf("<script data-third>");
            Assert.Less(charset, third);
            Assert.Less(third, second);
            Assert.Less(second, first);
            Assert.Less(first, html.IndexOf("<script src=\"TemplateData/web-shell.js\">"));
        }

        [Test]
        public void Inject_WithoutACharset_GoesFirstIntoTheHead()
        {
            var html = BuiltPageScripts.Inject(
                "<html>\n<HEAD lang=\"en\">\n<script src=\"TemplateData/web-shell.js\"></script>\n</HEAD>\n</html>", "data-big", _big);

            Assert.AreEqual(html.IndexOf("<HEAD lang=\"en\">") + "<HEAD lang=\"en\">".Length + 1, html.IndexOf("<script data-big>"));
        }

        [Test]
        public void Inject_ACharsetAfterAScriptOfTheTemplate_GoesFirstIntoTheHead()
        {
            var html = BuiltPageScripts.Inject(
                "<html>\n<head>\n<script src=\"TemplateData/web-shell.js\"></script>\n<meta charset=\"utf-8\">\n</head>\n</html>", "data-big", _big);

            Assert.Less(html.IndexOf("<script data-big>"), html.IndexOf("web-shell.js"));
        }

        [Test]
        public void Inject_AnHttpEquivCharset_GoesAfterIt()
        {
            const string meta = "<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">";
            var html = BuiltPageScripts.Inject(
                $"<html>\n<head>\n{meta}\n<script src=\"TemplateData/web-shell.js\"></script>\n</head>\n</html>", "data-big", _big);

            Assert.Less(html.IndexOf(meta), html.IndexOf("<script data-big>"));
            Assert.Less(html.IndexOf("<script data-big>"), html.IndexOf("web-shell.js"));
        }

        [Test]
        public void Inject_APageThatHasIt_StaysAsItIs()
        {
            var html = BuiltPageScripts.Inject(Template, "data-big", _big);

            Assert.AreEqual(html, BuiltPageScripts.Inject(html, "data-big", _big));
        }

        [Test]
        public void Inject_APageWithoutAHead_FailsTheBuild()
        {
            Assert.Throws<BuildFailedException>(() => BuiltPageScripts.Inject("<html><header></header></html>", "data-big", _big));
        }
    }
}
