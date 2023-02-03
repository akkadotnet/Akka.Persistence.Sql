IF NOT EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLES
              WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'tags')
BEGIN
    CREATE TABLE [dbo].[tags](
        ordering_id BIGINT NOT NULL,
        tag NVARCHAR(64) NOT NULL,
        PRIMARY KEY (ordering_id, tag)
    );
END
GO

CREATE OR ALTER FUNCTION [dbo].[Split](@String VARCHAR(8000), @Delimiter CHAR(1))
    RETURNS @temptable TABLE (items VARCHAR(8000)) AS
BEGIN
    DECLARE @idx INT
    DECLARE @slice VARCHAR(8000)

    SELECT @idx = 1
    IF LEN(@String) < 1 OR @String is NULL
        RETURN

    WHILE @idx != 0
        BEGIN
            SET @idx = CHARINDEX(@Delimiter, @String)
            IF @idx != 0
                SET @slice = LEFT(@String,@idx - 1)
            ELSE
                SET @slice = @String

            IF(LEN(@slice) > 0)
                INSERT INTO @temptable(Items) VALUES(@slice)

            SET @String = RIGHT(@String, LEN(@String) - @idx)
            IF len(@String) = 0
                BREAK
        END
    RETURN
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[BatchedMigration](@from_id BIGINT) AS
BEGIN
    DECLARE @max_id BIGINT;
    
    SELECT @max_id = MAX(ej.[Ordering])
    FROM [dbo].[EventJournal] ej
    WHERE
        ej.[Tags] IS NOT NULL
      AND LEN(ej.[Tags]) > 0
      AND ej.[Ordering] NOT IN (SELECT t.[ordering_id] FROM [dbo].[tags] t);
        
    WHILE @from_id <= @max_id
    BEGIN
        BEGIN TRAN;
        INSERT INTO [dbo].[tags]([ordering_id], [tag])
        SELECT * FROM (SELECT records.[Ordering], cross_product.[items] FROM (
            SELECT *
            FROM [dbo].[EventJournal] AS ej
            WHERE ej.[Ordering] >= @from_id AND ej.[Ordering] <= @from_id + 1000
        ) AS records CROSS APPLY [dbo].[Split](records.Tags, ';') cross_product) AS s([ordering_id], [tag])
        WHERE NOT EXISTS (
                SELECT * FROM [dbo].[tags] t WITH (updlock)
                WHERE s.[ordering_id] = t.[ordering_id] AND s.[tag] = t.[tag]
            );
        COMMIT TRAN;
        
        SET @from_id = @from_id + 1000;
    END
END;
