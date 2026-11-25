# registry verify: the registry against the lists of both Coolify panels. Pure: the answers come in, the findings go out.
_KIND = {"projects": "project", "applications": "application", "services": "service", "databases": "database"}


class Report:
    def __init__(self):
        self.findings = []
        self.checked = {}


def compare(registry, answers):
    # answers: server key -> (panel origin, {list name: [items]}).
    report = Report()
    on_panel = {key: _index(lists) for key, (_, lists) in answers.items()}

    for key, server in registry["servers"].items():
        origin, _ = answers[key]

        if origin.rstrip("/") != server["panel"].rstrip("/"):
            report.findings.append(f"{key}: the panel is {server['panel']} in the registry, {origin} in the MCP config of {server['mcp']}")

        expected = _expected(registry, key)
        report.checked[key] = len(expected)

        for uuid, (kind, name, owner) in expected.items():
            _check(report, on_panel, key, uuid, kind, name, owner)

        for uuid, (kind, name) in on_panel[key].items():
            if uuid not in expected:
                report.findings.append(f"{key}: {kind} {uuid} '{name}' is in Coolify, not in the registry")

    return report


def _expected(registry, key):
    # uuid -> (kind, expected name or None, who lists it).
    stack = registry["servers"][key]["observability"]["coolify"]
    expected = {stack["project"]: ("project", stack["name"], "servers." + key),
                stack["application"]: ("application", None, "servers." + key)}

    for name, project in registry["projects"].items():
        if project.get("deployed", True) is False or project["server"] != key:
            continue

        expected[project["coolify"]["project"]] = ("project", project["coolify"]["name"], name)

        for resource in project["resources"]:
            expected[resource["uuid"]] = (resource["type"], None, f"{name} ({resource['role']})")

    return expected


def _check(report, on_panel, key, uuid, kind, name, owner):
    found = on_panel[key].get(uuid)

    if found is None:
        elsewhere = [other for other, index in on_panel.items() if uuid in index]
        where = f", it is on {elsewhere[0]}" if elsewhere else ""
        report.findings.append(f"{key}: {kind} {uuid} of {owner} is not in Coolify{where}")
    elif found[0] != kind:
        report.findings.append(f"{key}: {uuid} of {owner} is {found[0]} in Coolify, {kind} in the registry")
    elif name is not None and found[1] != name:
        report.findings.append(f"{key}: project {uuid} of {owner} is '{found[1]}' in Coolify, '{name}' in the registry")


def _index(lists):
    return {item["uuid"]: (_KIND[list_name], item.get("name")) for list_name, items in lists.items() for item in items}
