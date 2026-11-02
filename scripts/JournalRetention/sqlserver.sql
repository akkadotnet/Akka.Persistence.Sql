-- Example: delete journal events older than 90 days, together with their tags.
-- Uses the default table mapping. Adapt the cutoff, batch size, and names.

DECLARE @cutoff bigint =                                  -- created is .NET ticks
    DATEDIFF_BIG(MILLISECOND, '1970-01-01', DATEADD(DAY, -90, SYSUTCDATETIME())) * 10000
    + 621355968000000000;

DECLARE @expired TABLE (ordering bigint PRIMARY KEY);

INSERT INTO @expired (ordering)
SELECT TOP (10000) j.ordering
FROM dbo.journal j
WHERE j.created < @cutoff
  AND j.sequence_number < (SELECT MAX(m.sequence_number)  -- keep each entity's latest event
                           FROM dbo.journal m
                           WHERE m.persistence_id = j.persistence_id);

BEGIN TRANSACTION;
DELETE FROM dbo.tags    WHERE ordering_id IN (SELECT ordering FROM @expired);
DELETE FROM dbo.journal WHERE ordering    IN (SELECT ordering FROM @expired);
COMMIT TRANSACTION;
