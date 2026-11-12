using UnityEngine.Scripting;

// Nothing references this assembly: it registers its decoder at the start, so the linker keeps it by this attribute.
// A game without mp3 bytes in its audio groups drops it (StrippedPlayerAssemblies of ProjectSettings/Framework.json),
// and UnityWebRequest with it.
[assembly: AlwaysLinkAssembly]
