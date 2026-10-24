// The robots journal (ADR 0013): every request of a client that says it is a robot, and every
// page request of the others, reported to the API once answered, with its status and its time.
// The API names the robot, looks up the network of the address and keeps the hits; it drops the
// readers' requests once it has counted their pace, which alone gives a hidden robot away.
//
// record() runs in the log phase, through the access log of the site (sites/site.conf): it only
// puts the request in a shared zone, never waits. flush() runs on one worker every interval
// (js_periodic, @lodb_robot_journal) and posts what the zone holds, one JSON object a line, to
// the API on the stack's network. A batch the API did not take stays for the next interval; a
// full zone forgets its oldest records (evict) rather than refuse new ones.

const JOURNAL_URL = 'http://lodb-api:8080/internal/robots/journal';
// Records posted at once: a few hundred kilobytes, far below the API's bound (4 MiB).
const BATCH = 500;
const ACCEPTED = 202;
const LOCK_KEY = 'flush';

// $lodb_robot_record (js_set): queues the request, and is itself always empty.
function record(r) {
  const v = r.variables;
  const line = JSON.stringify({
    t: Number(v.msec),
    ip: v.remote_addr,
    m: r.method,
    u: v.request_uri,
    s: Number(v.status),
    d: Number(v.request_time),
    b: Number(v.body_bytes_sent),
    a: v.http_user_agent || '',
    c: v.upstream_cache_status || '',
  });
  try {
    ngx.shared.lodb_robot_journal.set(v.request_id, line);
  } catch (error) {
    // A record larger than the zone: never one of nginx's, dropped.
  }
  return '';
}

// One flush at a time: a slow API would otherwise let the next interval post the same records.
async function flush() {
  const lock = ngx.shared.lodb_robot_journal_lock;
  if (!lock.add(LOCK_KEY, '1')) {
    return;
  }
  try {
    await post(ngx.shared.lodb_robot_journal);
  } finally {
    lock.delete(LOCK_KEY);
  }
}

async function post(zone) {
  const entries = zone.items(BATCH);
  if (entries.length === 0) {
    return;
  }
  const body = entries.map((entry) => entry[1]).join('\n');
  try {
    const reply = await ngx.fetch(JOURNAL_URL, {
      method: 'POST',
      body,
      headers: { 'Content-Type': 'application/x-ndjson' },
    });
    if (reply.status !== ACCEPTED) {
      return;
    }
  } catch (error) {
    // The API restarting or unreachable: the next interval posts them again.
    return;
  }
  // njs knows no for...of.
  entries.forEach((entry) => zone.delete(entry[0]));
}

export default { record, flush };
