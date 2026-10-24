-- Demo traffic, third part: each hit of the fleet, what the site answered, and the journal
-- written from them as the API writes it (RobotJournalWriter): the agents, the days, the hits.

-- Scanners probe for files and admin pages; a target matches the section HitSections reads.
CREATE FUNCTION pg_temp.robot_target(section text, mix text) RETURNS text
LANGUAGE plpgsql VOLATILE AS $$
DECLARE
    locale text := pg_temp.robot_locale();
    resource text := pg_temp.resource();
BEGIN
    RETURN CASE
        WHEN section = 'home' THEN pg_temp.pick(ARRAY['/', '/' || locale || '/'], ARRAY[1, 3])
        WHEN section = 'list' THEN format('/%s/%s', locale, resource)
        WHEN section = 'detail' THEN format('/%s/%s/%s', locale, resource, pg_temp.entry(resource))
        WHEN section = 'archive' THEN format('/%s/%s/%s', locale, pg_temp.past_version(),
            pg_temp.pick(ARRAY[resource, resource || '/' || pg_temp.entry(resource)],
                ARRAY[25, 75]))
        WHEN section = 'page' THEN format('/%s/%s', locale, pg_temp.pick(
            ARRAY['patches', 'trends', 'about', 'developers', 'legal/terms'],
            ARRAY[40, 25, 15, 10, 10]))
        WHEN section = 'sitemap' THEN pg_temp.pick(
            ARRAY['/sitemap.xml', format('/sitemaps/%s/%s.xml', locale, resource)], ARRAY[1, 4])
        WHEN section = 'robots' THEN '/robots.txt'
        WHEN section = 'api' THEN pg_temp.pick(ARRAY[
            format('/api/catalog/latest/en_US/%s', resource),
            format('/v1/%s?lang=en_US', resource),
            format('/v1/%s/%s', resource, split_part(pg_temp.entry(resource), '-', 1))],
            ARRAY[3, 4, 3])
        WHEN section = 'asset' AND mix = 'scan' THEN pg_temp.pick(
            ARRAY['/.env', '/wp-login.php', '/xmlrpc.php', '/config.json', '/backup.zip'],
            ARRAY[35, 30, 15, 10, 10])
        WHEN section = 'asset' THEN pg_temp.pick(
            ARRAY[format('/cdn/blobs/%s.webp', md5(random()::text)), '/favicon.ico'],
            ARRAY[92, 8])
        ELSE pg_temp.pick(
            ARRAY['/.git/config', '/wp-admin', '/cgi-bin/luci', '/actuator/env', '/server-status',
                '/.aws/credentials'],
            ARRAY[25, 25, 10, 15, 10, 15])
    END;
END $$;

-- The shedding of past patches (503) falls on the heaviest crawlers; scrapers meet the limits.
CREATE FUNCTION pg_temp.robot_status(mix text, section text) RETURNS smallint
LANGUAGE plpgsql VOLATILE AS $$
DECLARE
    draw float8 := random();
BEGIN
    RETURN (CASE
        WHEN mix = 'scan' AND section <> 'home' THEN
            CASE WHEN draw < 0.88 THEN 404 WHEN draw < 0.97 THEN 403 ELSE 400 END
        WHEN draw > 0.997 THEN 502
        WHEN mix = 'undeclared' THEN CASE
            WHEN draw < 0.2 THEN 503 WHEN draw < 0.32 THEN 403 WHEN draw < 0.34 THEN 404
            ELSE 200 END
        WHEN mix = 'scraper' THEN CASE
            WHEN draw < 0.22 THEN 429 WHEN draw < 0.34 THEN 403 WHEN draw < 0.37 THEN 404
            ELSE 200 END
        WHEN mix = 'tool_api' THEN CASE
            WHEN draw < 0.12 THEN 429 WHEN draw < 0.18 THEN 404 WHEN draw < 0.2 THEN 400
            ELSE 200 END
        WHEN section = 'archive' THEN CASE
            WHEN draw < CASE mix WHEN 'archive_heavy' THEN 0.12 ELSE 0.025 END THEN 503
            WHEN draw > 0.97 THEN 404 WHEN draw > 0.94 THEN 301 ELSE 200 END
        WHEN section IN ('detail', 'list') THEN CASE
            WHEN mix = 'search' AND draw < 0.1 THEN 304
            WHEN draw > 0.985 THEN 404 WHEN draw > 0.96 THEN 301 ELSE 200 END
        WHEN section IN ('sitemap', 'robots', 'asset') THEN CASE
            WHEN draw < 0.15 THEN 304 WHEN section = 'asset' AND draw > 0.97 THEN 404
            ELSE 200 END
        WHEN section = 'other' THEN CASE WHEN draw < 0.85 THEN 404 ELSE 301 END
        ELSE 200
    END)::smallint;
END $$;

CREATE FUNCTION pg_temp.robot_cache(section text, status smallint) RETURNS text
LANGUAGE sql VOLATILE AS $$
    SELECT CASE
        WHEN status NOT IN (200, 304) OR section NOT IN ('home', 'list', 'detail', 'sitemap')
            THEN NULL
        ELSE pg_temp.pick(ARRAY['HIT', 'MISS', 'STALE', 'EXPIRED'], ARRAY[70, 22, 4, 4])
    END
$$;

