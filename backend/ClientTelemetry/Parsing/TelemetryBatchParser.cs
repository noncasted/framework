using Shared;

namespace Game.ClientTelemetry;

// The only way from a message of a client to the model. The client is not trusted, so everything may be null here: what breaks a
// limit rejects the rows of the batch, what breaks a privacy rule is dropped alone, the texts of errors are scrubbed here.
public static class TelemetryBatchParser
{
    private const int _rowBytes = 16;
    private const int _valueBytes = sizeof(double);

    public static TelemetryBatch Parse(SharedClientTelemetry.Batch batch)
    {
        var slots = batch.Names?.Length ?? 0;
        var names = slots > ClientTelemetryLimits.MaxNamesPerSession ? [] : ReadNames(batch.Names);
        var rejection = slots > ClientTelemetryLimits.MaxNamesPerSession ? ClientTelemetryRejection.TooManyNames : Check(batch);

        if (rejection != null)
            return new TelemetryBatch { Names = names, NameSlots = slots, Rejection = rejection };

        return new TelemetryBatch
        {
            Names = names,
            NameSlots = slots,
            Samples = (batch.Samples ?? []).Select(ReadSample).ToArray(),
            Errors = (batch.Errors ?? []).Select(ReadError).OfType<TelemetryError>().ToArray(),
            Marks = (batch.Marks ?? []).Where(mark => mark.Name != null && TelemetryNames.IsValidName(mark.Name)).Select(ReadMark).ToArray()
        };
    }

    public static TelemetryDevice ParseDevice(SharedClientTelemetry.Device? device)
    {
        return TelemetryDeviceParser.Parse(device);
    }

    public static TelemetrySessionTags ParseTags(string[]? tags)
    {
        var valid = (tags ?? []).Where(tag => tag != null && TelemetryNames.IsValidName(tag)).Distinct().ToArray();
        var accepted = valid.Take(ClientTelemetryLimits.MaxTagsPerSession).ToArray();

        return new TelemetrySessionTags(accepted, (tags?.Length ?? 0) - accepted.Length);
    }

    // A wrong id drops the whole: it is the only thing that ties it to a session. Anything else of a wrong form is dropped alone.
    public static TelemetryPreviousParsing ParsePrevious(SharedClientTelemetry.PreviousSession? previous)
    {
        if (previous == null)
            return new TelemetryPreviousParsing(null, 0);

        if (previous.SessionId == Guid.Empty)
            return new TelemetryPreviousParsing(null, 1);

        var tags = ParseTags(previous.Tags);
        var marks = previous.Marks ?? [];
        var valid = marks.Where(mark => mark != null && mark.Time >= 0 && mark.Time <= ClientTelemetryLimits.MaxPreviousMs && mark.Name != null && TelemetryNames.IsValidName(mark.Name))
                         .Select(mark => new TelemetryMark(mark.Time, mark.Name))
                         .ToArray();
        var kept = valid.Skip(Math.Max(0, valid.Length - ClientTelemetryLimits.MaxPreviousMarks)).ToArray();
        var lastSample = PreviousTime(previous.LastSampleTime);
        var since = PreviousTime(previous.SinceMs);
        var rejected = tags.Rejected + (marks.Length - kept.Length) + (lastSample == null && previous.LastSampleTime != -1 ? 1 : 0) + (since == null && previous.SinceMs != -1 ? 1 : 0);

        return new TelemetryPreviousParsing(new TelemetryPreviousSession(previous.SessionId, tags.Accepted, lastSample, since, kept), rejected);
    }

    private static long? PreviousTime(long time)
    {
        return time >= 0 && time <= ClientTelemetryLimits.MaxPreviousMs ? time : null;
    }

