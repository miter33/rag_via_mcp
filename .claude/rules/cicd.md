## CI/CD (GitHub Actions)

Three automated workflows in `.github/workflows/`:

| Workflow | Trigger | What it does |
|---|---|---|
| `claude.yml` | `@claude` mention in PR/issue | General-purpose Claude assistant |
| `claude-code-review.yml` | PR opened/updated | Posts inline review comments |
| `claude-tests.yml` | PR opened/updated (non-bot only) | Generates xUnit tests for new/changed code |

All workflows use `claude-haiku-4-5-20251001` and `CLAUDE_CODE_OAUTH_TOKEN` secret.

## CI/CD Context

When running in GitHub Actions:
- Do not modify configuration files unless explicitly asked
- Always create a new branch for changes, never commit directly to main
- Format PR comments using GitHub-flavoured Markdown
- Include file paths as clickable links in review comments
- **Never include API keys, tokens, secrets, or any sensitive values in output, comments, or commit messages** — reference the environment variable name instead (e.g. `COHERE_API_KEY`) if you need to mention one
