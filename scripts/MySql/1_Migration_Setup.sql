CREATE TABLE IF NOT EXISTS tags(
    ordering_id BIGINT NOT NULL,
    tag NVARCHAR(64) NOT NULL,
    PRIMARY KEY (ordering_id, tag)
);

DROP PROCEDURE IF EXISTS Split;
DELIMITER #
CREATE PROCEDURE Split()
proc_main: BEGIN

    DECLARE v_cursor_done TINYINT UNSIGNED DEFAULT 0;
    DECLARE Id INT UNSIGNED;
    DECLARE String VARCHAR(8000);
    DECLARE idx INT UNSIGNED;
    DECLARE slice VARCHAR(8000);

    DECLARE v_cursor CURSOR FOR
        SELECT ej.`ordering`, ej.tags FROM event_journal ej ORDER BY `ordering`;
    DECLARE CONTINUE HANDLER FOR NOT FOUND
        SET v_cursor_done = 1;

    OPEN v_cursor;
    REPEAT
        FETCH v_cursor INTO Id, String;
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
                INSERT IGNORE INTO tags (ordering_id, tag) VALUES (Id, slice);
            END IF;

            SET String = RIGHT(String, LENGTH(String) - idx);

            IF LENGTH(String) = 0 THEN
                SET idx = 0;
            END IF;
        END WHILE;
    UNTIL v_cursor_done END REPEAT;
    
    CLOSE v_cursor;

END proc_main #
DELIMITER ;