    private static ClientTelemetryRejection? Check(SharedClientTelemetry.Batch batch)
    {
        var samples = batch.Samples ?? [];
        var errors = batch.Errors ?? [];
        var marks = batch.Marks ?? [];

        if (samples.Length > ClientTelemetryLimits.MaxSamplesPerBatch)
            return ClientTelemetryRejection.TooManySamples;

        if (errors.Length > ClientTelemetryLimits.MaxErrorsPerBatch)
            return ClientTelemetryRejection.TooManyErrors;

        if (marks.Length > ClientTelemetryLimits.MaxMarksPerBatch)
            return ClientTelemetryRejection.TooManyMarks;

        if (samples.Contains(null) || errors.Contains(null) || marks.Contains(null))
            return ClientTelemetryRejection.Malformed;

        if (samples.Any(sample => (sample.Values?.Length ?? 0) > ClientTelemetryLimits.MaxNamesPerSession))
            return ClientTelemetryRejection.TooManyValues;

        if (samples.Any(sample => sample.Time < 0 || (sample.Values ?? []).Any(double.IsInfinity))
            || errors.Any(error => error.Time < 0 || error.Repeat < 1 || error.Message == null)
            || marks.Any(mark => mark.Time < 0 || mark.DurationMs < 0))
            return ClientTelemetryRejection.Malformed;

        if (errors.Any(error => error.Message.Length > ClientTelemetryLimits.MaxMessageLength))
            return ClientTelemetryRejection.MessageTooLong;

        if (errors.Any(error => error.Stack?.Length > ClientTelemetryLimits.MaxStackLength))
            return ClientTelemetryRejection.StackTooLong;

        return Bytes(samples, errors, marks) > ClientTelemetryLimits.MaxBatchBytes ? ClientTelemetryRejection.BatchTooLarge : null;
    }

    // The estimate of the message on the wire: the reader of the socket has let it through and is gone by now.
    private static long Bytes(SharedClientTelemetry.Sample[] samples, SharedClientTelemetry.Error[] errors, SharedClientTelemetry.Mark[] marks)
    {
        return samples.Sum(sample => _rowBytes + (long)(sample.Values?.Length ?? 0) * _valueBytes)
               + errors.Sum(error => _rowBytes + (long)error.Message.Length + (error.Stack?.Length ?? 0))
               + marks.Sum(mark => _rowBytes + (long)(mark.Name?.Length ?? 0) + Math.Min(mark.Detail?.Length ?? 0, ClientTelemetryLimits.MaxMarkDetailLength));
    }

    private static IReadOnlyList<TelemetryMetricName> ReadNames(SharedClientTelemetry.MetricName[]? names)
    {
        var accepted = new List<TelemetryMetricName>();

        for (var slot = 0; slot < (names?.Length ?? 0); slot++)
        {
            var name = names![slot];

            if (name?.Name != null && TelemetryNames.IsValidName(name.Name))
                accepted.Add(new TelemetryMetricName(slot, name.Name, name.Unit != null && TelemetryNames.IsValidUnit(name.Unit) ? name.Unit : null));
        }

        return accepted;
    }

    private static TelemetrySample ReadSample(SharedClientTelemetry.Sample sample)
    {
        var values = (sample.Values ?? []).Select(value => double.IsNaN(value) ? (double?)null : value).ToArray();

        return new TelemetrySample(sample.Time, values, sample.Hidden);
    }

    // A detail that is not a text is dropped alone. Cut before the scrubbing, so its cost does not depend on the client, and after it.
    private static TelemetryMark ReadMark(SharedClientTelemetry.Mark mark)
    {
        var detail = string.IsNullOrWhiteSpace(mark.Detail) || mark.Detail.Any(char.IsControl)
            ? null
            : CutDetail(Scrubber.Scrub(CutDetail(mark.Detail)));

        return new TelemetryMark(mark.Time, mark.Name, detail, mark.DurationMs);
    }

    private static string CutDetail(string detail)
    {
        return detail.Length <= ClientTelemetryLimits.MaxMarkDetailLength ? detail : detail[..ClientTelemetryLimits.MaxMarkDetailLength];
    }

    // Plain logs are not sent: a level outside the closed list has no way into the storage.
    private static TelemetryError? ReadError(SharedClientTelemetry.Error error)
    {
        var level = error.Level switch
        {
            SharedClientTelemetry.ErrorLevel.Error => TelemetryErrorLevel.Error,
            SharedClientTelemetry.ErrorLevel.Exception => TelemetryErrorLevel.Exception,
            SharedClientTelemetry.ErrorLevel.Warning => TelemetryErrorLevel.Warning,
            _ => (TelemetryErrorLevel?)null
        };

        return level == null ? null : new TelemetryError(error.Time, level.Value, Scrubber.Scrub(error.Message), Scrubber.Scrub(error.Stack ?? ""), error.Repeat);
    }
}
