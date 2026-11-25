namespace Internal
{
    public static class WebRtcPeers
    {
        // The platforms with a peer: the browser, and the ones libdatachannel is built for (Runtime/Plugins). Elsewhere
        // Create fails with DllNotFoundException.
        public static bool IsSupported(UnityEngine.RuntimePlatform platform)
        {
            return platform switch
            {
                UnityEngine.RuntimePlatform.WebGLPlayer => true,
                UnityEngine.RuntimePlatform.LinuxEditor => true,
                UnityEngine.RuntimePlatform.LinuxPlayer => true,
                UnityEngine.RuntimePlatform.WindowsEditor => true,
                UnityEngine.RuntimePlatform.WindowsPlayer => true,
                _ => false
            };
        }

        public static IWebRtcPeer Create(WebRtcPeerOptions options, IWebRtcPeerEvents events)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new JsWebRtcPeer(options, events);
#else
            return new NativeWebRtcPeer(options, events);
#endif
        }

        // The connection with every channel of the options: the desktop peer carries them all, the browser one channel
        // (SingleChannelWebRtcPeer), which is all a link of one channel asks for.
        public static IWebRtcChannels CreateChannels(WebRtcPeerOptions options, IWebRtcChannelEvents events)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new SingleChannelWebRtcPeer(options, events);
#else
            return new NativeWebRtcPeer(options, events);
#endif
        }
    }
}
