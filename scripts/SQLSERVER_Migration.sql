DROP TABLE IF EXISTS [dbo].[TagTable];

DROP FUNCTION IF EXISTS [dbo].[Split];

CREATE TABLE dbo.TagTable(
  ordering_id BIGINT NOT NULL,
  tag NVARCHAR(64) NOT NULL,
  PRIMARY KEY  (ordering_id, tag)
);
GO

CREATE FUNCTION [dbo].[Split](@String varchar(8000), @Delimiter char(1))
	RETURNS @temptable TABLE (items varchar(8000)) AS
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

BEGIN TRANSACTION 
INSERT INTO dbo.TagTable(ordering_id, tag)
SELECT a.Ordering, b.items
	FROM EventJournal AS a
	CROSS APPLY dbo.Split(a.Tags, ';') b;
COMMIT