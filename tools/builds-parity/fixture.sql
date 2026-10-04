-- Builds of the /b/{token} parity (L5.3): public and private builds on two patches, in the
-- four game modes and several languages, with ghosts (a champion, an item and a keystone no
-- patch knows), their owners and votes. For a COPY of the legacy dev database only, the one
-- both stacks then read; never for a database that serves.
--
--   psql -v ON_ERROR_STOP=1 -v latest=16.19.1 -v older=15.14.1 -f fixture.sql <database>
--
-- latest, older: two versions both stacks have ingested, the builds' own patches. Running it
-- again replaces the rows it wrote: the accounts `parity_*` and the tokens `feedbeef*`.
-- tokens.json lists the same tokens with their language; test/fixture.test.mjs keeps both
-- in step.

\if :{?latest}
\else
  \echo 'Set -v latest=<version>, a version both stacks have ingested.'
  \quit
\endif
\if :{?older}
\else
  \echo 'Set -v older=<version>, another version both stacks have ingested.'
  \quit
\endif

BEGIN;

-- The votes go with their builds and voters (ON DELETE CASCADE).
DELETE FROM builds WHERE share_token LIKE 'feedbeef%';
DELETE FROM users WHERE username LIKE 'parity\_%';

-- A supporter with a public card and a Riot tag, a plain author, and two voters. No
-- password: nobody signs in with them.
INSERT INTO users (email, username, roles, password, is_public_profile, riot_tagline,
                   is_supporter, is_verified, created_at)
VALUES
  ('parity-star@example.test', 'parity_star', '["ROLE_USER"]', NULL, true, 'EUW', true, true,
   '2026-01-10 09:00:00'),
  ('parity-plain@example.test', 'parity_plain', '["ROLE_USER"]', NULL, false, NULL, false,
   true, '2026-02-12 10:30:00'),
  ('parity-voter-a@example.test', 'parity_voter_a', '["ROLE_USER"]', NULL, false, NULL, false,
   true, '2026-03-01 12:00:00'),
  ('parity-voter-b@example.test', 'parity_voter_b', '["ROLE_USER"]', NULL, false, NULL, false,
   true, '2026-03-02 12:00:00');

