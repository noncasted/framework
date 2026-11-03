namespace Global.Telemetry
{
    // The names of the error codes of WebGL: the suffix of a mark and the constant of a message.
    public static class TelemetryGlErrorCodes
    {
        public static string MarkSuffixOf(int code)
        {
            return code switch
            {
                0x0500 => "invalid_enum",
                0x0501 => "invalid_value",
                0x0502 => "invalid_operation",
                0x0505 => "out_of_memory",
                0x0506 => "invalid_framebuffer_operation",
                0x9242 => "context_lost",
                _ => "other"
            };
        }

        public static string ConstantOf(int code)
        {
            return code switch
            {
                0x0500 => "INVALID_ENUM",
                0x0501 => "INVALID_VALUE",
                0x0502 => "INVALID_OPERATION",
                0x0505 => "OUT_OF_MEMORY",
                0x0506 => "INVALID_FRAMEBUFFER_OPERATION",
                0x9242 => "CONTEXT_LOST_WEBGL",
                _ => "of another code"
            };
        }
    }
}
