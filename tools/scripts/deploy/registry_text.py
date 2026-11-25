# The registry for an agent: one project in full, all projects as a table, the findings of registry verify.


def project(name, project, servers):
    if project.get("deployed", True) is False:
        return [f"{name}: not deployed. {project.get('note', '')}".rstrip()]

    server = servers[project["server"]]
    stack = server["observability"]
    coolify = project["coolify"]
    lines = [
        f"{name}: server {project['server']} ({server['provider']}, {server['city']}, {server['ip']}), mode {project['mode']},"
        f" namespace {project['namespace']}, framework {'yes' if project['framework'] else 'no'}",
        f"ssh {server['ssh']} (user {server['ssh_user']}, docker: {server['docker']}), MCP {server['mcp']}, panel {server['panel']}",
        f"coolify project {coolify['project']} '{coolify['name']}' / {coolify['environment']}",
    ]
    lines += [f"  {resource['role']}: {resource['type']} {resource['uuid']}" + (f" ({resource['name']})" if resource.get("name") else "")
              for resource in project["resources"]]
    lines.append(f"repository {project['repository']}, branch {project['branch']}")
    lines.append("domains: " + (", ".join(f"{role} {_domains(value)}" for role, value in project["domains"].items()) or "none"))
    lines.append(f"logs {stack['logs']}, traces {stack['traces']} on the host ({stack['access']} over ssh {server['ssh']});"
                 f" stack application {stack['coolify']['application']}")

    if project.get("note"):
        lines.append(f"note: {project['note']}")

    return lines


def table(registry):
    rows = [("project", "server", "mode", "namespace", "coolify project", "client")]

    for name, project in registry["projects"].items():
        if project.get("deployed", True) is False:
            rows.append((name, "-", project["mode"], project["namespace"], "not deployed", "-"))
            continue

        client = project["domains"].get("client", "-")
        rows.append((name, project["server"], project["mode"], project["namespace"], project["coolify"]["project"], _domains(client)))

    widths = [max(len(row[column]) for row in rows) for column in range(len(rows[0]))]

    return ["  ".join(cell.ljust(width) for cell, width in zip(row, widths)).rstrip() for row in rows]


def verify(report):
    checked = ", ".join(f"{key} {count} uuids" for key, count in report.checked.items())

    if not report.findings:
        return [f"registry matches Coolify: {checked}"]

    return [f"{len(report.findings)} differences ({checked} checked):", *(f"  {finding}" for finding in report.findings)]


def _domains(value):
    return " ".join(value) if isinstance(value, list) else value