-- One build per row: token, language, visibility, mode and patch first, as tokens.json
-- lists them. Runes: 8000 Precision, 8100 Domination, 8200 Sorcery, 8300 Inspiration,
-- 8400 Resolve; ids that stayed in the same slot for years. Ghosts: champion ParityGhost,
-- item 999999, keystone 9999.
WITH listed (share_token, language, is_public, game_mode, game_version, owner, name,
           champion_id, description, runes, steps, updated_at) AS (
  VALUES
  ('feedbeef0000000000000001', 'fr_FR', true, 'sr', :'latest', 'parity_star',
   'Ahri mid comète', 'Ahri',
   E'Roam après le niveau 6.\nGarder la charme pour le pick.',
   '{"primaryStyleId": 8200, "primarySelections": [8229, 8226, 8210, 8237],
     "secondaryStyleId": 8300, "secondarySelections": [8304, 8347]}',
   '[{"label": "Départ", "note": "Deux potions.", "items": ["1056", "2003", "2003"]},
     {"label": "Cœur", "note": null, "items": ["3020", "3089"]},
     {"label": "Fin", "note": "Selon la partie.", "items": ["3135", "3157"]}]',
   '2026-09-01 08:00:00'),
  ('feedbeef0000000000000002', 'en_US', true, 'aram', :'latest', 'parity_plain',
   'Jinx ARAM lethal tempo', 'Jinx', NULL,
   '{"primaryStyleId": 8000, "primarySelections": [8008, 9111, 9104, 8014],
     "secondaryStyleId": 8100, "secondarySelections": [8139, 8135]}',
   '[{"label": "Start", "note": null, "items": ["1055", "1001"]},
     {"label": "Core", "note": null, "items": ["3006", "3031", "3094"]}]',
   '2026-09-02 08:00:00'),
  ('feedbeef0000000000000003', 'de_DE', true, 'arena', :'older', 'parity_star',
   'Garen Arena', 'Garen', 'Einfach und robust.',
   '{"primaryStyleId": 8400, "primarySelections": [8437, 8446, 8444, 8451],
     "secondaryStyleId": 8000, "secondarySelections": [9111, 8014]}',
   '[{"label": "Kern", "note": null, "items": ["3047", "3075"]}]',
   '2026-08-20 18:00:00'),
  ('feedbeef0000000000000004', 'es_ES', true, 'nexus_blitz', :'older', 'parity_plain',
   'Wukong Nexus Blitz', 'MonkeyKing', NULL,
   '{"primaryStyleId": 8000, "primarySelections": [8010, 9111, 9104, 8299],
     "secondaryStyleId": 8400, "secondarySelections": [8444, 8451]}',
   '[{"label": "Inicio", "note": null, "items": ["1055"]},
     {"label": "Núcleo", "note": null, "items": ["3047", "3065"]}]',
   '2026-08-21 18:00:00'),
  ('feedbeef0000000000000005', 'ko_KR', false, 'sr', :'older', 'parity_plain',
   'Leona support (private)', 'Leona', NULL,
   '{"primaryStyleId": 8400, "primarySelections": [8439, 8463, 8444, 8453],
     "secondaryStyleId": 8300, "secondarySelections": [8304, 8347]}',
   '[{"label": "시작", "note": null, "items": ["3340", "2003"]},
     {"label": "핵심", "note": null, "items": ["3047", "3075"]}]',
   '2026-07-30 07:00:00'),
  ('feedbeef0000000000000006', 'zh_TW', false, 'aram', :'latest', 'parity_star',
   'Lux ARAM (private)', 'Lux', NULL,
   '{"primaryStyleId": 8200, "primarySelections": [8229, 8226, 8210, 8237],
     "secondaryStyleId": 8300, "secondarySelections": [8304, 8347]}',
   '[{"label": "核心", "note": null, "items": ["3020", "3089", "3135"]}]',
   '2026-09-03 07:00:00'),
  ('feedbeef0000000000000007', 'en_US', true, 'sr', :'latest', 'parity_plain',
   'Ghost champion', 'ParityGhost', 'A champion no patch knows.',
   '{"primaryStyleId": 8000, "primarySelections": [8010, 9111, 9104, 8014],
     "secondaryStyleId": 8400, "secondarySelections": [8444, 8451]}',
   '[{"label": "Core", "note": null, "items": ["3006", "3031"]}]',
   '2026-09-04 07:00:00'),
  ('feedbeef0000000000000008', 'fr_FR', true, 'sr', :'older', 'parity_star',
   'Objet fantôme', 'Ahri', NULL,
   '{"primaryStyleId": 8200, "primarySelections": [8229, 8226, 8210, 8237],
     "secondaryStyleId": 8300, "secondarySelections": [8304, 8347]}',
   '[{"label": "Cœur", "note": null, "items": ["3020", "999999", "3089"]}]',
   '2026-08-10 07:00:00'),
  ('feedbeef0000000000000009', 'pt_BR', false, 'sr', :'latest', 'parity_plain',
   'Keystone fantasma (private)', 'Jinx', NULL,
   '{"primaryStyleId": 8000, "primarySelections": [9999, 9111, 9104, 8014],
     "secondaryStyleId": 8100, "secondarySelections": [8139, 8135]}',
   '[{"label": "Núcleo", "note": null, "items": ["3006", "3031"]}]',
   '2026-09-05 07:00:00'),
  ('feedbeef0000000000000010', 'en_GB', true, 'sr', :'older', 'parity_star',
   'Thresh full order', 'Thresh',
   E'Hook, lantern, repeat.\n\nTen steps of the purchase order.',
   '{"primaryStyleId": 8400, "primarySelections": [8439, 8446, 8429, 8242],
     "secondaryStyleId": 8300, "secondarySelections": [8304, 8347]}',
   '[{"label": "Start", "note": "Support item.", "items": ["3340", "2003", "2003"]},
     {"label": "Boots", "note": null, "items": ["1001"]},
     {"label": "First item", "note": null, "items": ["3047"]},
     {"label": "Second item", "note": "Against burst.", "items": ["3065"]},
     {"label": "Late", "note": null, "items": ["3075", "3157"]}]',
   '2026-08-11 07:00:00')
)
INSERT INTO builds (share_token, language, is_public, game_mode, game_version, owner_id, name,
                    champion_id, description, runes, steps, created_at, updated_at)
SELECT r.share_token, r.language, r.is_public, r.game_mode, r.game_version, u.id, r.name,
       r.champion_id, r.description, r.runes::jsonb, r.steps::jsonb,
       r.updated_at::timestamp - interval '1 day', r.updated_at::timestamp
FROM listed r
JOIN users u ON u.username = r.owner;

-- Scores: +2, -1 and 0 on public builds; a vote on a private build, which no page shows.
WITH ballots (share_token, voter, value) AS (
  VALUES
  ('feedbeef0000000000000001', 'parity_voter_a', 1),
  ('feedbeef0000000000000001', 'parity_voter_b', 1),
  ('feedbeef0000000000000002', 'parity_voter_a', -1),
  ('feedbeef0000000000000003', 'parity_voter_a', 1),
  ('feedbeef0000000000000003', 'parity_voter_b', -1),
  ('feedbeef0000000000000005', 'parity_voter_a', 1)
)
INSERT INTO build_votes (build_id, voter_id, value, created_at)
SELECT b.id, u.id, v.value, '2026-09-10 12:00:00'
FROM ballots v
JOIN builds b ON b.share_token = v.share_token
JOIN users u ON u.username = v.voter;

COMMIT;
