# shellcheck shell=bash disable=SC2154  # endpoint and the helpers: ../scaleway-bucket.sh
# `verifier`, sourced by ../scaleway-bucket.sh: on the VPS, the writer's key puts a probe page
# as the pre-rendering pass will and is refused a listing, then an anonymous HEAD and GET read
# the probe back as nginx will.

readonly PROBE_KEY="pages/$PROBE_EPOCH/_sonde"
readonly PROBE_CONTENT_TYPE='text/html; charset=utf-8'
readonly PROBE_CACHE_CONTROL='public, max-age=3600, s-maxage=31536000'
readonly PROBE_STORAGE_CLASS=ONEZONE_IA
readonly METADATA_LIMIT_BYTES=2048
readonly HASH_LENGTH=43
readonly HTTP_OK=200
# A dropped route to the store fails the read instead of hanging the run.
readonly CONNECT_TIMEOUT_SECONDS=5
readonly READ_TIMEOUT_SECONDS=30
# Different on every answer, so of no use to the reader of the received headers.
readonly VOLATILE_HEADERS='^(date|x-amz-request-id|x-amz-id-2|x-amzn-requestid):'

# Anonymous, over IPv4, from a container: the path nginx's requests take (Docker NAT). The
# host itself may reach Scaleway over IPv6, which the policy does not list.
anonymous_curl() {
  docker run --rm --entrypoint curl "$IMAGE_AWS_CLI" -4 -sS \
    --connect-timeout "$CONNECT_TIMEOUT_SECONDS" --max-time "$READ_TIMEOUT_SECONDS" \
    "$@" </dev/null
}

# The probe's metadata, one `name<TAB>value` per line: the four page headers of the contract
# (ADR 0011), with a policy as long as a real page's, and the build.
probe_metadata() {
  local first second
  # shellcheck disable=SC2046  # seq's numbers are printf's arguments, one each
  first=$(printf "a%.0s" $(seq "$HASH_LENGTH"))
  # shellcheck disable=SC2046
  second=$(printf "b%.0s" $(seq "$HASH_LENGTH"))
  printf 'content-security-policy\t%s\n' "script-src 'self' 'sha256-$first=' 'sha256-$second='; \
object-src 'none'; base-uri 'self'; report-uri /api/csp-reports; report-to csp"
  printf 'content-security-policy-report-only\t%s\n' \
    "default-src 'self'; report-uri /api/csp-reports; report-to csp"
  printf 'reporting-endpoints\t%s\n' 'csp="/api/csp-reports"'
  printf 'link\t%s\n' '<https://ddragon.leagueoflegends.com>; rel=preconnect'
  printf 'lodb-build\t%s\n' sonde
}

