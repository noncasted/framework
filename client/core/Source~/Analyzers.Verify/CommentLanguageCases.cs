namespace Framework.Analyzers.Verify
{
    // FWR004: Cyrillic in any kind of comment; strings and English comments are fine.
    internal static class CommentLanguageCases
    {
        public static void Run(Harness harness)
        {
            harness.Case("FWR004 every kind of comment", One(@"
// a comment in English
public class A
{
    // комментарий
    /* блок */
    /// <summary>документация</summary>
    public void Use() { }

    /** ещё документация */
    public void Other() { } // в конце строки
}"), new[] { "FWR004@5", "FWR004@6", "FWR004@7", "FWR004@10", "FWR004@11" });

            harness.Case("FWR004 strings and English are fine", One(@"
public class A
{
    // Russian text lives in the localization table.
    private const string Text = ""привет"";
    private const char Letter = 'я';
}"), new string[0]);

            harness.Case("FWR004 generated code is skipped", new[] { new Source("Runtime/A.g.cs", "// комментарий\npublic class A { }") },
                new string[0]);
        }

        private static Source[] One(string text)
        {
            return new[] { new Source("Runtime/A.cs", text) };
        }
    }
}
