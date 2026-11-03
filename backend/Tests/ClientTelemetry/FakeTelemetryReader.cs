using Game.ClientTelemetry;

namespace Framework.Tests.ClientTelemetry;

// The pages of IClientTelemetryReader over lists: a limit never cuts one seq, like the real one.
public sealed class FakeTelemetryReader : IClientTelemetryReader
{
    private readonly Dictionary<Guid, TelemetrySessionInfo> _sessions = new();

    public List<TelemetryStoredSample> Samples { get; } = [];
    public List<TelemetryStoredMark> Marks { get; } = [];
    public Dictionary<Guid, List<TelemetryStoredError>> Errors { get; } = new();
    public List<Guid> ErrorReads { get; } = [];

    public void Set(TelemetrySessionInfo session)
    {
        _sessions[session.Id] = session;
    }

    public Task<TelemetrySessionInfo?> GetSession(Guid id)
    {
        return Task.FromResult(_sessions.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<TelemetryStoredSample>> GetSamples(Guid session, long afterSeq, int limit)
    {
        return Task.FromResult(Page(Samples, afterSeq, limit, sample => sample.Seq));
    }

    public Task<IReadOnlyList<TelemetryStoredError>> GetErrors(Guid session, long afterSeq, int limit)
    {
        ErrorReads.Add(session);
        return Task.FromResult(Page(Errors.GetValueOrDefault(session) ?? [], afterSeq, limit, error => error.Seq));
    }

    public Task<IReadOnlyList<TelemetryStoredMark>> GetMarks(Guid session, long afterSeq, int limit)
    {
        return Task.FromResult(Page(Marks, afterSeq, limit, mark => mark.Seq));
    }

    public Task<IReadOnlyList<TelemetrySessionInfo>> GetSessions(TelemetrySessionFilter filter)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<TelemetryStartupInfo>> GetStartups(int limit)
    {
        throw new NotSupportedException();
    }

    public Task<TelemetryStartupInfo?> GetStartup(Guid id)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<TelemetryStartupSpan>> GetStartupSpans(Guid id)
    {
        throw new NotSupportedException();
    }

    public Task<TelemetryGlobalErrorGroups> GetErrorGroups(int limit)
    {
        throw new NotSupportedException();
    }

    private static IReadOnlyList<T> Page<T>(IEnumerable<T> rows, long afterSeq, int limit, Func<T, long> seq)
    {
        var next = rows.Where(row => seq(row) > afterSeq).ToArray();

        if (next.Length <= limit)
            return next;

        return next.Where(row => seq(row) <= seq(next[limit - 1])).ToArray();
    }
}
