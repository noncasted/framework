using System;
using Shared;

namespace Internal
{
    public class LocalServerRequestException : Exception
    {
        public LocalServerRequestException(ErrorCode code, string message) : base(message)
        {
            Code = code;
        }

        public ErrorCode Code { get; }
    }
}
