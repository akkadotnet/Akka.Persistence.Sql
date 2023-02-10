CREATE TABLE IF NOT EXISTS "public"."tags"(
    ordering_id BIGINT NOT NULL,
    tag VARCHAR(64) NOT NULL,
    sequence_nr BIGINT NOT NULL,
    persistence_id VARCHAR(255) NOT NULL,
    PRIMARY KEY (ordering_id, tag, persistence_id)
);

CREATE OR REPLACE PROCEDURE "public"."AkkaMigration_Split"(id bigint, tags varchar(8000), seq_nr bigint, pid varchar(255)) AS $$
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

CREATE OR REPLACE PROCEDURE "public"."AkkaMigration_Normalize"() AS $$
DECLARE var_r record;
BEGIN
    FOR var_r IN(SELECT ej."ordering" AS id, ej."tags", ej."sequence_nr" as seq_nr, ej."persistence_id" AS pid FROM "public"."event_journal" AS ej ORDER BY "ordering") 
    LOOP 
		CALL "public"."AkkaMigration_Split"(var_r.id, var_r.tags, var_r.seq_nr, var_r.pid);
    END LOOP;
END
$$ LANGUAGE plpgsql;
