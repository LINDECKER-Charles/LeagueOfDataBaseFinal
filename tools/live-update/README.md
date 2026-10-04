# tools/live-update

Signing of the live update bundles of the Android app (plan, L10.3; ADR 0008). A bundle is the
zip of the `shell-store` build (`dist/shell-store/browser`, `index.html` at its root), published
as a GitHub Release asset and named by the client policy (`client-policy publish --bundle-*`).
`@capawesome/capacitor-live-update` downloads it, then refuses it unless its RSA signature holds
against the public key the app embeds.

| Element     | Value                                                                   |
| ----------- | ----------------------------------------------------------------------- |
| Signature   | RSA PKCS#1 v1.5 over SHA-256 of the zip (`SHA256withRSA`), in base64    |
| Keys        | RSA 2048 to 4096 bits; 4096 signs in 684 characters, the API keeps 1024 |
| Private key | `LODB_LIVE_UPDATE_PRIVATE_KEY`: the PEM, or the PEM in base64           |
| Public key  | `LODB_LIVE_UPDATE_PUBLIC_KEY`: X.509 PEM (`BEGIN PUBLIC KEY`)           |
| Checksum    | SHA-256 of the zip, lower case hexadecimal (the API requires it)        |

The keys come from the environment only, never from a file of the repository. The tests
generate throwaway keys at each run; none is committed, not even a test one.

## Key

Generated once, on the release host, outside the checkout (the guide
`docs/guides/release-android.md` covers its backup):

```sh
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:4096 -out /secure/lodb-live-update.pem
LODB_LIVE_UPDATE_PRIVATE_KEY="$(cat /secure/lodb-live-update.pem)" \
  node tools/live-update/public-key.mjs > lodb-live-update.pub.pem
```

The public key goes into the app's build (the `publicKey` of the `LiveUpdate` plugin in
`capacitor.config.ts`). Every copy of the app carries it: changing the key means a native
release, and bundles signed with the new key only reach the shells that embed it.

## Sign

```sh
LODB_LIVE_UPDATE_PRIVATE_KEY="$(cat /secure/lodb-live-update.pem)" \
  node tools/live-update/sign-bundle.mjs --zip lodb-bundle-2.4.0.zip --id 2.4.0 \
    --url https://github.com/<owner>/<repo>/releases/download/android-v2.4.0/lodb-bundle-2.4.0.zip \
    --minimum-native 2.4.0 --out lodb-bundle-2.4.0.json
```

Writes the descriptor (`id`, `url`, `checksum`, `signature`, `minimumNativeVersion`) after
checking the signature against the key's public half. It refuses what the API or the app would
refuse: the id `public` (the plugin's embedded bundle), an id beyond 64 characters or outside
`[A-Za-z0-9._-]`, a URL other than `https`, a minimum that is not `X.Y.Z`.

`--minimum-native` is the oldest shell whose native plugins this front works with. The app
ignores a bundle whose minimum is above its own version: it waits for the native update.

## Verify

```sh
LODB_LIVE_UPDATE_PUBLIC_KEY="$(cat lodb-live-update.pub.pem)" \
  node tools/live-update/verify-bundle.mjs --zip lodb-bundle-2.4.0.zip \
    --descriptor lodb-bundle-2.4.0.json
```

Checks the zip as the device will: checksum, then signature. The release runs it on the asset
downloaded back from the GitHub Release.

## Tests

```sh
node --test tools/live-update/test/*.test.mjs
```

Cross-check with `openssl dgst -sha256 -verify` when `openssl` is present: the same scheme as
Java's `SHA256withRSA`, which the plugin uses on the device.
