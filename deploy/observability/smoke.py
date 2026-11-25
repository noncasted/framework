#!/usr/bin/env python3
"""Smoke test of the observability stack: sends one OTLP/HTTP protobuf log and one span, then finds them.

Usage (stdlib only):
  smoke.py --logs http://127.0.0.1:9428 --traces http://127.0.0.1:10428
      sends straight to the stores (the per-signal endpoints of the contract), e.g. through the ssh tunnel
  smoke.py --logs http://victorialogs:9428 --traces http://victoriatraces:10428 --ingest http://otlp:4318
      sends through the router; run inside the network `observability` (README.md)

Exits non-zero when a record is not found in 60 s.
"""
import argparse
import json
import os
import struct
import sys
import time
import urllib.parse
import urllib.request

STREAM_FIELDS = "service.namespace,service.name,deployment.environment"


def varint(n):
    out = b""
    while True:
        low, n = n & 0x7F, n >> 7
        if not n:
            return out + bytes([low])
        out += bytes([low | 0x80])


def field(number, payload):
    return varint(number << 3 | 2) + varint(len(payload)) + payload


def number(number_, value):
    return varint(number_ << 3) + varint(value)


def fixed64(number_, value):
    return varint(number_ << 3 | 1) + struct.pack("<Q", value)


def text(number_, value):
    return field(number_, value.encode())


def attribute(key, value):
    return text(1, key) + field(2, text(1, value))


def build(trace_id, span_id, marker):
    now = time.time_ns()
    resource = b"".join(field(1, attribute(k, v)) for k, v in [
        ("service.namespace", "smoke-test"), ("service.name", "smoke"),
        ("deployment.environment", "test"), ("service.instance.id", marker)])
    scope = text(1, "Observability.Smoke")
    record = (fixed64(1, now) + number(2, 9) + text(3, "Information") + field(5, text(1, f"smoke {marker}"))
              + field(9, trace_id) + field(10, span_id))
    span = (field(1, trace_id) + field(2, span_id) + text(5, f"smoke {marker}") + number(6, 1)
            + fixed64(7, now - 1_000_000) + fixed64(8, now))
    logs = field(1, field(1, resource) + field(2, field(1, scope) + field(2, record)))
    spans = field(1, field(1, resource) + field(2, field(1, scope) + field(2, span)))
    return logs, spans


def post(url, body):
    request = urllib.request.Request(url, data=body, headers={"Content-Type": "application/x-protobuf"})
    with urllib.request.urlopen(request, timeout=10) as response:
        print(f"POST {url} -> {response.status}")


def query(base, logsql):
    body = urllib.parse.urlencode({"query": logsql}).encode()
    with urllib.request.urlopen(f"{base}/select/logsql/query", data=body, timeout=10) as response:
        return [json.loads(line) for line in response.read().decode().splitlines() if line]


def wait(name, base, logsql):
    deadline = time.time() + 60
    while time.time() < deadline:
        rows = query(base, logsql)
        if rows:
            print(f"{name}: found {json.dumps(rows[0])[:300]}")
            return True
        time.sleep(3)
    print(f"{name}: NOT FOUND in 60 s by {logsql}")
    return False


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--logs", required=True, help="VictoriaLogs base URL")
    parser.add_argument("--traces", required=True, help="VictoriaTraces base URL")
    parser.add_argument("--ingest", help="router base URL; without it the stores are written directly")
    args = parser.parse_args()

    trace_id, span_id = os.urandom(16), os.urandom(8)
    marker = trace_id.hex()[:12]
    logs, spans = build(trace_id, span_id, marker)
    if args.ingest:
        post(f"{args.ingest}/v1/logs", logs)
        post(f"{args.ingest}/v1/traces", spans)
        post(f"{args.ingest}/v1/metrics", b"")
    else:
        post(f"{args.logs}/insert/opentelemetry/v1/logs?_stream_fields={STREAM_FIELDS}", logs)
        post(f"{args.traces}/insert/opentelemetry/v1/traces", spans)

    found_log = wait("log", args.logs,
                     f'_time:10m {{service.namespace="smoke-test", service.name="smoke"}} trace_id:="{trace_id.hex()}"')
    found_span = wait("span", args.traces, f'_time:10m trace_id:="{trace_id.hex()}" name:*')
    found_trace = wait_trace(args.traces, trace_id.hex())
    sys.exit(0 if found_log and found_span and found_trace else 1)


def wait_trace(base, trace_hex):
    # The Jaeger API sees a trace only after -insert.indexFlushInterval (20 s by default).
    url = f"{base}/select/jaeger/api/traces/{trace_hex}"
    deadline = time.time() + 60
    while time.time() < deadline:
        with urllib.request.urlopen(url, timeout=10) as response:
            traces = json.loads(response.read())["data"]
        if traces:
            print(f"trace: {url} -> {len(traces[0]['spans'])} span(s)")
            return True
        time.sleep(3)
    print(f"trace: NOT FOUND in 60 s at {url}")
    return False


if __name__ == "__main__":
    main()
