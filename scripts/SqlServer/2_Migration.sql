INSERT INTO [dbo].[tags]([ordering_id], [tag])
    SELECT * FROM (
        SELECT a.[Ordering], b.[items] FROM
            [dbo].[EventJournal] AS a
            CROSS APPLY [dbo].[Split](a.Tags, ';') b
    ) AS s([ordering_id], [tag])
    WHERE NOT EXISTS (
        SELECT * FROM [dbo].[tags] t WITH (updlock)
        WHERE s.[ordering_id] = t.[ordering_id] AND s.[tag] = t.[tag]
    );