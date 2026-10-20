# shellcheck shell=bash disable=SC2154  # endpoint and the helpers: ../scaleway-bucket.sh
# `configurer`, sourced by ../scaleway-bucket.sh: the bucket, its versioning, its lifecycle,
# then its policy, last.

readonly POLICY_VERSION=2023-04-17
readonly STAGING_RULE=expire-pages
readonly STAGING_EXPIRY_DAYS=14
readonly RETIRED_RULE_PREFIX=retire-pages-
readonly RETIRED_EXPIRY_DAYS=1
readonly MULTIPART_RULE=abandon-multipart
readonly MULTIPART_ABORT_DAYS=1
readonly OCTET_MAX=255
readonly IPV4_FORMAT='^([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})$'
readonly EPOCH_FORMAT='^[1-9][0-9]{0,8}$'
readonly EPOCH_LIST_FORMAT='^[0-9 ]*$'

# Lifecycle rules and policies are compared on their meaning: a provider may add fields,
# move a prefix out of Filter, or turn a one-element list into a string.
readonly JQ_CANONICAL='
def rules: [(.Rules // [])[] | {id: .ID, status: .Status,
  prefix: (.Filter.Prefix // .Prefix // ""), expiration: (.Expiration.Days // 0),
  abort: (.AbortIncompleteMultipartUpload.DaysAfterInitiation // 0),
  extra: ([.Expiration.Date, .Transitions, .NoncurrentVersionExpiration,
    .NoncurrentVersionTransitions, .Filter.Tag, .Filter.And] | map(select(. != null)))}]
  | sort_by(.id);
def list: if type == "array" then sort else [.] end;
def one: if type == "array" and length == 1 then .[0] else . end;
def principal: if type == "object" then map_values(one) else . end
  | if . == {SCW: "*"} or . == {AWS: "*"} then "*" else . end;
def statement: del(.Sid) | .Principal |= principal | .Action |= list | .Resource |= list
  | if .Condition.IpAddress then .Condition.IpAddress["aws:SourceIp"] |= list else . end;
def policy: if . == null then null
  else {Version, Statement: (.Statement | list | map(statement) | sort)} end;'

check_vps_address() {
  local octet
  [[ "${VPS_IPV4:-}" =~ $IPV4_FORMAT ]] ||
    fail "VPS_IPV4 : l'adresse IPv4 de sortie du VPS (187.77.173.149)"
  for octet in "${BASH_REMATCH[@]:1}"; do
    ((10#$octet <= OCTET_MAX)) || fail "VPS_IPV4 : $VPS_IPV4 n'est pas une adresse IPv4"
  done
}

# A retired epoch is one already written and no longer live. A rule on a future epoch would
# stay on the bucket (ensure_lifecycle keeps them) and empty the pass that later writes it.
refuse_unretirable_epoch() {
  local newest=$SERVED_EPOCH
  [[ "$1" != "$SERVED_EPOCH" ]] ||
    fail "l'epoch $1 est celle que nginx sert (SERVED_EPOCH) : la retirer viderait le catalogue"
  [[ "$1" != "$WRITE_EPOCH" ]] ||
    fail "l'epoch $1 est celle que la passe ecrit (WRITE_EPOCH) : la retirer effacerait la passe"
  if ((WRITE_EPOCH > newest)); then newest=$WRITE_EPOCH; fi
  ((10#$1 < newest)) || fail "l'epoch $1 n'a jamais ete ecrite (au-dela de SERVED_EPOCH et \
WRITE_EPOCH) : sa regle retire-pages-$1 effacerait la passe qui l'ecrira"
}

check_epochs() {
  local epoch
  require SERVED_EPOCH "$EPOCH_FORMAT" \
    "l'epoch servie (LODB_ARCHIVE_SERVE_EPOCH), 1 ou plus : 0 est reservee aux sondes"
  require WRITE_EPOCH "$EPOCH_FORMAT" \
    "l'epoch de la passe en cours, 1 ou plus (l'epoch servie s'il n'y a pas de passe)"
  retired_epochs=$(printf '%s' "${RETIRED_EPOCHS:-}" | tr ',' ' ')
  [[ "$retired_epochs" =~ $EPOCH_LIST_FORMAT ]] ||
    fail "RETIRED_EPOCHS : des epochs separees par des espaces ou des virgules (\"1 2\")"
  for epoch in $retired_epochs; do
    [[ "$epoch" =~ $EPOCH_FORMAT ]] ||
      fail "RETIRED_EPOCHS : $epoch n'est pas une epoch (1 ou plus)"
    refuse_unretirable_epoch "$epoch"
  done
}

check_environment_epochs() {
  local name
  case ${ENVIRONMENT:-} in
    prod) check_epochs ;;
    staging)
      for name in SERVED_EPOCH WRITE_EPOCH RETIRED_EPOCHS; do
        [[ -z "${!name:-}" ]] || fail "$name : sans objet sur staging, ou la regle $STAGING_RULE \
expire toute page apres $STAGING_EXPIRY_DAYS jours"
      done
      ;;
    *) fail "ENVIRONMENT : staging ou prod" ;;
  esac
}

# Each bucket bears its environment's name: the staging rules, run against the prod bucket,
# would expire every page of the served epoch after 14 days.
check_bucket_environment() {
  [[ "$BUCKET" == *-"$ENVIRONMENT" ]] || fail "BUCKET : $BUCKET ne finit pas par -$ENVIRONMENT \
(lodb-pages-$ENVIRONMENT) : ses regles de cycle de vie iraient au bucket d'un autre environnement"
}

check_configuration_inputs() {
  check_common_inputs "$OPERATOR_KEY_HINT"
  check_environment_epochs
  check_bucket_environment
  require PROJECT_ID "$UUID_FORMAT" "l'identifiant du projet lodb-pages-$ENVIRONMENT (UUID)"
  require OPERATOR_USER_ID "$UUID_FORMAT" \
    "l'identifiant de l'utilisateur de l'operateur (UUID) : sans lui, la politique l'exclurait"
  require WRITER_APPLICATION_ID "$UUID_FORMAT" \
    "l'identifiant de l'application lodb-pages-writer-$ENVIRONMENT (UUID)"
  check_vps_address
}

# LocationConstraint: what `aws s3 mb`, the command Scaleway documents, sends.
ensure_bucket() {
  local answer
  if answer=$(s3api head-bucket --bucket "$BUCKET" 2>&1); then
    echo "  existe deja"
    return
  fi
  case $answer in
    *"(404)"* | *NoSuchBucket* | *"Not Found"*)
      s3api create-bucket --bucket "$BUCKET" \
        --create-bucket-configuration "LocationConstraint=$REGION" >/dev/null ||
        fail "creation du bucket refusee (message ci-dessus)"
      echo "  cree, prive, sans versionnage ni verrouillage d'objet"
      ;;
    *"(403)"* | *Forbidden* | *AccessDenied*)
      fail "le bucket $BUCKET existe mais cette cle n'y a pas acces : un autre projet ou un autre \
compte le possede (un nom est unique), ou une politique ecarte l'operateur (README.md)"
      ;;
    *) fail "reponse inattendue de Scaleway : $(last_line "$answer")" ;;
  esac
}

