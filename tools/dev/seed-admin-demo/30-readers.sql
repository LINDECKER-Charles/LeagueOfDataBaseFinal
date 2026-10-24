-- Demo traffic, last part: the readers of the home measure, as visits. A visit lands on a
-- page (origin page, its referrer's source) and goes on by navigation (internal); more of them
-- at the weekend, on the evening of Europe, and on patch day, every second Wednesday.

CREATE TEMP TABLE seed_profile (profile text PRIMARY KEY, weight float8 NOT NULL)
    ON COMMIT DROP;
INSERT INTO seed_profile VALUES
    ('Chrome|Windows|desktop', 38), ('Edge|Windows|desktop', 8), ('Firefox|Windows|desktop', 7),
    ('Opera|Windows|desktop', 4), ('Chrome|macOS|desktop', 3), ('Safari|macOS|desktop', 3),
    ('Firefox|Linux|desktop', 1), ('Chrome|Android|mobile', 17),
    ('Samsung Internet|Android|mobile', 3), ('Safari|iOS|mobile', 11), ('Chrome|iOS|mobile', 2),
    ('Safari|iPadOS|tablet', 1.5), ('Chrome|Android|tablet', 0.5), ('Bot|other|bot', 3);

CREATE TEMP TABLE seed_country (code text PRIMARY KEY, name text NOT NULL) ON COMMIT DROP;
INSERT INTO seed_country VALUES
    ('FR', 'France'), ('BE', 'Belgium'), ('CH', 'Switzerland'), ('CA', 'Canada'),
    ('MA', 'Morocco'), ('LU', 'Luxembourg'), ('US', 'United States'), ('GB', 'United Kingdom'),
    ('AU', 'Australia'), ('PH', 'Philippines'), ('IN', 'India'), ('NL', 'Netherlands'),
    ('SE', 'Sweden'), ('SG', 'Singapore'), ('NZ', 'New Zealand'), ('IE', 'Ireland'),
    ('DE', 'Germany'), ('AT', 'Austria'), ('ES', 'Spain'), ('MX', 'Mexico'),
    ('AR', 'Argentina'), ('CL', 'Chile'), ('CO', 'Colombia'), ('PE', 'Peru'), ('BR', 'Brazil'),
    ('PT', 'Portugal'), ('PL', 'Poland'), ('IT', 'Italy'), ('KR', 'South Korea'),
    ('TR', 'Türkiye'), ('RU', 'Russia'), ('UA', 'Ukraine'), ('KZ', 'Kazakhstan'),
    ('BY', 'Belarus'), ('VN', 'Vietnam'), ('JP', 'Japan'), ('CN', 'China'), ('MY', 'Malaysia'),
    ('CZ', 'Czechia'), ('SK', 'Slovakia'), ('RO', 'Romania'), ('MD', 'Moldova'),
    ('HU', 'Hungary'), ('GR', 'Greece'), ('CY', 'Cyprus'), ('TH', 'Thailand'),
    ('ID', 'Indonesia'), ('TW', 'Taiwan'), ('HK', 'Hong Kong'), ('SA', 'Saudi Arabia'),
    ('AE', 'United Arab Emirates'), ('EG', 'Egypt');

CREATE TEMP TABLE seed_route (resource text PRIMARY KEY, list_route text, detail_route text,
    type text) ON COMMIT DROP;
INSERT INTO seed_route VALUES
    ('champions', 'app_champions', 'app_champion', 'champion'),
    ('items', 'app_items', 'app_item', 'item'),
    ('runes', 'app_runes', 'app_rune', 'runesReforged'),
    ('summoners', 'app_summoners', 'app_summoner', 'summoner');

CREATE FUNCTION pg_temp.referer_host(source text) RETURNS text
LANGUAGE sql VOLATILE AS $$
    SELECT CASE source
        WHEN 'search' THEN pg_temp.pick(
            ARRAY['www.google.com', 'www.google.fr', 'www.bing.com', 'www.google.de',
                'www.google.es', 'duckduckgo.com', 'search.naver.com', 'www.qwant.com',
                'www.ecosia.org', 'yandex.ru', 'search.yahoo.com'],
            ARRAY[60, 14, 6, 4, 3, 3, 1.5, 1.5, 1.5, 1.5, 1])
        WHEN 'social' THEN pg_temp.pick(
            ARRAY['www.reddit.com', 'discord.com', 'www.youtube.com', 'www.twitch.tv', 'x.com',
                'www.facebook.com'],
            ARRAY[38, 26, 14, 7, 9, 6])
        WHEN 'external' THEN pg_temp.pick(
            ARRAY['www.op.gg', 'www.leagueoflegends.com', 'chatgpt.com', 'u.gg', 'lolalytics.com',
                'mobalytics.gg', 'fr.wikipedia.org', 'www.perplexity.ai'],
            ARRAY[18, 16, 16, 14, 14, 10, 8, 4])
    END
$$;

-- Where a visit lands, by where it comes from.
CREATE FUNCTION pg_temp.landing(source text) RETURNS text
LANGUAGE sql VOLATILE AS $$
    SELECT pg_temp.pick(ARRAY['detail', 'list', 'home'], CASE source
        WHEN 'search' THEN ARRAY[70, 20, 10]
        WHEN 'direct' THEN ARRAY[25, 25, 50]
        WHEN 'social' THEN ARRAY[75, 10, 15]
        ELSE ARRAY[60, 25, 15] END)
$$;

CREATE TEMP TABLE seed_visit ON COMMIT DROP AS
WITH days AS (
    SELECT age, current_date - age AS day,
        ((current_date - age - DATE '2026-10-21') % 14 + 14) % 14 AS since_patch
    FROM generate_series(0, :days - 1) AS age
), volume AS (
    SELECT day, round(:sessions
        * CASE extract(isodow FROM day) WHEN 6 THEN 1.3 WHEN 7 THEN 1.35 WHEN 5 THEN 1.1
            ELSE 1 END
        * CASE since_patch WHEN 0 THEN 1.6 WHEN 1 THEN 1.3 ELSE 1 END
        * (1 + 0.35 * (:days - age) / :days::float8)
        * (0.88 + 0.24 * random()))::int AS visits
    FROM days
), drawn AS (
    SELECT v.day,
        pg_temp.pick(p.profiles, p.weights) AS profile,
        pg_temp.pick(l.locales, l.weights) AS locale,
        pg_temp.pick(ARRAY['direct', 'search', 'social', 'external'], ARRAY[30, 48, 8, 14])
            AS source,
        substr(md5('lodb-demo-' || floor(power(random(), 1.7) * 45000)), 1, 16) AS visitor,
        (v.day + make_interval(
            hours => pg_temp.pick(
                ARRAY['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11', '12', '13',
                    '14', '15', '16', '17', '18', '19', '20', '21', '22', '23'],
                ARRAY[1.6, 1.1, 0.8, 0.6, 0.5, 0.6, 1, 1.6, 2.2, 2.6, 2.9, 3.2, 3.6, 3.8, 4,
                    4.4, 5, 5.6, 6, 6.2, 5.6, 4.4, 3.2, 2.2])::int,
            secs => random() * 3600))::timestamptz AS started,
        1 + least(24, floor(-ln(1 - random()) * 1.7))::int AS depth
    FROM volume AS v
    CROSS JOIN LATERAL generate_series(1, v.visits)
    CROSS JOIN (SELECT array_agg(profile) AS profiles, array_agg(weight) AS weights
        FROM seed_profile) AS p
    CROSS JOIN (SELECT array_agg(locale) AS locales, array_agg(weight) AS weights
        FROM seed_locale) AS l
)
SELECT drawn.*, pg_temp.referer_host(drawn.source) AS referer_host,
    CASE WHEN random() < 0.05 THEN 'en_US' ELSE l.lang END AS lang,
    CASE WHEN random() >= 0.04 THEN pg_temp.pick(l.countries, l.country_weights) END AS country
FROM drawn
JOIN seed_locale AS l USING (locale);

CREATE TEMP TABLE seed_view ON COMMIT DROP AS
WITH steps AS (
    SELECT s.*, step,
        s.started + make_interval(secs => (step - 1) * (40 + random() * 80)) AS occurred_at,
        CASE WHEN step = 1 THEN pg_temp.landing(s.source)
            ELSE pg_temp.pick(ARRAY['detail', 'list', 'home'], ARRAY[64, 27, 9]) END AS kind,
        pg_temp.resource() AS resource
    FROM seed_visit AS s
    CROSS JOIN LATERAL generate_series(1, s.depth) AS step
)
SELECT steps.*, pg_temp.entry(resource) AS entry,
    (CASE WHEN kind = 'detail' AND random() < 0.012 THEN 404 ELSE 200 END)::smallint AS status
FROM steps
WHERE occurred_at <= now();

DELETE FROM analytics_event WHERE occurred_at >= current_date - (:days - 1);
DELETE FROM analytics_daily WHERE day >= current_date - (:days - 1);
SELECT count(*) FILTER (WHERE created) AS partitions_created
FROM (SELECT public.lodb_create_analytics_partition(current_date - age) AS created
    FROM generate_series(0, :days - 1) AS age) AS partitions \g /dev/null

INSERT INTO analytics_event (occurred_at, origin, route, path, type, kind, entity, status,
    lang, locale, visitor, browser, os, device, is_bot, referer_host, referer_source, country,
    country_name)
SELECT v.occurred_at,
    CASE WHEN v.step = 1 THEN 'page' ELSE 'navigation' END,
    CASE v.kind WHEN 'home' THEN 'app_home' WHEN 'list' THEN r.list_route ELSE r.detail_route END,
    CASE v.kind
        WHEN 'home' THEN format('/%s/', v.locale)
        WHEN 'list' THEN format('/%s/%s', v.locale, v.resource)
        ELSE format('/%s/%s/%s', v.locale, v.resource, v.entry) END,
    CASE v.kind WHEN 'home' THEN 'home' ELSE r.type END,
    v.kind,
    CASE WHEN v.kind = 'detail' AND v.resource IN ('items', 'runes')
        THEN split_part(v.entry, '-', 1)
        WHEN v.kind = 'detail' THEN v.entry END,
    v.status, v.lang, v.locale, v.visitor,
    split_part(v.profile, '|', 1), split_part(v.profile, '|', 2), split_part(v.profile, '|', 3),
    v.profile = 'Bot|other|bot',
    CASE WHEN v.step = 1 THEN v.referer_host ELSE 'league-of-data-base.com' END,
    CASE WHEN v.step = 1 THEN v.source ELSE 'internal' END,
    v.country, c.name
FROM seed_view AS v
JOIN seed_route AS r USING (resource)
LEFT JOIN seed_country AS c ON c.code = v.country
ORDER BY v.occurred_at;
