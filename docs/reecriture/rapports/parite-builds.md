# Parité des pages /b/{token} (L5.3)

- Date : 2026-09-27
- Ancienne stack : http://localhost:8080
- Réécriture : http://localhost:18080
- Commande : `node tools/next/builds-parity/compare.mjs --out docs/reecriture/rapports/parite-builds.md`
- Pages : 11, dont 0 avec un écart

| Jeton | Cas | Statut ancien | Statut nouveau | Écarts |
|---|---|---|---|---|
| `feedbeef0000000000000001` | public sr latest, supporter with a card, three steps, +2 | 200 | 200 | 0 |
| `feedbeef0000000000000002` | public aram latest, no description, -1 | 200 | 200 | 0 |
| `feedbeef0000000000000003` | public arena older, score 0 from two votes | 200 | 200 | 0 |
| `feedbeef0000000000000004` | public nexus_blitz older, no vote | 200 | 200 | 0 |
| `feedbeef0000000000000005` | private sr older, a vote no page shows | 200 | 200 | 0 |
| `feedbeef0000000000000006` | private aram latest, Traditional Chinese | 200 | 200 | 0 |
| `feedbeef0000000000000007` | public, ghost champion | 200 | 200 | 0 |
| `feedbeef0000000000000008` | public older, ghost item between two known ones | 200 | 200 | 0 |
| `feedbeef0000000000000009` | private, ghost keystone | 200 | 200 | 0 |
| `feedbeef0000000000000010` | public older, five steps, multi-line description | 200 | 200 | 0 |
| `feedbeef00000000000000ff` | a token no build holds: 404 on both stacks | 404 | 404 | 0 |
