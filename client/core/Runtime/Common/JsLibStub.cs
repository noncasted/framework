using System;

namespace Internal
{
    // What a function of a .jslib does outside a WebGL player: it exists only there.
    public static class JsLibStub
    {
        public static Exception Missing()
        {
            return new PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");
        }
    }
}
