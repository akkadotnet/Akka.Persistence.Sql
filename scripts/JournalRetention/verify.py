#!/usr/bin/env python3
"""Exercise the journal retention scripts in disposable databases on two local containers.

Requires Docker and running PostgreSQL/SQL Server containers with their native
clients. Credentials stay inside each container; only synthetic data is used.
"""

import argparse
from pathlib import Path
import re
import subprocess
import time
import uuid


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
SCRIPT = "delete_expired_events.sql"
UNIX_EPOCH_TICKS = 621355968000000000
NOW_TICKS = int(time.time() * 10_000_000) + UNIX_EPOCH_TICKS
OLD_TICKS = NOW_TICKS - 200 * 86_400 * 10_000_000


class Raw(str):
    """A SQL Server configuration value inserted without quoting."""


class Database:
    def __init__(self, provider, container):
        self.provider = provider
        self.pg = provider == "PostgreSql"
        self.container = container
        self.name = "journal_retention_verify_" + uuid.uuid4().hex
        self.checks = 0

    def quote(self, identifier):
        if self.pg:
            return '"' + identifier.replace('"', '""') + '"'
        return "[" + identifier.replace("]", "]]") + "]"

    def execute(self, sql, *, master=False, fail=False):
        database = ("postgres" if self.pg else "master") if master else self.name
        if self.pg:
            command = [
                "docker", "exec", "-i", self.container, "sh", "-c",
                'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "${POSTGRES_USER:-postgres}" -d "$1"',
                "sh", database,
            ]
        else:
            command = [
                "docker", "exec", "-i", self.container, "sh", "-c",
                'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; '
                'exec /opt/mssql-tools18/bin/sqlcmd -C -b -r 1 -S localhost -U sa -d "$1" -h -1 -W',
                "sh", database,
            ]
        result = subprocess.run(command, input=sql, text=True, capture_output=True, timeout=120)
        output = result.stdout + result.stderr
        if fail:
            if result.returncode == 0:
                raise AssertionError("Expected SQL rejection, but operation succeeded: " + output)
            if not re.search(fail, output, re.IGNORECASE):
                raise AssertionError("SQL failed for an unexpected reason: " + output)
            self.checks += 1
        elif result.returncode:
            raise AssertionError(output)
        return output

    def scalar(self, sql):
        output = self.execute(sql)
        numbers = [int(line.strip()) for line in output.splitlines() if re.fullmatch(r"-?\d+", line.strip())]
        if len(numbers) != 1:
            raise AssertionError("Expected one integer result: " + output)
        return numbers[0]

    def expect(self, actual, expected, label):
        if actual != expected:
            raise AssertionError(f"{label}: expected {expected!r}, got {actual!r}")
        self.checks += 1

    def expect_output(self, output, pattern, label):
        if not re.search(pattern, output):
            raise AssertionError(f"{label}: output did not match {pattern!r}: {output}")
        self.checks += 1

    def configure(self, path, config):
        sql = path.read_text()
        for key, value in config.items():
            if self.pg:
                escaped = str(value).replace("\\", "\\\\").replace("'", "\\'")
                pattern = rf"(?m)^\\set {re.escape(key)} '[^\n]*'$"
                replacement = f"\\set {key} '{escaped}'"
                sql, count = re.subn(pattern, lambda _m, r=replacement: r, sql)
            else:
                escaped = str(value).replace("'", "''")
                literal = str(value) if isinstance(value, Raw) or not isinstance(value, str) else f"N'{escaped}'"
                pattern = rf"(?m)^(DECLARE @{re.escape(key)} \w+(?:\(\d+\))? = )[^;]*;"
                sql, count = re.subn(pattern, lambda m, lit=literal: m.group(1) + lit + ";", sql)
            if count != 1:
                raise AssertionError(f"Cannot configure {path.name}: {key}")
        return sql

    def retention(self, config, *, fail=False):
        return self.execute(self.configure(HERE / self.provider / SCRIPT, config), fail=fail)

    def flag(self, value):
        return ("true" if value else "false") if self.pg else int(value)

    def number(self, value):
        return str(value) if self.pg else value

    def mapping_config(self, mapping):
        pg = self.pg
        config = {
            "schema_name": "public" if pg else "dbo",
            "journal_table": "journal",
            "journal_ordering_column": "ordering",
            "journal_persistence_id_column": "persistence_id",
            "journal_sequence_number_column": "sequence_number",
            "journal_created_column": "created",
            "tag_table": "tags",
            "tag_ordering_id_column": "ordering_id",
        }
        if mapping == "compat":
            config.update({
                "journal_table": "event_journal" if pg else "EventJournal",
                "journal_ordering_column": "ordering" if pg else "Ordering",
                "journal_persistence_id_column": "persistence_id" if pg else "PersistenceId",
                "journal_sequence_number_column": "sequence_nr" if pg else "SequenceNr",
                "journal_created_column": "created_at" if pg else "Timestamp",
            })
        if mapping == "custom":
            config.update({
                "schema_name": "test schema']\"",
                "journal_table": "event journal']\"",
                "journal_ordering_column": "event ordering']\"",
                "journal_persistence_id_column": "entity id']\"",
                "journal_sequence_number_column": "entity sequence']\"",
                "journal_created_column": "written at']\"",
                "tag_table": "event tags']\"",
                "tag_ordering_id_column": "tag ordering']\"",
            })
        return config

    def run_case(self, mapping):
        pg = self.pg
        q = self.quote
        config = self.mapping_config(mapping)
        journal = q(config["schema_name"]) + "." + q(config["journal_table"])
        tags = q(config["schema_name"]) + "." + q(config["tag_table"])
        ordering = q(config["journal_ordering_column"])
        persistence_id = q(config["journal_persistence_id_column"])
        sequence_number = q(config["journal_sequence_number_column"])
        created = q(config["journal_created_column"])
        tag_ordering = q(config["tag_ordering_id_column"])
        payload = "decode('01','hex')" if pg else "0x01"

        if mapping == "custom":
            identity = "bigint GENERATED BY DEFAULT AS IDENTITY" if pg else "bigint IDENTITY"
            self.execute(f"CREATE SCHEMA {q(config['schema_name'])};")
            self.execute(
                f"CREATE TABLE {journal} ({ordering} {identity} PRIMARY KEY, "
                f"{persistence_id} varchar(255) NOT NULL, {sequence_number} bigint NOT NULL, "
                f"{created} bigint NOT NULL, message {'bytea' if pg else 'varbinary(max)'} NOT NULL);")
            self.execute(f"CREATE TABLE {tags} ({tag_ordering} bigint NOT NULL, tag varchar(64) NOT NULL, PRIMARY KEY ({tag_ordering}, tag));")
            message_column = "message"
            tag_extra_columns, tag_extra = "", lambda pid, seq: ""
        else:
            directory = ROOT / "docs" / "ddl" / mapping / ("postgresql" if pg else "sqlserver")
            self.execute((directory / "journal.sql").read_text())
            self.execute((directory / "journal-tags.sql").read_text())
            message_column = "message" if mapping == "default" else ("payload" if pg else "Payload")
            tag_extra_columns = ", sequence_nr, persistence_id"
            tag_extra = lambda pid, seq: f", {seq}, '{pid}'"
        message = q(message_column)

        def add_events(pid, events):
            rows = ",".join(f"('{pid}', {seq}, {ticks}, {payload})" for seq, ticks in events)
            self.execute(f"INSERT INTO {journal} ({persistence_id}, {sequence_number}, {created}, {message}) VALUES {rows};")
            for seq, _ in events:
                row = self.scalar(f"SELECT {ordering} FROM {journal} WHERE {persistence_id}='{pid}' AND {sequence_number}={seq};")
                self.execute(
                    f"INSERT INTO {tags} ({tag_ordering}, tag{tag_extra_columns}) VALUES "
                    f"({row}, 'a'{tag_extra(pid, seq)}), ({row}, 'b'{tag_extra(pid, seq)});")

        # Entity A: three expired events and two current ones. Entity B: only
        # expired events, so its highest sequence number must survive.
        add_events("entity-a", [(1, OLD_TICKS), (2, OLD_TICKS), (3, OLD_TICKS), (4, NOW_TICKS), (5, NOW_TICKS)])
        add_events("entity-b", [(1, OLD_TICKS), (2, OLD_TICKS), (3, OLD_TICKS)])
        # An unrelated orphan tag: retention must not touch it.
        self.execute(f"INSERT INTO {tags} ({tag_ordering}, tag{tag_extra_columns}) VALUES (999999, 'orphan'{tag_extra('gone', 1)});")

        events = lambda: self.scalar(f"SELECT COUNT(*) FROM {journal};")
        tag_rows = lambda: self.scalar(f"SELECT COUNT(*) FROM {tags};")
        def survivors(pid):
            output = self.execute(
                f"SELECT {sequence_number} FROM {journal} WHERE {persistence_id}='{pid}' ORDER BY {sequence_number};")
            return [token for token in output.split() if token.isdigit()]

        report = self.retention(config)
        self.expect_output(report, r"eligible for deletion: 5\b", "report counts eligible events")
        self.expect((events(), tag_rows()), (8, 17), "report mode preserves all rows")

        execute = {**config, "execute_delete": self.flag(True)}
        self.retention({**execute, "retention_days": self.number(0)}, fail="retention_days must be")
        self.retention({**execute, "batch_size": self.number(0)}, fail="batch_size must be")
        self.retention({**execute, "delete_tags": "maybe" if pg else Raw("NULL")}, fail="delete_tags must be")
        self.retention({**execute, "tag_table": config["tag_table"] + "x" * 129}, fail="identifier")
        self.retention({**execute, "tag_table": "missing_tags"}, fail="tag table does not exist")
        self.retention({**execute, "journal_table": "missing_journal"}, fail="journal table does not exist")
        self.expect((events(), tag_rows()), (8, 17), "rejected runs delete nothing")

        batch = {**execute, "batch_size": self.number(2)}
        for expected_events, remain in [(6, "yes"), (4, "yes"), (3, "no"), (3, "no")]:
            output = self.retention(batch)
            self.expect_output(output, rf"More eligible events remain: {remain}", "execute reports remaining work")
            self.expect(events(), expected_events, "bounded delete and resume")
            self.expect(tag_rows(), expected_events * 2 + 1, "tags removed with their events")
        self.expect(survivors("entity-a"), ["4", "5"], "current events retained")
        self.expect(survivors("entity-b"), ["3"], "highest sequence number retained")
        self.expect(self.scalar(f"SELECT COUNT(*) FROM {tags} WHERE {tag_ordering}=999999;"), 1, "unrelated orphan untouched")

        # With the optional FK installed, the tags-first delete still succeeds.
        if mapping == "default":
            self.execute(f"DELETE FROM {tags} WHERE {tag_ordering}=999999;")
            fk_config = {key: config[key] for key in ["schema_name", "journal_table", "journal_ordering_column", "tag_table", "tag_ordering_id_column"]}
            fk_script = ROOT / "scripts" / "TagTableForeignKey" / self.provider / "02_add_foreign_key.sql"
            self.execute(self.configure(fk_script, fk_config))
            add_events("entity-c", [(1, OLD_TICKS), (2, OLD_TICKS)])
            self.retention(execute)
            self.expect(survivors("entity-c"), ["2"], "delete succeeds with the FK installed")
            self.expect(self.scalar(f"SELECT COUNT(*) FROM {tags} t WHERE NOT EXISTS (SELECT 1 FROM {journal} j WHERE j.{ordering} = t.{tag_ordering});"), 0, "no orphans with the FK")
            self.execute(self.configure(ROOT / "scripts" / "TagTableForeignKey" / self.provider / "03_drop_foreign_key.sql", fk_config))

        # Csv tag mode: no tag table, delete_tags off.
        self.execute(f"DROP TABLE {tags};")
        self.retention(execute, fail="tag table does not exist")
        add_events_without_tags = ",".join(f"('entity-d', {seq}, {OLD_TICKS}, {payload})" for seq in [1, 2, 3])
        self.execute(f"INSERT INTO {journal} ({persistence_id}, {sequence_number}, {created}, {message}) VALUES {add_events_without_tags};")
        output = self.retention({**execute, "delete_tags": self.flag(False)})
        self.expect_output(output, r"and 0 tag rows", "no tag rows deleted without a tag table")
        self.expect(survivors("entity-d"), ["3"], "delete_tags off still deletes expired events")

        self.execute(f"DROP TABLE {journal};")
        if mapping == "custom":
            self.execute(f"DROP SCHEMA {q(config['schema_name'])};")
        print(f"PASS {self.provider}: {mapping} mapping")

    def run(self):
        self.execute(f"CREATE DATABASE {self.quote(self.name)};", master=True)
        try:
            for mapping in ["default", "compat", "custom"]:
                self.run_case(mapping)
        finally:
            if not self.pg:
                self.execute(f"ALTER DATABASE {self.quote(self.name)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", master=True)
            self.execute(f"DROP DATABASE {self.quote(self.name)};", master=True)
        print(f"PASS {self.provider}: {self.checks} assertions; disposable database removed")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--postgres-container", required=True)
    parser.add_argument("--sqlserver-container", required=True)
    args = parser.parse_args()
    for provider, container in [("PostgreSql", args.postgres_container), ("SqlServer", args.sqlserver_container)]:
        Database(provider, container).run()


if __name__ == "__main__":
    main()
