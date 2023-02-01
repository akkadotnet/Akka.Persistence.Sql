CREATE TABLE IF NOT EXISTS "public"."TagTable"(
    ordering_id BIGINT NOT NULL,
    tag VARCHAR(64) NOT NULL,
    PRIMARY KEY (ordering_id, tag)
);

CREATE OR REPLACE PROCEDURE "public"."Split"(id bigint, tags varchar(8000)) AS $$
DECLARE var_t record;
BEGIN
    FOR var_t IN(SELECT unnest(string_to_array(tags, ';')) AS t) 
    LOOP 
		CONTINUE WHEN var_t.t IS NULL OR var_t.t = '';
        INSERT INTO "public"."TagTable" (ordering_id, tag) 
            VALUES (id, var_t.t)
		    ON CONFLICT DO NOTHING;
    END LOOP;
END
$$ LANGUAGE plpgsql;

CREATE OR REPLACE PROCEDURE "public"."Normalize"() AS $$
DECLARE var_r record;
BEGIN
    FOR var_r IN(SELECT ej."ordering" AS id, ej.tags FROM "public"."event_journal" AS ej ORDER BY "ordering") 
    LOOP 
		CALL "public"."Split"(var_r.id, var_r.tags);
    END LOOP;
END
$$ LANGUAGE plpgsql;
