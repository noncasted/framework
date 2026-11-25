# The LogsQL of the commands. Field names are the ones VictoriaLogs v1.54 / VictoriaTraces v0.13 give OTLP data
# (deploy/observability/CLAUDE.md); the project filter is added by the store (api.py), not here.

# OTLP severity numbers: 13-16 warn, 17-20 error, 21-24 fatal. The number, not the text: exporters spell the text differently.
WARNINGS_AND_ERRORS = "severity_number:>=13"
EXAMPLE_FIELDS = ("_time", "_msg", "trace_id", "exception.type", "exception.message")
ALL = "*"


def errors(limit):
    # The message with its numbers (ids, hex, durations) collapsed to <N> is the template a group is counted by.
    return (f"{WARNINGS_AND_ERRORS} | format \"<_msg>\" as template | collapse_nums at template prettify"
            f" | stats by (service.name, severity_text, template) count() hits, max(_time) last,"
            f" row_max({', '.join(EXAMPLE_FIELDS)}) example | sort by (hits desc) | limit {limit}")


def trace_logs(trace_id):
    return f'trace_id:="{trace_id}"'


def trace_spans(trace_id):
    # name:* skips the index rows of VictoriaTraces (trace_id_idx).
    return f'trace_id:="{trace_id}" name:*'
