using System;
using Shared;

namespace Internal
{
    // The game server has refused the ticket (SharedSessionAuth.Response.Status). For a return into a match
    // NoReservation / Closed mean the seat is gone; otherwise it is an error of the entry.
    public class SessionEntryRefusedException : Exception
    {
        public SessionEntryRefusedException(SessionAuthStatus status)
            : base($"[Network] [Session] The session refused the entry: {status}")
        {
            Status = status;
        }

        public SessionAuthStatus Status { get; }
    }
}
