-- Example: delete journal events older than 90 days, together with their tags.
-- Uses the default table mapping. Adapt the cutoff, batch size, and names.

CREATE TEMPORARY TABLE expired AS
SELECT j.ordering
FROM journal j
WHERE j.created < (UNIX_TIMESTAMP() - 90 * 86400) * 10000000
                  + 621355968000000000                  -- created is .NET ticks
  AND j.sequence_number < (SELECT MAX(m.sequence_number) -- keep each entity's latest event
                           FROM journal m
                           WHERE m.persistence_id = j.persistence_id)
LIMIT 10000;

START TRANSACTION;
DELETE t FROM tags t    JOIN expired e ON t.ordering_id = e.ordering;
DELETE j FROM journal j JOIN expired e ON j.ordering    = e.ordering;
COMMIT;

DROP TEMPORARY TABLE expired;
