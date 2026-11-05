CREATE TABLE IF NOT EXISTS tags(
    ordering_id BIGINT NOT NULL,
    tag NVARCHAR(64) NOT NULL,
    sequence_nr BIGINT NOT NULL,
    persistence_id VARCHAR(255),
    PRIMARY KEY (ordering_id, tag, persistence_id)
);

DROP PROCEDURE IF EXISTS AkkaMigration_Split;

DELIMITER ??
CREATE PROCEDURE AkkaMigration_Split(IN fromId INT, IN toId INT)
BEGIN

    DECLARE v_cursor_done TINYINT UNSIGNED DEFAULT 0;
    DECLARE Id INT UNSIGNED;
    DECLARE String VARCHAR(8000);
    DECLARE PId VARCHAR(255);
    DECLARE SeqNr INT UNSIGNED;
    DECLARE idx INT UNSIGNED;
    DECLARE slice VARCHAR(8000);

    DECLARE v_cursor CURSOR FOR
        SELECT ej.`ordering`, ej.tags 
        FROM event_journal ej
        WHERE ej.`ordering` >= fromId AND ej.`ordering` <= toId
        ORDER BY ej.`ordering`;
    DECLARE CONTINUE HANDLER FOR NOT FOUND
        SET v_cursor_done = 1;

    OPEN v_cursor;
    REPEAT
        FETCH v_cursor INTO Id, String, SeqNr, PId;
        SET idx = 1;

        IF String IS NULL OR LENGTH(String) < 1 THEN
            SET idx = 0;
        END IF;

        WHILE idx != 0 DO
            SET idx = LOCATE(';', String);
            IF idx != 0 THEN
                SET slice = LEFT(String, idx - 1);
            ELSE
                SET slice = String;
            END IF;

            IF LENGTH(slice) > 0 THEN
                INSERT IGNORE INTO tags (ordering_id, tag, sequence_nr, persistence_id) VALUES (Id, slice, SeqNr, PId);
            END IF;

            SET String = RIGHT(String, LENGTH(String) - idx);

            IF LENGTH(String) = 0 THEN
                SET idx = 0;
            END IF;
        END WHILE;
    UNTIL v_cursor_done END REPEAT;
    
    CLOSE v_cursor;

END??

CREATE PROCEDURE AkkaMigration_BatchedMigration(IN fromId BIGINT)
BEGIN
    DECLARE maxId BIGINT UNSIGNED;
    DECLARE oldCommitValue TINYINT DEFAULT @@autocommit;

    SELECT maxId = MAX(ej.`ordering`)
    FROM event_journal ej
    WHERE
        ej.`tags` IS NOT null
      AND LENGTH(ej.`tags`) > 0
      AND ej.`ordering` NOT IN (SELECT t.`ordering_id` FROM tags t);

    loopLabel: LOOP
        IF fromId > maxId THEN
            LEAVE loopLabel;
        END IF;

        SET autocommit = 0;
        START TRANSACTION;
        CALL AkkaMigration_Split(fromId, fromId + 1000);
        COMMIT;
        SET autocommit = oldCommitValue;

        SET fromId = fromId + 1000;
    END LOOP;
END ??

DELIMITER ;