# Exit codes of the script and the one error it reports.
USAGE = 2  # argparse uses the same code: arguments, the section of the project, the env file
SERVICE = 1  # unreachable host, a network timeout, 5xx, 400, a redirect, an answer that is not JSON
AUTH = 3  # 401, 403
NOT_FOUND = 4  # no such session
WAIT_TIMEOUT = 5  # no new session in the time of monitor


class Failure(Exception):
    def __init__(self, code, message):
        super().__init__(message)
        self.code = code
        self.message = message
