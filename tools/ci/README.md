# tools/ci

Checks of the supply chain: the GitHub Actions workflows themselves, run by the `workflows`
job of `.github/workflows/ci.yml` next to `actionlint`, and the base images of the
Dockerfiles, run by its `supply-chain` job. Both jobs gate the merge into `test`. The scripts
that tag the images by digest, for `_build.yml` and the promotion to prod, live here too.

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

## npm audit of the production dependencies

```sh
node tools/ci/check-npm-audit.mjs                       # src/LoDb.Web, level high
node tools/ci/check-npm-audit.mjs --report audit.json   # a saved `npm audit --json`
```

Runs `npm audit --omit=dev --package-lock-only --json` in `src/LoDb.Web`: from the lockfile,
nothing installed. Only the production dependencies count: they are bundled into the SSR
server and the browser, while the development ones (build, tests, client generator) never
leave the CI. The E2E package ships nothing and is not audited.

**Blocking, with dated exceptions.** An advisory from `high` up fails the `supply-chain` job,
which gates the merge into `test`, hence every deploy: a known flaw does not reach the site
silently, and the fix is usually a lockfile bump within the ranges already allowed. An
advisory that cannot be fixed yet (a transitive dependency pinned by its parent, no patched
version) must not block the deploys forever: it is accepted in
`tools/ci/npm-audit-allowlist.json`, one entry per advisory and package:

```json
[
  {
    "advisory": "GHSA-xxxx-xxxx-xxxx",
    "package": "example-lib",
    "reason": "Only the build-time CSS inliner reaches it, on our own CSS; fixed with its parent.",
    "expires": "2026-12-31"
  }
]
```

All four fields are required, and `expires` is at most 90 days ahead: past it the entry no
longer applies and the advisory blocks again, so that it is looked at anew. A malformed list
accepts nothing. An entry that names an advisory no longer reported is flagged (warning):
remove it. Only the lockfile decides; `npm audit fix` is not run by the CI.

## Images built, attested and scanned

`_build.yml` resolves `test` to one commit, then builds `api`, `web-ssr` and `nginx` from it:

1. **Push under the commit's SHA only** (`:<sha>`, immutable), with an SBOM and a provenance
   attestation in max mode next to each image (BuildKit; the SBOM generator is pinned by digest
   in the workflow's `env`). The commit is the image's `org.opencontainers.image.revision`
   label. The `runtime` stages are rebuilt without cache (`no-cache-filters`): they take the
   distribution's security fixes of the day (`docker/nginx`, `docker/web-ssr`).
2. **Scan** the pushed digest with Trivy (pinned by digest, `TRIVY_IMAGE`): pulled, saved as
   an archive and handed to the scanner, which gets no registry credential. HIGH and CRITICAL
   vulnerabilities that have a fixed version fail the build: OS packages and the libraries the
   image ships (`.deps.json` of .NET, `package.json` of Node).
3. **Tag `:staging`** once the three scans passed, from the scanned digests
   (`tag-image.sh`): the moving tag never names a mix of two builds nor an unscanned image.
   The digests are the workflow's outputs.

```sh
bash tools/ci/tag-image.sh ghcr.io/<owner>/lodb/api sha256:<digest> prod latest
```

`tag-image.sh` creates the tags from `<repository>@<digest>` (`docker buildx imagetools
create` copies the index as it is, attestations included), then fails unless each tag names
that digest.

**A finding that blocks.** Rebuild first: a newer base digest (Dependabot) or the fixes of the
day often clear it. If the fix cannot ship yet (a library pinned by a dependency, a base image
not rebuilt), accept it in `.trivyignore` at the repository root, one line per vulnerability,
with an expiry date at most 30 days ahead (an image's fixes come with its base's next rebuild,
weeks, where a transitive npm fix waits for its parent's release) and the reason above it:

```text
# libfoo in the nginx base: fixed upstream, image not rebuilt yet; not reachable (module unused).
CVE-2026-12345 exp:2026-11-10
```

Past its date the line no longer applies, and the scan blocks again. `.trivyignore` is one of
the `stack-paths` of `ci.yml`: pushing it alone runs the pipeline, hence a new build and scan.
To read a report locally,
build the image, then (Git Bash: `MSYS_NO_PATHCONV=1`):

```sh
docker save --output image.tar lodb-api:local
docker run --rm -v "$PWD/image.tar:/scan/image.tar:ro" -v "$PWD:/repo:ro" -w /repo \
  ghcr.io/aquasecurity/trivy:0.74.0@sha256:62b1e65e8869bc4b4c6aa4fa2b21595256c7c2f6018a9d9ad61caf87187c1969 \
  image --input /scan/image.tar --scanners vuln --severity HIGH,CRITICAL --ignore-unfixed
```

**Bumping Trivy or the SBOM generator.** Pick a release at least two weeks old, resolve its
digest (`docker buildx imagetools inspect <image>:<version> --format '{{.Manifest.Digest}}'`),
and change the version and the digest together in `_build.yml` (and above). For Trivy, check
the same digest on `ghcr.io/aquasecurity/trivy` and `aquasec/trivy`: both registries receive
the same release.

## Promotion to prod, by digest

After a deploy of staging that passed its smoke tests, the `staging-validated` job of `ci.yml`
points `:staging-validated` at the digests `_build.yml` produced. The push of `main` then runs
`_promote.yml`:

```sh
IMAGE_PREFIX=ghcr.io/<owner>/lodb bash tools/ci/promote-images.sh   # TARGET_TAGS="prod latest"
```

From a full-history checkout of the promoted commit, `promote-images.sh` refuses unless:

- for each image, `:staging` names the digest `:staging-validated` names (a build pushed
  since, or a staging deploy that failed or still runs, stops it);
- the three images carry one revision (their `org.opencontainers.image.revision` label);
- that revision holds the code of the promoted commit on the paths `ci.yml` watches
  (`stack-paths`, a test keeps both lists equal): otherwise prod would run code staging never
  served. A difference outside them (documentation) does not count.

It then tags `:prod` and `:latest` from the digests themselves (`tag-image.sh`). When it
refuses, wait for staging to be green on the revision to promote, then merge `test` into
`main` again.

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