# Every retry of a key would keep a noncurrent copy, billed, that no lifecycle rule here
# removes. A versioned bucket cannot be made unversioned again: a new bucket is the fix.
check_versioning() {
  local status
  status=$(s3api get-bucket-versioning --bucket "$BUCKET" --query Status --output text)
  case $status in
    Enabled) fail "le versionnage est actif sur $BUCKET (ou le verrouillage d'objet, qui \
l'impose) : chaque reecriture d'une page garderait une copie facturee. Creer un bucket neuf." ;;
    Suspended) echo "  versionnage : suspendu (les versions anciennes restent a supprimer)" ;;
    *) echo "  versionnage : inactif" ;;
  esac
}

read_lifecycle() {
  local answer
  if answer=$(s3api get-bucket-lifecycle-configuration --bucket "$BUCKET" 2>&1); then
    printf '%s' "$answer"
    return
  fi
  case $answer in
    *NoSuchLifecycleConfiguration*) printf '{}' ;;
    *) fail "lecture du cycle de vie : $(last_line "$answer")" ;;
  esac
}

rule_ids() {
  jq_in_image -r '[(.Rules // [])[].ID] | join(" ")' <<<"$1"
}

existing_retired_epochs() {
  # shellcheck disable=SC2016  # a jq program: $prefix is jq's, not the shell's
  jq_in_image -r --arg prefix "$RETIRED_RULE_PREFIX" '(.Rules // [])[].ID
    | select(test("^" + $prefix + "[0-9]+$")) | ltrimstr($prefix)' <<<"$1"
}

