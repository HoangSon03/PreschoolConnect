---
name: feature-delivery
description: "Deliver a product feature, API/UI change, or bug fix in PreschoolConnect. Use for implementation tasks, not read-only audits; also load database-safety when persistence, migrations, tenant isolation, or auditing is affected."
---

# Feature delivery

Follow the project rules in `AGENTS.md`; they are authoritative.

1. Inspect only task-relevant instructions, files, dependencies, and existing changes. Do not run a repository-wide audit; expand scope only when necessary.
2. Confirm the request is within approved product scope. Treat proposals and unresolved rules as decisions to resolve, not requirements to invent.
3. Trace the relevant implementation and tests. Make the smallest complete change using existing patterns. For persistence, migrations, tenant isolation, or auditing, also follow `database-safety`.
4. Preserve server-side school/student authorization and use synthetic data.
5. Update the existing canonical docs for meaningful code, API, configuration, or behavior changes. Do not create duplicate documentation.
6. Run proportionate relevant tests/builds, review the changed-file diff, and finish with the mandatory report below.

## Mandatory final report

After every completed feature implementation or issue fix, use exactly these four sections, in this order:

Write the report in Vietnamese, including section headings and explanations. Preserve file paths, identifiers, and commands as written.

1. **Tệp đã thay đổi**: List each file modified or created and briefly explain its changes.
2. **Tệp đã kiểm tra**: List the relevant files actually read or examined and briefly explain their relevance.
3. **Lý do thay đổi**: Explain why the main changes were necessary and how they satisfy the requested requirements.
4. **Xác minh**: List tests, builds, or checks actually executed and their results. Explicitly identify relevant checks not run or skipped.

Report only actual activity; never fabricate file inspection, changes, or verification.
Group related files where appropriate while identifying each file. Keep explanations concise, understandable, and proportional to the task.
Do not include remaining issues, risks, recommendations, follow-up tasks, or a repeated implementation plan.
This format applies to feature implementations and issue fixes, not ordinary conversational responses.
