using Cluster.Deploy;
using Common.Extensions;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging;

namespace Cluster.Monitoring.Online;

// Runs on the coordinator, the one host of every deploy. Restores the days of the statistics, applies every change
// of ConnectedUsersLiveData (and the start of every hour, when the same users stay online) to the counter, and
// writes the changed days and the OnlineStatsLiveData at most once per FlushDelay.
public class OnlineTracker : ICoordinatorSetupCompleted
{
    public OnlineTracker(
        IDeploymentState<ConnectedUsersLiveData> connected,
        IDeploymentState<OnlineStatsLiveData> stats,
        IOnlineDaysStore store,
        ILogger<OnlineTracker> logger)
    {
        _connected = connected;
        _stats = stats;
        _store = store;
        _logger = logger;
    }

    private static readonly TimeSpan FlushDelay = TimeSpan.FromSeconds(5);

    private readonly IDeploymentState<ConnectedUsersLiveData> _connected;
    private readonly IDeploymentState<OnlineStatsLiveData> _stats;
    private readonly IOnlineDaysStore _store;
    private readonly ILogger<OnlineTracker> _logger;

    // The View callbacks come from the deliveries of the channel, the hour ticks and the flush from the thread pool.
    private readonly object _lock = new();
    private OnlineCounter _counter = null!;
    private IReadOnlySet<Guid> _online = new HashSet<Guid>();
    private bool _flushScheduled;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        var now = DateTime.UtcNow;
        var restored = await _store.Read(OnlineCounter.FirstKeptDay(now), OnlineCounter.DayOf(now));
        _counter = new OnlineCounter(restored);

        _connected.View(lifetime, data => {
            var online = data.AllUsers().Select(user => user.UserId).ToHashSet();
            Apply(lifetime, online);
        });

        HourTicks(lifetime).NoAwait();
    }

    private void Apply(IReadOnlyLifetime lifetime, IReadOnlySet<Guid> online)
    {
        lock (_lock)
        {
            _online = online;
            _counter.Apply(online, DateTime.UtcNow);

            if (_flushScheduled == true)
                return;

            _flushScheduled = true;
        }

        Flush(lifetime).NoAwait();
    }

    private async Task HourTicks(IReadOnlyLifetime lifetime)
    {
        try
        {
            while (lifetime.IsTerminated == false)
            {
                var now = DateTime.UtcNow;
                var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
                await Task.Delay(next - now + TimeSpan.FromSeconds(1), lifetime.Token);

                IReadOnlySet<Guid> online;

                lock (_lock)
                    online = _online;

                Apply(lifetime, online);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsTerminated == true)
        {
        }
    }

    // The top of an unawaited task: a failed write is logged and its days go with the next flush.
    private async Task Flush(IReadOnlyLifetime lifetime)
    {
        IReadOnlyList<OnlineDayState> changed = [];

        try
        {
            await Task.Delay(FlushDelay, lifetime.Token);

            OnlineStatsLiveData summary;

            lock (_lock)
            {
                _flushScheduled = false;
                changed = _counter.TakeChanged();
                summary = _counter.Summary(DateTime.UtcNow);
            }

            await _store.Write(changed);
            await _stats.SetValue(summary);
        }
        catch (OperationCanceledException) when (lifetime.IsTerminated == true)
        {
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[OnlineTracker] Failed to write the online statistics");

            lock (_lock)
                _counter.MarkChanged(changed.Select(day => day.Day));
        }
    }
}