-- A past patch is read from the object store, or rendered cold when it is not there yet.
CREATE FUNCTION pg_temp.robot_duration(section text, status smallint, cache text)
RETURNS int
LANGUAGE sql VOLATILE AS $$
    SELECT CASE
        WHEN status IN (403, 429, 503) THEN pg_temp.span(0, 4, 2)
        WHEN status = 502 THEN pg_temp.span(2000, 30000, 1)
        WHEN status IN (301, 304, 400, 404) OR section IN ('robots', 'asset', 'other')
            THEN pg_temp.span(0, 12, 2)
        WHEN cache = 'HIT' THEN pg_temp.span(1, 14, 2)
        WHEN section = 'archive' THEN CASE
            WHEN random() < 0.78 THEN pg_temp.span(8, 70, 1.5)
            ELSE pg_temp.span(900, 7500, 2) END
        WHEN section = 'sitemap' THEN pg_temp.span(6, 120, 2)
        WHEN section = 'api' THEN pg_temp.span(14, 260, 2)
        ELSE pg_temp.span(55, 520, 2.2)
    END
$$;

CREATE FUNCTION pg_temp.robot_bytes(section text, status smallint, method text)
RETURNS bigint
LANGUAGE sql VOLATILE AS $$
    SELECT CASE
        WHEN method = 'HEAD' OR status = 304 THEN 0
        WHEN status IN (301, 400, 403, 429, 503) THEN pg_temp.span(150, 420, 1)
        WHEN status = 404 AND section IN ('other', 'asset') THEN pg_temp.span(150, 600, 1)
        WHEN status = 404 THEN pg_temp.span(6000, 9500, 1)
        WHEN status = 502 THEN 560
        WHEN section = 'robots' THEN 742
        WHEN section = 'home' THEN pg_temp.span(88000, 132000, 1)
        WHEN section = 'list' THEN pg_temp.span(160000, 430000, 1)
        WHEN section IN ('detail', 'archive') THEN pg_temp.span(52000, 145000, 1)
        WHEN section = 'sitemap' THEN pg_temp.span(40000, 900000, 1.5)
        WHEN section = 'api' THEN pg_temp.span(6000, 620000, 2)
        WHEN section = 'asset' THEN pg_temp.span(900, 360000, 2)
        ELSE pg_temp.span(38000, 96000, 1)
    END
$$;

-- The monitor comes every five minutes; the AI crawlers grow over the stretch.
CREATE TEMP TABLE seed_hit ON COMMIT DROP AS
WITH fleet_day AS (
    SELECT f.agent, f.mix, f.verdict, f.network, f.detection, f.method, m.sections, m.weights,
        current_date - age AS day,
        CASE WHEN f.mix = 'monitor' THEN f.per_day::int ELSE round(
            f.per_day * :robot_scale * (0.8 + 0.4 * random())
            * (1 + f.growth * (f.since - age) / greatest(f.since, 1)))::int END AS volume
    FROM seed_fleet AS f
    JOIN seed_mix AS m USING (mix)
    CROSS JOIN LATERAL generate_series(f.until, least(f.since, :days - 1)) AS age
), drawn AS (
    SELECT d.*, pg_temp.pick(d.sections, d.weights) AS section,
        (d.day + make_interval(secs => CASE WHEN d.mix = 'monitor'
            THEN (n - 1) * 300 + random() * 20 ELSE random() * 86400 END))::timestamptz
            AS occurred_at
    FROM fleet_day AS d
    CROSS JOIN LATERAL generate_series(1, d.volume) AS n
), answered AS (
    SELECT drawn.*, pg_temp.robot_status(mix, section) AS status,
        CASE WHEN mix = 'scan' AND random() < 0.08 THEN 'POST' ELSE method END AS verb
    FROM drawn
    WHERE occurred_at <= now()
), cached AS (
    SELECT answered.*, pg_temp.robot_cache(section, status) AS cache FROM answered
)
SELECT occurred_at, agent, detection, verdict, network, verb AS method,
    pg_temp.robot_target(section, mix) AS target, section, status,
    pg_temp.robot_duration(section, status, cache) AS duration_ms,
    pg_temp.robot_bytes(section, status, verb) AS bytes, cache
FROM cached;

TRUNCATE robot_hit, robot_daily, robot_agent RESTART IDENTITY;

INSERT INTO robot_agent (agent, first_seen, last_seen)
SELECT agent, min(occurred_at), max(occurred_at) FROM seed_hit GROUP BY agent ORDER BY agent;

INSERT INTO robot_daily (day, agent_id, detection, verdict, network, section, status, hits,
    bytes, duration_ms, slow_hits)
SELECT (h.occurred_at AT TIME ZONE 'UTC')::date, a.id, h.detection, h.verdict,
    coalesce(h.network, ''), h.section, h.status, count(*), sum(h.bytes), sum(h.duration_ms),
    count(*) FILTER (WHERE h.duration_ms >= 1000)
FROM seed_hit AS h
JOIN robot_agent AS a USING (agent)
GROUP BY 1, 2, 3, 4, 5, 6, 7;

INSERT INTO robot_hit (occurred_at, agent_id, detection, verdict, network, method, target,
    section, status, duration_ms, bytes, cache)
SELECT h.occurred_at, a.id, h.detection, h.verdict, h.network, h.method, h.target, h.section,
    h.status, h.duration_ms, h.bytes, h.cache
FROM seed_hit AS h
JOIN robot_agent AS a USING (agent)
WHERE h.occurred_at >= now() - make_interval(days => :hit_days)
ORDER BY h.occurred_at;
