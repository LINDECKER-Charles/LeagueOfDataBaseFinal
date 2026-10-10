# tools/ci

Checks of the GitHub Actions workflows themselves, run by the `workflows` job of
`.github/workflows/ci.yml` next to `actionlint`.

## Actions pinned to a commit

```sh
node tools/ci/check-pinned-actions.mjs            # the repository's workflows
node tools/ci/check-pinned-actions.mjs --root DIR # another checkout
```

Every `uses:` of `.github/workflows/*.yml` and `.github/actions/**/action.yml` must read:

| Reference | Form |
| --- | --- |
| Remote action or reusable workflow | `owner/repo[/path]@<40 hex> # vX.Y.Z` |
| Local action or reusable workflow | `./…`, as is: code of this repository |
| Container image | `docker://image:tag@sha256:<64 hex>` |

A tag can be moved by the owner of the action; a commit cannot. The version in the comment is
for the reader, and Dependabot (`.github/dependabot.yml`, `github-actions`) bumps it with the
SHA. A `uses:` the script cannot parse on one line fails too, rather than being skipped. The
archived workflows of `legacy/` never run and are not read.

To pin a new action, resolve the release the major tag points to, dereferencing an annotated
tag:

```sh
gh api repos/OWNER/REPO/commits/vX.Y.Z --jq .sha
```

## actionlint

The `workflows` job downloads the actionlint release named in its `env`, and checks the
archive against the SHA-256 of the release's `checksums.txt` before running it. To bump it,
change both values together. Locally:

```sh
docker run --rm -v "$PWD:/repo" -w /repo \
  rhysd/actionlint:1.7.12@sha256:b1934ee5f1c509618f2508e6eb47ee0d3520686341fec936f3b79331f9315667
```

## Tests

```sh
node --test tools/ci/test/*.test.mjs
```
