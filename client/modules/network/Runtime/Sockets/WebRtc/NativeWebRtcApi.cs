#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Internal
{
    // The C API of libdatachannel (rtc.h of v0.24, Plugins/libdatachannel; built by Source~/libdatachannel/build.sh).
    // Strings go as null-terminated UTF-8 byte arrays: blittable on Mono and IL2CPP alike.
    internal static class NativeWebRtcApi
    {
        // Not the name of the upstream library: the runtime of the editor asks the system loader for a bare name before
        // the plugins of the project, so an installed libdatachannel (on OpenSSL) would win over this build.
        private const string Library = "framework_datachannel";

        public const int Success = 0;

        public enum State
        {
            New = 0,
            Connecting = 1,
            Connected = 2,
            Disconnected = 3,
            Failed = 4,
            Closed = 5
        }

        public enum LogLevel
        {
            None = 0,
            Fatal = 1,
            Error = 2,
            Warning = 3,
            Info = 4,
            Debug = 5,
            Verbose = 6
        }

        // rtcConfiguration; C bool is one byte. The order and the types of the fields are the native layout: the marshaller
        // reads them whatever their access is, the fields without a value stay zero (the defaults of the library).
        [StructLayout(LayoutKind.Sequential)]
        public readonly struct Configuration
        {
            public Configuration(IntPtr iceServers, int iceServersCount)
            {
                this = default;
                _iceServers = iceServers;
                _iceServersCount = iceServersCount;
            }

            private readonly IntPtr _iceServers;
            private readonly int _iceServersCount;
#pragma warning disable CS0169 // The native side reads them.
            private readonly IntPtr _proxyServer;
            private readonly IntPtr _bindAddress;
            private readonly int _certificateType;
            private readonly int _iceTransportPolicy;
            private readonly byte _enableIceTcp;
            private readonly byte _enableIceUdpMux;
            private readonly byte _disableAutoNegotiation;
            private readonly byte _forceMediaTransport;
            private readonly ushort _portRangeBegin;
            private readonly ushort _portRangeEnd;
            private readonly int _mtu;
            private readonly int _maxMessageSize;
#pragma warning restore CS0169
        }

        // rtcReliability of rtc.h v0.24: C bool is one byte, the fields after the two flags are 4-byte aligned.
        [StructLayout(LayoutKind.Sequential)]
        public struct Reliability
        {
            public byte Unordered;
            public byte Unreliable;
            public uint MaxPacketLifeTime;
            public uint MaxRetransmits;
        }

        // rtcDataChannelInit of rtc.h v0.24: the pointer is 8-byte aligned after rtcReliability, the two flags and the
        // stream follow it. The native side reads it by offset.
        [StructLayout(LayoutKind.Sequential)]
        public struct DataChannelInit
        {
            public Reliability Reliability;
            public IntPtr Protocol;
            public byte Negotiated;
            public byte ManualStream;
            public ushort Stream;

            public static DataChannelInit Of(WebRtcChannelMode mode)
            {
                var init = default(DataChannelInit);

                // A reliable ordered channel is the zero init: the library keeps the defaults of the channel.
                if (mode == WebRtcChannelMode.Unreliable)
                {
                    init.Reliability.Unordered = 1;
                    init.Reliability.Unreliable = 1;
                    init.Reliability.MaxRetransmits = 0;
                }

                return init;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogCallback(LogLevel level, IntPtr message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DescriptionCallback(int pc, IntPtr sdp, IntPtr type, IntPtr user);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CandidateCallback(int pc, IntPtr candidate, IntPtr mid, IntPtr user);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void StateCallback(int pc, State state, IntPtr user);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DataChannelCallback(int pc, int dc, IntPtr user);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ChannelCallback(int id, IntPtr user);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ErrorCallback(int id, IntPtr error, IntPtr user);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void MessageCallback(int id, IntPtr message, int size, IntPtr user);

        [DllImport(Library, EntryPoint = "rtcInitLogger", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitLogger(LogLevel level, LogCallback callback);

        [DllImport(Library, EntryPoint = "rtcSetUserPointer", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetUserPointer(int id, IntPtr user);

        [DllImport(Library, EntryPoint = "rtcCreatePeerConnection", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CreatePeerConnection(ref Configuration configuration);

        [DllImport(Library, EntryPoint = "rtcDeletePeerConnection", CallingConvention = CallingConvention.Cdecl)]
        public static extern int DeletePeerConnection(int pc);

        [DllImport(Library, EntryPoint = "rtcSetLocalDescriptionCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetLocalDescriptionCallback(int pc, DescriptionCallback callback);

        [DllImport(Library, EntryPoint = "rtcSetLocalCandidateCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetLocalCandidateCallback(int pc, CandidateCallback callback);

        [DllImport(Library, EntryPoint = "rtcSetStateChangeCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetStateChangeCallback(int pc, StateCallback callback);

        [DllImport(Library, EntryPoint = "rtcSetDataChannelCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetDataChannelCallback(int pc, DataChannelCallback callback);

        [DllImport(Library, EntryPoint = "rtcSetRemoteDescription", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetRemoteDescription(int pc, byte[] sdp, byte[] type);

        [DllImport(Library, EntryPoint = "rtcAddRemoteCandidate", CallingConvention = CallingConvention.Cdecl)]
        public static extern int AddRemoteCandidate(int pc, byte[] candidate, byte[] mid);

        [DllImport(Library, EntryPoint = "rtcCreateDataChannel", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CreateDataChannel(int pc, byte[] label);

        [DllImport(Library, EntryPoint = "rtcCreateDataChannelEx", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CreateDataChannelEx(int pc, byte[] label, ref DataChannelInit init);

        // The buffer is a null-terminated UTF-8 string: null asks its size, one byte past the label.
        [DllImport(Library, EntryPoint = "rtcGetDataChannelLabel", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetDataChannelLabel(int dc, IntPtr buffer, int size);

        [DllImport(Library, EntryPoint = "rtcSetOpenCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetOpenCallback(int id, ChannelCallback callback);

        [DllImport(Library, EntryPoint = "rtcSetClosedCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetClosedCallback(int id, ChannelCallback callback);

        [DllImport(Library, EntryPoint = "rtcSetErrorCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetErrorCallback(int id, ErrorCallback callback);

        [DllImport(Library, EntryPoint = "rtcSetMessageCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetMessageCallback(int id, MessageCallback callback);

        [DllImport(Library, EntryPoint = "rtcSendMessage", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SendMessage(int id, byte[] data, int size);

        [DllImport(Library, EntryPoint = "rtcIsOpen", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool IsOpen(int id);

        [DllImport(Library, EntryPoint = "rtcDelete", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Delete(int id);

        [DllImport(Library, EntryPoint = "rtcMaxMessageSize", CallingConvention = CallingConvention.Cdecl)]
        public static extern int MaxMessageSize(int id);

        [DllImport(Library, EntryPoint = "rtcGetBufferedAmount", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetBufferedAmount(int id);

        public static byte[] Utf8(string value)
        {
            var bytes = new byte[Encoding.UTF8.GetByteCount(value) + 1];
            Encoding.UTF8.GetBytes(value, 0, value.Length, bytes, 0);
            return bytes;
        }

        public static string String(IntPtr value)
        {
            return value == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(value);
        }

        public static int Check(int result, string call)
        {
            if (result < Success)
                throw new InvalidOperationException($"[Network] [libdatachannel] {call} failed: error={result}");

            return result;
        }
    }
}
#endif