same_rules() {
  jq_in_image -n -e --argjson current "$1" --argjson wanted "$2" \
    "$JQ_CANONICAL (\$current | rules) == (\$wanted | rules)" >/dev/null
}

expiry_rule() {
  printf '{"ID":"%s","Status":"Enabled","Filter":{"Prefix":"%s"},"Expiration":{"Days":%s}}' \
    "$1" "$2" "$3"
}

# Staging: every page expires. Prod: one rule per retired epoch, its trailing slash kept
# (`pages/1` would also match `pages/10/`); $1 lists the epochs whose rule the bucket holds.
desired_lifecycle() {
  local rules="" epoch
  if [[ "$ENVIRONMENT" == staging ]]; then
    rules="$(expiry_rule "$STAGING_RULE" pages/ "$STAGING_EXPIRY_DAYS"),"
  else
    # shellcheck disable=SC2086  # both lists are split into their epochs on purpose
    for epoch in $(printf '%s\n' "$PROBE_EPOCH" $1 $retired_epochs | sort -n -u); do
      rules+="$(expiry_rule "$RETIRED_RULE_PREFIX$epoch" "pages/$epoch/" "$RETIRED_EXPIRY_DAYS"),"
    done
  fi
  printf '{"Rules":[%s{"ID":"%s","Status":"Enabled","Filter":{"Prefix":""},%s}]}' "$rules" \
    "$MULTIPART_RULE" \
    "\"AbortIncompleteMultipartUpload\":{\"DaysAfterInitiation\":$MULTIPART_ABORT_DAYS}"
}

# Retirement rules mark a prod bucket: the staging rule set would drop them, and expire the
# served epoch with every other page.
refuse_prod_rules_on_staging() {
  [[ -z "$1" ]] || fail "le cycle de vie de $BUCKET porte des regles $RETIRED_RULE_PREFIX<N> : \
c'est celui d'un bucket de prod. Rien n'est change : verifier BUCKET et ENVIRONMENT"
}

# The retirement rules already on the bucket stay, so that a retirement in progress is never
# cut short by a run that forgets to list it; one on a live or future epoch stops the run.
ensure_lifecycle() {
  local current wanted previous kept epoch
  current=$(read_lifecycle)
  kept=$(existing_retired_epochs "$current")
  if [[ "$ENVIRONMENT" == prod ]]; then
    for epoch in $kept; do refuse_unretirable_epoch "$epoch"; done
  else
    refuse_prod_rules_on_staging "$kept"
  fi
  wanted=$(desired_lifecycle "$kept")
  if same_rules "$current" "$wanted"; then
    echo "  cycle de vie : $(rule_ids "$wanted") (deja en place)"
    return
  fi
  previous=$(rule_ids "$current")
  s3api put-bucket-lifecycle-configuration --bucket "$BUCKET" \
    --lifecycle-configuration "$wanted" >/dev/null
  # Read back: a provider that silently drops part of a rule shows here.
  current=$(read_lifecycle)
  same_rules "$current" "$wanted" || fail "cycle de vie pose, mais relu autrement : $current"
  echo "  cycle de vie : $(rule_ids "$wanted") (remplace : ${previous:-aucune regle})"
}

# Allow statements only (Scaleway: "Deny is useless"); resources without an arn: prefix.
bucket_policy() {
  local objects="\"$BUCKET/pages/*\",\"$BUCKET/build/*\""
  local operator="{\"SCW\":\"user_id:$OPERATOR_USER_ID\"}"
  local writer="{\"SCW\":\"application_id:$WRITER_APPLICATION_ID\"}"
  local from_vps="{\"IpAddress\":{\"aws:SourceIp\":[\"$VPS_IPV4/32\"]}}"
  printf '{"Version":"%s","Id":"lodb-pages-%s","Statement":[%s,%s,%s]}' \
    "$POLICY_VERSION" "$ENVIRONMENT" \
    "{\"Sid\":\"Operateur\",\"Effect\":\"Allow\",\"Principal\":$operator,\
\"Action\":[\"s3:*\"],\"Resource\":[\"$BUCKET\",\"$BUCKET/*\"]}" \
    "{\"Sid\":\"Ecrivain\",\"Effect\":\"Allow\",\"Principal\":$writer,\
\"Action\":[\"s3:PutObject\"],\"Resource\":[$objects]}" \
    "{\"Sid\":\"LectureVps\",\"Effect\":\"Allow\",\"Principal\":\"*\",\
\"Action\":[\"s3:GetObject\"],\"Resource\":[$objects],\"Condition\":$from_vps}"
}

