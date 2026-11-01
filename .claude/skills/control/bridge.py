# The framed protocol of the MCP bridge of an editor.
import json
import socket
import struct


def bridge_command(port, command, timeout):
    # The framed protocol of the bridge: a welcome line, then 8 bytes of big-endian length before each message.
    with socket.create_connection(("127.0.0.1", port), timeout=timeout) as connection:
        connection.settimeout(timeout)
        welcome = b""
        while not welcome.endswith(b"\n"):
            welcome += connection.recv(1)
        payload = json.dumps(command).encode()
        connection.sendall(struct.pack(">Q", len(payload)) + payload)
        header = _read_exact(connection, 8)
        return json.loads(_read_exact(connection, struct.unpack(">Q", header)[0]))


def _read_exact(connection, size):
    data = b""
    while len(data) < size:
        chunk = connection.recv(size - len(data))
        if not chunk:
            raise ConnectionError("the bridge closed the connection")
        data += chunk
    return data
