---
name: start-cluster
description: Start the backend Aspire cluster. Use when any task requires a running cluster — benchmarks, tests, API calls, telemetry analysis. Also trigger on "start cluster", "start backend", "start server", "run cluster", "запусти кластер", "запусти бэкенд", "запусти сервер".
---

# Start Cluster

```bash
.claude/skills/control/control cluster start
```

It returns when the cluster answers and does nothing when it is already up; do not ask the user first. `status`, `stop`,
`restart`, `rebuild`, `logs` and the recovery: the `control` skill.

The values of the cluster (its project, the launch profile, the health url) are `cluster` of `.claude/control.json`. The ports
and the services of a game are in the notes of that game (`.claude/project-skills.md`, its docs), not in this skill.
