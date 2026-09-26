-- One line per object of the current schema, sorted: what `migrate` and
-- `baseline mark-applied` compare with doctrine-catalog.txt. Written for the comparison,
-- not for humans: column order and physical details are left out, and so are the EF
-- history table and the objects an extension owns.
WITH relation AS (
    SELECT c.oid, c.relname, c.relkind
    FROM pg_class c
    WHERE c.relnamespace = current_schema()::regnamespace
      AND c.relname <> '__EFMigrationsHistory'
      AND NOT EXISTS (
          SELECT 1 FROM pg_depend d
          WHERE d.classid = 'pg_class'::regclass AND d.objid = c.oid AND d.deptype = 'e')
),
line AS (
    SELECT format('table %s', r.relname) AS text
    FROM relation r WHERE r.relkind IN ('r', 'p')
    UNION ALL
    SELECT format('view %s', r.relname)
    FROM relation r WHERE r.relkind IN ('v', 'm')
    UNION ALL
    SELECT format(
        'column %s.%s %s%s%s%s',
        r.relname,
        a.attname,
        format_type(a.atttypid, a.atttypmod),
        CASE WHEN a.attnotnull THEN ' not null' ELSE '' END,
        CASE a.attidentity
            WHEN 'd' THEN ' identity by default'
            WHEN 'a' THEN ' identity always'
            ELSE ''
        END,
        coalesce(' default ' || pg_get_expr(ad.adbin, ad.adrelid), ''))
    FROM relation r
    JOIN pg_attribute a ON a.attrelid = r.oid AND a.attnum > 0 AND NOT a.attisdropped
    LEFT JOIN pg_attrdef ad ON ad.adrelid = r.oid AND ad.adnum = a.attnum
    WHERE r.relkind IN ('r', 'p', 'v', 'm')
    UNION ALL
    SELECT format(
        'constraint %s.%s %s',
        r.relname,
        con.conname,
        replace(
            pg_get_constraintdef(con.oid),
            ' REFERENCES ' || quote_ident(current_schema()) || '.',
            ' REFERENCES '))
    FROM relation r
    JOIN pg_constraint con ON con.conrelid = r.oid
    UNION ALL
    SELECT format(
        'index %s.%s %s',
        r.relname,
        i.relname,
        replace(
            pg_get_indexdef(i.oid),
            ' ON ' || quote_ident(current_schema()) || '.',
            ' ON '))
    FROM relation r
    JOIN pg_index x ON x.indrelid = r.oid
    JOIN pg_class i ON i.oid = x.indexrelid
    UNION ALL
    SELECT format(
        'sequence %s %s start %s increment %s min %s max %s cache %s%s',
        r.relname,
        format_type(s.seqtypid, NULL),
        s.seqstart,
        s.seqincrement,
        s.seqmin,
        s.seqmax,
        s.seqcache,
        CASE WHEN s.seqcycle THEN ' cycle' ELSE '' END)
    FROM relation r
    JOIN pg_sequence s ON s.seqrelid = r.oid
    UNION ALL
    SELECT format('trigger %s.%s', r.relname, t.tgname)
    FROM relation r
    JOIN pg_trigger t ON t.tgrelid = r.oid AND NOT t.tgisinternal
    UNION ALL
    SELECT format('function %s(%s)', p.proname, pg_get_function_identity_arguments(p.oid))
    FROM pg_proc p
    WHERE p.pronamespace = current_schema()::regnamespace
      AND NOT EXISTS (
          SELECT 1 FROM pg_depend d
          WHERE d.classid = 'pg_proc'::regclass AND d.objid = p.oid AND d.deptype = 'e')
    UNION ALL
    SELECT format('type %s %s', t.typname, t.typtype)
    FROM pg_type t
    WHERE t.typnamespace = current_schema()::regnamespace
      AND t.typtype IN ('c', 'd', 'e', 'r')
      AND (t.typrelid = 0 OR EXISTS (
          SELECT 1 FROM pg_class c WHERE c.oid = t.typrelid AND c.relkind = 'c'))
      AND NOT EXISTS (
          SELECT 1 FROM pg_depend d
          WHERE d.classid = 'pg_type'::regclass AND d.objid = t.oid AND d.deptype = 'e')
)
SELECT text FROM line ORDER BY text COLLATE "C";
