using System.Runtime.CompilerServices;

// The internals of the content directories (stores, registration) for the tests and the editor.
[assembly: InternalsVisibleTo("Internal.Tests")]
[assembly: InternalsVisibleTo("Internal.PlayMode.Tests")]
[assembly: InternalsVisibleTo("Internal.Editor")]
