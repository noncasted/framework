using System.Runtime.CompilerServices;

// The internals of the content directories (stores, registration) for the tests and the content smoke of client/project.
[assembly: InternalsVisibleTo("Internal.Tests")]
[assembly: InternalsVisibleTo("Internal.PlayMode.Tests")]
[assembly: InternalsVisibleTo("ContentSmoke")]
