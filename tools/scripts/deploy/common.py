# Exit codes of the script and the one error it reports.
SERVICE = 1  # ssh, git, an unreachable store or panel, 5xx, a query the store rejects (400), an answer that is not JSON
USAGE = 2  # argparse uses the same code: arguments, an unknown project, a missing config
AUTH = 3  # 401, 403 of a Coolify panel
NOT_FOUND = 4  # no spans of the trace
FINDINGS = 5  # registry verify found differences


class Failure(Exception):
    def __init__(self, code, message):
        super().__init__(message)
        self.code = code
        self.message = message
