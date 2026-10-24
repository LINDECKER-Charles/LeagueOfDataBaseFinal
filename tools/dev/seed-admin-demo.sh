#!/usr/bin/env bash
# Fills the database of a local stack with 90 days of demo traffic for the admin's analytics
# pages: robots in the journal (robot_agent, robot_daily, and robot_hit for the 30 days it
# keeps) and readers in the home measure (analytics_event).
#
#   tools/dev/seed-admin-demo.sh                                 # the lodb-dev stack
#   LODB_COMPOSE_PROJECT=lodb-dev-e1 tools/dev/seed-admin-demo.sh # a worktree's slot
#
# Local stacks only: any other Compose project is refused. The parts of seed-admin-demo/ run
# in their order, in one transaction. They replace the whole robot journal, and the views and
# daily aggregates of the seeded days; no other table is touched. The rollup folds the views
# into the daily aggregates within five minutes (the admin's « Consolider » does it at once).
set -euo pipefail

readonly LOCAL_PROJECT='^lodb-dev(-e[12])?$'

project="${LODB_COMPOSE_PROJECT:-lodb-dev}"
if [[ ! "$project" =~ $LOCAL_PROJECT ]]; then
  echo "Refused: $project is not a local stack (lodb-dev, lodb-dev-e1, lodb-dev-e2)." >&2
  exit 2
fi

parts="$(cd "$(dirname "${BASH_SOURCE[0]}")/seed-admin-demo" && pwd)"
{
  echo 'BEGIN;'
  cat "$parts"/*.sql
  echo 'COMMIT;'
} | docker exec -i "$project-postgres-1" psql -U lodb -d lodb -q -v ON_ERROR_STOP=1

docker exec "$project-postgres-1" psql -U lodb -d lodb -At -F ' ' -c "
  SELECT 'robot_hit', count(*) FROM robot_hit
  UNION ALL SELECT 'robot_daily', count(*) FROM robot_daily
  UNION ALL SELECT 'robot_agent', count(*) FROM robot_agent
  UNION ALL SELECT 'analytics_event', count(*) FROM analytics_event"
