using System.Globalization;
using Cluster.Monitoring.Online;

namespace Console.Infrastructure.Online;

// Times of the online statistics as the console shows them: UTC, like the other pages of the console.
public static class OnlinePeriodFormat
{
    public static string Label(OnlinePeriod period, OnlineScale scale)
    {
        return scale switch
        {
            OnlineScale.Hours => period.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
            OnlineScale.Days => period.Start.ToString("dd.MM", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, null)
        };
    }

    public static string Day(OnlinePeriod period)
    {
        return period.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string Updated(DateTime time)
    {
        return $"{time.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} UTC";
    }

    // The last periods of the scale: the oldest first, the current one last.
    public static IReadOnlyList<OnlinePeriod> Last(OnlineStatsLiveData data, OnlineScale scale, int count)
    {
        var periods = scale switch
        {
            OnlineScale.Hours => data.Hours,
            OnlineScale.Days => data.Days,
            _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, null)
        };

        return periods.TakeLast(count).ToArray();
    }
}
