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

- [ ] `dotnet build LoDb.slnx -c Release` · `dotnet test LoDb.slnx`
- [ ] `npm --prefix src/LoDb.Web run lint` · `typecheck` · `test` · `build:web` · `api:check`
- [ ] Added or updated tests proving the change works

## ✅ Checklist

- [ ] This PR targets the `dev` branch (not `main`)
- [ ] Code follows the project conventions (`CLAUDE.md`, section "Nouvelle stack")
- [ ] Architecture invariants preserved (single `LoDb.Api` host, egress through the `ddragon` client, atomic blob writes, items and spells indexed by id, SSR without cookies or secrets)
- [ ] Nothing changed under `legacy/` (archived stack)
- [ ] Documentation updated if needed
- [ ] No new warnings introduced

## 📸 Screenshots (if applicable)
