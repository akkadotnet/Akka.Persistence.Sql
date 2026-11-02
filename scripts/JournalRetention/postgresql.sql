-- Example: delete journal events older than 90 days, together with their tags.
-- Uses the default table mapping. Adapt the cutoff, batch size, and names.

BEGIN;

CREATE TEMP TABLE expired ON COMMIT DROP AS
SELECT j.ordering
FROM journal j
WHERE j.created < (EXTRACT(EPOCH FROM now() - INTERVAL '90 days') * 10000000)::bigint
                  + 621355968000000000                  -- created is .NET ticks
  AND j.sequence_number < (SELECT max(m.sequence_number) -- keep each entity's latest event
                           FROM journal m
                           WHERE m.persistence_id = j.persistence_id)
LIMIT 10000;

DELETE FROM tags    WHERE ordering_id IN (SELECT ordering FROM expired);
DELETE FROM journal WHERE ordering    IN (SELECT ordering FROM expired);

COMMIT;
