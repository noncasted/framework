using System.Runtime.CompilerServices;

// The editor tests check the generators through their internal steps (the scan, the source of a class).
[assembly: InternalsVisibleTo("Internal.Editor.Tests")]
