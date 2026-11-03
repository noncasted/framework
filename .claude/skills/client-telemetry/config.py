# Where to read from: the arguments, then the section "## client-telemetry" of .claude/project-skills.md.
# The env file is data: only the line of the token is parsed, nothing is sourced, expanded or printed.
import os
import re
import urllib.parse
from pathlib import Path

from common import USAGE, Failure

SECTION = "## client-telemetry"
TOKEN_NAME = "CONSOLE_TOKEN"
_LOCAL_HOSTS = ("127.0.0.1", "localhost", "::1")
_KEY_LINE = re.compile(r"^\s*(?:[-*]\s+)?(url|env-file):\s*`?([^`\s]+)`?\s*$")
_TOKEN_LINE = re.compile(r"^\s*(?:export\s+)?" + TOKEN_NAME + r"\s*=(.*)$")
_TOKEN_VALUE = re.compile(r"^[\x21-\x7e]+$")


class Settings:
    def __init__(self, url, env_file):
        self.url = url
        self.env_file = env_file


def resolve(url_argument, env_file_argument):
    section = {} if url_argument and env_file_argument else _read_section()
    url = url_argument or section.get("url")
    env_file = env_file_argument or section.get("env-file")

    for name, value in (("url", url), ("env-file", env_file)):
        if not value:
            raise Failure(USAGE, f"no {name} in the section {SECTION} of .claude/project-skills.md, and no --{name}")

    return Settings(_check_url(url), Path(env_file).expanduser())


def read_token(env_file):
    try:
        text = env_file.read_bytes().decode("utf-8")
    except OSError as error:
        raise Failure(USAGE, f"cannot read the env file {env_file}: {error.strerror}") from None
    except UnicodeDecodeError:
        raise Failure(USAGE, f"the env file {env_file} is not UTF-8") from None

    value = None

    for line in text.splitlines():
        match = _TOKEN_LINE.match(line)

        if match:
            value = _unquote(match.group(1).strip())

    if not value:
        raise Failure(USAGE, f"no {TOKEN_NAME} in the env file {env_file}")

    if not _TOKEN_VALUE.match(value):
        raise Failure(USAGE, f"{TOKEN_NAME} in the env file {env_file} has characters a header cannot carry (the value is not shown)")

    return value


def _read_section():
    project = os.environ.get("CLAUDE_PROJECT_DIR")

    if not project:
        raise Failure(USAGE, "CLAUDE_PROJECT_DIR is not set: run from a project or pass --url and --env-file")

    path = Path(project) / ".claude" / "project-skills.md"

    try:
        lines = path.read_text(encoding="utf-8").splitlines()
    except OSError as error:
        raise Failure(USAGE, f"cannot read {path}: {error.strerror}") from None

    if SECTION not in (line.strip() for line in lines):
        raise Failure(USAGE, f"no section {SECTION} in {path}")

    start = [line.strip() for line in lines].index(SECTION) + 1
    values = {}

    for line in lines[start:]:
        if line.startswith("## "):
            break

        match = _KEY_LINE.match(line)

        if match:
            values[match.group(1)] = match.group(2)

    return values


def _check_url(url):
    parsed = urllib.parse.urlparse(url)

    if parsed.scheme == "https" and parsed.hostname:
        return url.rstrip("/")

    if parsed.scheme == "http" and parsed.hostname in _LOCAL_HOSTS:
        return url.rstrip("/")

    raise Failure(USAGE, f"the url {url} must be https (http only on this machine): the token goes in its header")


def _unquote(value):
    if value[:1] in ('"', "'"):
        end = value.find(value[0], 1)

        return value[1:end] if end > 0 else ""

    parts = value.split()

    return parts[0] if parts else ""
