<!--
  Thanks for contributing to League of Database!
  PRs target the `dev` branch, not `main`. See CONTRIBUTING.md.
-->

## 📝 Description

<!-- Briefly describe the changes and the motivation. Link related issues (e.g. Closes #123). -->

## 🔗 Type of change

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that breaks existing behavior)
- [ ] Documentation / chore

## 🧪 Tests & guardrails

<!-- Must be green before requesting review (same as CI). -->

- [ ] `docker compose exec -T php php vendor/bin/phpunit tests/Unit`
- [ ] `npm test` · `npm run typecheck` · `npm run build` (from `app/`)
- [ ] Added or updated tests proving the change works

## ✅ Checklist

- [ ] This PR targets the `dev` branch (not `main`)
- [ ] Code follows the project conventions (`CLAUDE.md`)
- [ ] Architecture invariants preserved (egress via go-fetcher, DB-less storage, `AbstractManager` / `AbstractResourceController`)
- [ ] Documentation updated if needed
- [ ] No new warnings introduced

## 📸 Screenshots (if applicable)