metadata_size() {
  local name value total=0
  while IFS=$'\t' read -r name value; do
    total=$((total + ${#name} + ${#value}))
  done < <(probe_metadata)
  echo "$total"
}

write_probe_files() {
  printf '<!doctype html><html lang="fr"><title>Sonde LoDb</title><p>%s</p></html>\n' \
    "$(date -u +%Y-%m-%dT%H:%M:%SZ)" | gzip -n -c >"$probe_dir/page.html.gz"
  probe_metadata | jq_in_image -R -n '[inputs | split("\t") | {(.[0]): .[1]}] | add' \
    >"$probe_dir/metadonnees.json"
}

write_probe() {
  local size
  size=$(metadata_size)
  ((size < METADATA_LIMIT_BYTES)) || fail "metadonnees de la sonde trop longues : $size octets"
  write_probe_files
  retry_while_denied "ecriture de la sonde" s3api_with_volume "$probe_dir" put-object \
    --bucket "$BUCKET" --key "$PROBE_KEY" --body /sonde/page.html.gz \
    --content-type "$PROBE_CONTENT_TYPE" --content-encoding gzip \
    --cache-control "$PROBE_CACHE_CONTROL" --storage-class "$PROBE_STORAGE_CLASS" \
    --metadata file:///sonde/metadonnees.json ||
    fail "ecriture de la sonde refusee : la cle n'est pas celle de lodb-pages-writer, ou ses \
droits (ObjectStorageObjectsWrite sur ce projet) et la politique ne lui ouvrent pas pages/*"
  echo "  ecrite avec la cle de l'ecrivain : gzip, $PROBE_STORAGE_CLASS," \
    "$size octets de metadonnees"
}

# The writer never reads, lists nor deletes with its key (ADR 0011). A read of the probe with
# the key proves nothing from here: the anonymous statement admits every principal from the
# VPS's address. A listing is granted to no one but the operator (s3:*), so the key a run is
# given must be refused one, or it is not the writer's.
confirm_write_only() {
  local answer
  if answer=$(s3api list-objects-v2 --bucket "$BUCKET" --prefix "$PROBE_KEY" --max-keys 1 2>&1)
  then
    fail "cette cle liste le bucket : ce n'est pas celle de lodb-pages-writer, qui n'a que \
ObjectStorageObjectsWrite (une cle de l'operateur ?)"
  fi
  is_denied "$answer" || fail "liste du bucket avec cette cle : $(last_line "$answer")"
  echo "  cle d'ecriture seule : verifie (liste du bucket refusee)"
}

header_value() {
  { grep -i "^$2:" "$1" || true; } | head -n 1 | cut -d ':' -f 2- |
    sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//'
}

# header_mismatch FILE NAME EXPECTED: prints " NAME (lu : VALUE)" when they differ.
header_mismatch() {
  local value
  value=$(header_value "$1" "$2")
  [[ "$value" == "$3" ]] || printf ' %s (lu : %s)' "$2" "${value:-absent}"
}

status_of() {
  head -n 1 "$1" | tr -d '\r' | cut -d ' ' -f 2
}

# What Scaleway really sends back (extra headers, metadata casing) is the evidence nginx's
# configuration is checked against: the operator sends this block back.
print_headers() {
  echo "  en-tetes recus au $2 :"
  tail -n +2 "$1" | tr -d '\r' | { grep -v -i -E "$VOLATILE_HEADERS" || true; } |
    sed -e '/^$/d' -e 's/^/    /'
}

# check_headers FILE REQUEST: the headers of a 200 are those the probe was written with.
check_headers() {
  local mismatches="" name value
  mismatches+=$(header_mismatch "$1" content-type "$PROBE_CONTENT_TYPE")
  mismatches+=$(header_mismatch "$1" content-encoding gzip)
  mismatches+=$(header_mismatch "$1" cache-control "$PROBE_CACHE_CONTROL")
  while IFS=$'\t' read -r name value; do
    mismatches+=$(header_mismatch "$1" "x-amz-meta-$name" "$value")
  done < <(probe_metadata)
  [[ -z "$mismatches" ]] || fail "$2 anonyme : en-tetes differents de ceux ecrits :$mismatches"
  echo "  $2 anonyme depuis le VPS (IPv4) : $HTTP_OK, en-tetes et metadonnees intacts"
  print_headers "$1" "$2"
}

# What nginx asks the store: the body must come back as the gzip written, byte for byte.
read_probe_by_get() {
  local status
  anonymous_curl -D /dev/stderr -o - "$1" >"$probe_dir/corps" 2>"$probe_dir/entetes-get" ||
    fail "GET anonyme impossible : $(tail -n 1 "$probe_dir/entetes-get")"
  status=$(status_of "$probe_dir/entetes-get")
  [[ "$status" == "$HTTP_OK" ]] || fail "GET anonyme depuis le VPS : ${status:-aucune reponse} \
au lieu de $HTTP_OK. 403 : l'adresse de sortie n'est pas VPS_IPV4, ou la lecture anonyme \
conditionnee par l'adresse ne marche pas (README.md, repli)"
  check_headers "$probe_dir/entetes-get" GET
  cmp -s "$probe_dir/corps" "$probe_dir/page.html.gz" ||
    fail "GET anonyme : le corps rendu n'est pas le gzip ecrit (decompresse en chemin ?)"
  echo "  corps : le gzip ecrit, octet pour octet"
}

# nginx never sends HEAD to the store (its cache turns a HEAD into a GET): a refusal here
# is reported, a 200 must still carry the probe's headers.
read_probe_by_head() {
  local status
  anonymous_curl -I "$1" >"$probe_dir/entetes-head" 2>&1 ||
    fail "HEAD anonyme impossible : $(tail -n 1 "$probe_dir/entetes-head")"
  status=$(status_of "$probe_dir/entetes-head")
  if [[ "$status" == "$HTTP_OK" ]]; then
    check_headers "$probe_dir/entetes-head" HEAD
  else
    echo "  HEAD anonyme depuis le VPS (IPv4) : ${status:-aucune reponse}, sans effet sur nginx"
  fi
}

read_probe_anonymously() {
  local url="$endpoint/$BUCKET/$PROBE_KEY"
  read_probe_by_get "$url"
  read_probe_by_head "$url"
  echo
  echo "Depuis le poste de l'operateur, la meme lecture doit etre refusee (403) :"
  printf "  curl -s -o /dev/null -w '%%{http_code}\\\\n' %s\n" "$url"
}

verify() {
  check_common_inputs "la cle de l'application lodb-pages-writer de cet environnement"
  if ! command -v gzip >/dev/null 2>&1 || ! command -v cmp >/dev/null 2>&1; then
    fail "gzip et cmp sont requis"
  fi
  export AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY
  probe_dir=$(mktemp -d)
  trap 'rm -rf "$probe_dir"' EXIT
  printf 'Sonde %s/%s/%s\n' "$endpoint" "$BUCKET" "$PROBE_KEY"
  write_probe
  confirm_write_only
  read_probe_anonymously
}
