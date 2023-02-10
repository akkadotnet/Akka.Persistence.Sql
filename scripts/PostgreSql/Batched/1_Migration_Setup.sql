CREATE TABLE IF NOT EXISTS "public"."tags"(
    ordering_id BIGINT NOT NULL,
    tag VARCHAR(64) NOT NULL,
    sequence_nr BIGINT NOT NULL,
    persistence_id VARCHAR(255) NOT NULL,
    PRIMARY KEY (ordering_id, tag)
);

CREATE OR REPLACE PROCEDURE "public"."Split"(id bigint, tags varchar(8000), seq_nr bigint, pid varchar(255)) AS $$
DECLARE var_t record;
BEGIN
    FOR var_t IN(SELECT unnest(string_to_array(tags, ';')) AS t) 
    LOOP 
		CONTINUE WHEN var_t.t IS NULL OR var_t.t = '';
        INSERT INTO "public"."tags" (ordering_id, tag, sequence_nr, persistence_id)
            VALUES (id, var_t.t, seq_nr, pid)
            ON CONFLICT DO NOTHING;
    END LOOP;
END
$$ LANGUAGE plpgsql;

CREATE OR REPLACE PROCEDURE "public"."Normalize"(IN fromId BIGINT, IN toId BIGINT) AS $$
DECLARE var_r record;
BEGIN
    FOR var_r IN(
        SELECT ej."ordering" AS id, ej."tags", ej."sequence_nr" as seq_nr, ej."persistence_id" AS pid 
        FROM "public"."event_journal" AS ej
        WHERE ej.ordering >= fromId AND ej.ordering <= toId
        ORDER BY "ordering") 
    LOOP
        CALL "public"."Split"(var_r.id, var_r.tags, var_r.seq_nr, var_r.pid);
    END LOOP;
END
$$ LANGUAGE plpgsql;

CREATE OR REPLACE PROCEDURE "public"."BatchedMigration"(IN from_id BIGINT) AS $$
DECLARE max_id BIGINT;
BEGIN
    max_id := (SELECT MAX(ej."ordering")
        FROM event_journal ej
        WHERE
            ej."tags" IS NOT NULL
            AND LENGTH(ej."tags") > 0
            AND ej."ordering" NOT IN (SELECT t."ordering_id" FROM "public"."tags" t));

    LOOP 
        EXIT WHEN from_id > max_id;

        CALL "public"."Normalize"(from_id, from_id + 1000);
        COMMIT;
        
        from_id := from_id + 1000;
    END LOOP;
END;
$$ LANGUAGE plpgsql;