# The end of an editor: save, exit, and the windows a crash leaves behind.
import json
import os
import time

from bridge import bridge_command
from common import alive, kill_now, processes

EXIT_TIMEOUT = 30
SAVE_TIMEOUT = 30
SAVE_CODE = """
if (UnityEditor.EditorApplication.isPlaying) { UnityEditor.EditorApplication.isPlaying = false; return "left play"; }
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
UnityEditor.AssetDatabase.SaveAssets();
return "saved";
"""


def exit_through_bridge(port, pid):
    try:
        bridge_command(port, {"type": "execute_code", "params": {"action": "execute", "code": "UnityEditor.EditorApplication.Exit(0); return 1;", "safety_checks": False}}, 10)
    except (OSError, ValueError, ConnectionError):
        pass    # the connection ends with the process
    deadline = time.time() + EXIT_TIMEOUT
    while time.time() < deadline:
        if not alive(pid):
            return True
        time.sleep(0.5)
    return False


def bug_reporters(client):
    # The reporter of a crash is a process of its own with the path of the project in its arguments.
    return [proc for proc in processes() if "bugreporter" in os.path.basename(proc.args[0]).lower() and any(str(client) in arg for arg in proc.args)]


def close_bug_reporters(client):
    reporters = [proc.pid for proc in bug_reporters(client)]
    for pid in reporters:
        kill_now(pid)
    return reporters


def save_open_work(port):
    # A failure is an answer, not an error: the editor is stopped either way, and the result says what was not saved.
    try:
        response = bridge_command(port, {"type": "execute_code", "params": {"action": "execute", "code": SAVE_CODE, "safety_checks": False}}, SAVE_TIMEOUT)
    except (OSError, ValueError, ConnectionError) as error:
        return f"not saved: {error}"
    return json.dumps(response, ensure_ascii=False)[:300]
