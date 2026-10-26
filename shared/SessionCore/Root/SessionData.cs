using System;
using System.Collections.Generic;
using System.Linq;
using Internal;
using Shared;

namespace Game.Session
{
    public interface ISessionData
    {
        SessionType Type { get; }
        int ExpectedUsers { get; }
        Guid Id { get; }
        IReadOnlyLifetime Lifetime { get; }
    }

    public class SessionData : ISessionData
    {
        public SessionType Type { get; set; }
        public int ExpectedUsers { get; set; }
        public Guid Id { get; set; }
        public IReadOnlyLifetime Lifetime { get; set; }
    }
}
