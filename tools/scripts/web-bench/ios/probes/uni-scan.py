#!/usr/bin/env python3
"""Scans the result of the uni probe (probe.json of `probe-run.py --probe uni`) for uniforms that became all zeros.

For every dump (t4, t8, ...) prints the indexes of the programs (in the order of their first useProgram) whose uniforms
matching the regex are all zeros; the dumps that failed are printed with their error. The default regex is the row the
iPadOS 18.2 bug zeroes: glstate_matrix_projection[3] / unity_MatrixVP[3] (Unity names them hlslcc_mtx4x4...).

Usage: uni-scan.py <probe.json> [--uniform '<regex>']
Example: uni-scan.py /tmp/uni1/probe.json --uniform 'unity_ObjectToWorld\\[3\\]$'
"""
import argparse
import json
import re
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("probe_json")
    parser.add_argument("--uniform", default=r"(glstate_matrix_projection|unity_MatrixVP)\[3\]$")
    args = parser.parse_args()
    pattern = re.compile(args.uniform)
    with open(args.probe_json) as f:
        dumps = json.load(f)
    if not isinstance(dumps, dict):
        sys.exit(f"error: {args.probe_json} is not the result of the uni probe (an object of dumps)")

    def order(name):
        return int(re.sub(r"\D", "", name) or 0)

    for name in sorted(dumps, key=order):
        dump = dumps[name]
        if not name.startswith("t"):
            print(name, dump)
            continue
        matched = 0
        zero = []
        for index, uniforms in enumerate(dump):
            values = [v for k, v in uniforms.items() if pattern.search(k)]
            if values:
                matched += 1
            if values and all(isinstance(v, list) and not any(v) for v in values):
                zero.append(index)
        print(f"{name:>4} programs={len(dump)} with the uniform={matched} zero={zero}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
