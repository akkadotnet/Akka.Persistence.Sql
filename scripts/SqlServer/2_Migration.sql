INSERT INTO [dbo].[tags]([ordering_id], [tag], [sequence_nr], [persistence_id])
    SELECT * FROM (
        SELECT records.[Ordering], cross_product.[items], records.SequenceNr, records.PersistenceId FROM
            [dbo].[EventJournal] AS records
            CROSS APPLY [dbo].[AkkaMigration_Split](records.Tags, ';') cross_product
    ) AS s([ordering_id], [tag], [sequence_nr], [persistence_id])
    WHERE NOT EXISTS (
        SELECT * FROM [dbo].[tags] t WITH (updlock)
        WHERE s.[ordering_id] = t.[ordering_id] AND s.[tag] = t.[tag]
    );