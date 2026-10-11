# tools/ci

Checks of the supply chain: the GitHub Actions workflows themselves, run by the `workflows`
job of `.github/workflows/ci.yml` next to `actionlint`, and the base images of the
Dockerfiles, run by its `supply-chain` job. Both jobs gate the merge into `test`.

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

GitHub enforces the same rule on its side once the repository setting *Require actions to be
pinned to a full-length commit SHA* (Settings → Actions → General) is on: a workflow that
names an action by tag then fails to start. The check here reports it earlier, in the pull
request, with the file and the line.

## Base images pinned by digest

```sh
node tools/ci/check-pinned-images.mjs            # the repository's Dockerfiles
node tools/ci/check-pinned-images.mjs --root DIR # another checkout
```

Every image a Dockerfile under `docker/` names — `FROM`, `COPY --from=<image>`,
`RUN --mount=…,from=<image>` and the `# syntax=` frontend — must read
`image:tag@sha256:<64 hex>`, the digest of the multi-architecture index. A tag can be moved by
whoever publishes the image, a digest cannot: the CI builds and scans exactly what the pull
request reviewed. The tag stays for the reader and for Dependabot. A stage of the same file
(`FROM build`, `COPY --from=build`) and `scratch` are not images; an image named through a
variable (`FROM node:${VERSION}`) fails, since neither the check nor Dependabot can follow it.

**Bumping.** Dependabot (`.github/dependabot.yml`, `docker`, one grouped PR a week) bumps the
tag and the digest of each `FROM` together, and proposes a new digest of the same tag when its
publisher rebuilds it, which is how the OS security fixes arrive. Majors (.NET, Node, JDK) are
bumped by hand with `global.json` and the CI. By hand, resolve the index digest of the tag and
replace the line's digest:

```sh
docker buildx imagetools inspect node:24-slim --format '{{.Manifest.Digest}}'
```

The `# syntax=` line (`docker/dockerfile`) is not a `FROM`: check it by hand at the same time.
The images that only the workflows run (Trivy, the SBOM generator in `_build.yml`, actionlint
below) are pinned the same way, and bumped by hand.

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