# Once a policy exists, any principal it does not cite loses the bucket, the operator
# included: a policy without the operator's full access is never sent.
refuse_policy_without_operator() {
  # shellcheck disable=SC2016  # a jq program: $policy, $user and $bucket are jq's
  jq_in_image -n -e --argjson policy "$1" --arg user "user_id:$OPERATOR_USER_ID" \
    --arg bucket "$BUCKET" '[$policy.Statement[] | select(.Effect == "Allow"
      and .Principal == {SCW: $user} and (.Action | index("s3:*")) != null
      and (.Resource | index($bucket)) != null and (.Resource | index($bucket + "/*")) != null)]
    | length > 0' >/dev/null ||
    fail "politique refusee : elle n'accorde pas s3:* a l'operateur sur $BUCKET et $BUCKET/*"
}

# Version and statements only: a provider may drop the Id or the Sids, or spell the anonymous
# principal its own way, and the policy still says the same.
canonical_policy() {
  jq_in_image -n -c --argjson policy "$1" "$JQ_CANONICAL \$policy | policy"
}

# The policy of a get-bucket-policy answer ('{}': none), in the same canonical form.
canonical_answer() {
  jq_in_image -n -c --argjson answer "$1" \
    "$JQ_CANONICAL \$answer.Policy // \"null\" | fromjson | policy"
}

# Applied last: until now, nothing could lock the operator out. Compared in canonical form.
ensure_policy() {
  local policy wanted current previous=aucune
  local summary="operateur, ecrivain, lecture anonyme depuis $VPS_IPV4"
  policy=$(bucket_policy)
  refuse_policy_without_operator "$policy"
  wanted=$(canonical_policy "$policy")
  current=$(read_policy)
  current=$(canonical_answer "$current")
  if [[ "$current" == "$wanted" ]]; then
    echo "  politique : $summary (deja en place)"
    return
  fi
  [[ "$current" == null ]] || previous="une autre politique"
  s3api put-bucket-policy --bucket "$BUCKET" --policy "$policy" >/dev/null
  current=$(read_policy)
  current=$(canonical_answer "$current")
  if [[ "$current" != "$wanted" ]]; then
    # The policy is in place whatever it reads like: whether the operator still gets in is
    # what they need to know first, then both forms.
    confirm_operator_access
    fail "politique posee, mais relue autrement. Posee : $wanted ; relue : $current"
  fi
  echo "  politique : $summary (remplacee : $previous)"
}

confirm_operator_access() {
  retry_while_denied "acces de l'operateur sous la politique" \
    s3api head-bucket --bucket "$BUCKET" ||
    fail "la politique ecarte l'operateur (OPERATOR_USER_ID faux ?) : la retirer avec \
'bash $SCRIPT supprimer-politique', corriger, relancer (README.md, erreur de politique)"
  echo "  acces de l'operateur sous la politique : verifie"
}

print_next_steps() {
  local secret=ENV_PROD
  [[ "$ENVIRONMENT" == prod ]] || secret=ENV_STAGING
  echo
  echo "Bucket pret. Ensuite (tools/prerender/README.md) : sur le VPS, avec la cle de l'ecrivain,"
  echo "  BUCKET=$BUCKET bash $SCRIPT verifier"
  echo "puis, dans $secret : LoDb__Prerender__Bucket=$BUCKET, LODB_PAGES_STORE_BUCKET=$BUCKET,"
  echo "et la cle de l'ecrivain en LoDb__Prerender__AccessKey et LoDb__Prerender__SecretKey."
}

configure() {
  check_configuration_inputs
  scope_operator_key
  printf 'Bucket %s (%s, %s, projet %s)\n' "$BUCKET" "$ENVIRONMENT" "$endpoint" "$PROJECT_ID"
  ensure_bucket
  check_versioning
  ensure_lifecycle
  ensure_policy
  confirm_operator_access
  print_next_steps
}
