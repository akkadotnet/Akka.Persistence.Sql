#!/usr/bin/env python3
"""Exercise the operator scripts in disposable databases on two local containers.

Requires Docker and running PostgreSQL/SQL Server containers with their native
clients. Credentials stay inside each container; only synthetic data is used.
"""

import argparse
from pathlib import Path
import re
import subprocess
import uuid


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
FK_NAME = "tags_ordering_id_journal_ordering_fk"


class Database:
    def __init__(self, provider, container):
        self.provider = provider
        self.container = container
        self.name = "tag_fk_verify_" + uuid.uuid4().hex
        self.checks = 0

    def quote(self, identifier):
        if self.provider == "PostgreSql":
            return '"' + identifier.replace('"', '""') + '"'
        return "[" + identifier.replace("]", "]]") + "]"

    def execute(self, sql, *, master=False, fail=False):
        database = ("postgres" if self.provider == "PostgreSql" else "master") if master else self.name
        if self.provider == "PostgreSql":
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
        if fail:
            if result.returncode == 0:
                raise AssertionError("Expected SQL rejection, but operation succeeded")
            if not isinstance(fail, str) or not re.search(fail, result.stdout + result.stderr, re.IGNORECASE):
                raise AssertionError("SQL failed for an unexpected reason: " + result.stdout + result.stderr)
            self.checks += 1
        elif result.returncode:
            raise AssertionError(result.stdout + result.stderr)
        return result.stdout

    def scalar(self, sql):
        output = self.execute(sql)
        numbers = [int(line.strip()) for line in output.splitlines() if re.fullmatch(r"-?\d+", line.strip())]
        if len(numbers) != 1:
            raise AssertionError("Expected one integer result: " + output)
        return numbers[0]

    def expect(self, actual, expected, label):
        if actual != expected:
            raise AssertionError(f"{label}: expected {expected}, got {actual}")
        self.checks += 1

    def script(self, filename, config, *, fail=False):
        sql = (HERE / self.provider / filename).read_text()
        for key, value in config.items():
            if self.provider == "PostgreSql":
                escaped = str(value).replace("\\", "\\\\").replace("'", "\\'")
                pattern = rf"(?m)^\\set {re.escape(key)} '[^\n]*'$"
                replacement = f"\\set {key} '{escaped}'"
            else:
                escaped = str(value).replace("'", "''")
                pattern = rf"(?m)^(DECLARE @{re.escape(key)} \w+(?:\(\d+\))? = )[^;]*;"
                literal = f"N'{escaped}'" if isinstance(value, str) else str(value)
                replacement = lambda match, literal=literal: match.group(1) + literal + ";"
            sql, count = re.subn(pattern, (lambda _match, value=replacement: value) if isinstance(replacement, str) else replacement, sql)
            if count != 1:
                raise AssertionError(f"Cannot configure {filename}: {key}")
        return self.execute(sql, fail=fail)

    def run_case(self, mapping):
        pg = self.provider == "PostgreSql"
        config = {
            "schema_name": "public" if pg else "dbo",
            "journal_table": "journal",
            "journal_ordering_column": "ordering",
            "tag_table": "tags",
            "tag_ordering_id_column": "ordering_id",
        }
        if mapping == "compat":
            config["journal_table"] = "event_journal" if pg else "EventJournal"
            config["journal_ordering_column"] = "ordering" if pg else "Ordering"
        if mapping == "custom":
            config.update({
                "schema_name": "test schema']\"",
                "journal_table": "event journal']\"",
                "journal_ordering_column": "event ordering']\"",
                "tag_table": "event tags']\"",
                "tag_ordering_id_column": "tag ordering']\"",
            })
        q = self.quote
        journal = q(config["schema_name"]) + "." + q(config["journal_table"])
        tags = q(config["schema_name"]) + "." + q(config["tag_table"])
        ordering = q(config["journal_ordering_column"])
        child_ordering = q(config["tag_ordering_id_column"])
        if mapping == "custom":
            self.execute(f"CREATE SCHEMA {q(config['schema_name'])};")
            self.execute(f"CREATE TABLE {journal} ({ordering} bigint PRIMARY KEY, deleted {'boolean' if pg else 'bit'} NOT NULL);")
            self.execute(f"CREATE TABLE {tags} ({child_ordering} bigint NOT NULL, tag varchar(64) NOT NULL, custom_timestamp bigint NOT NULL DEFAULT 123, PRIMARY KEY ({child_ordering}, tag));")
            self.execute(f"INSERT INTO {journal} VALUES (1, {'false' if pg else '0'}), (2, {'true' if pg else '1'});")
        else:
            directory = ROOT / "docs" / "ddl" / mapping / ("postgresql" if pg else "sqlserver")
            self.execute((directory / "journal.sql").read_text())
            self.execute((directory / "journal-tags.sql").read_text())
            if mapping == "default":
                columns = ["deleted", "persistence_id", "sequence_number", "created", "message"]
            elif pg:
                columns = ["is_deleted", "persistence_id", "sequence_nr", "created_at", "payload"]
            else:
                columns = ["IsDeleted", "PersistenceId", "SequenceNr", "Timestamp", "Payload"]
            fields = ",".join(q(value) for value in columns)
            payload = "decode('01','hex')" if pg else "0x01"
            self.execute(f"INSERT INTO {journal} ({fields}) VALUES ({'false' if pg else '0'}, 'synthetic', 1, 0, {payload}), ({'true' if pg else '1'}, 'synthetic', 2, 0, {payload});")
        # Two live-parent tags, two soft-deleted-parent tags, and five orphan tags.
        suffix_columns = "" if mapping == "custom" else ", sequence_nr, persistence_id"
        values = []
        for identity, label in [(1, "a"), (1, "b"), (2, "a"), (2, "b"), (3, "a"), (3, "b"), (4, "a"), (4, "b"), (5, "a")]:
            suffix = "" if mapping == "custom" else f", {identity}, 'synthetic'"
            values.append(f"({identity}, '{label}'{suffix})")
        self.execute(f"INSERT INTO {tags} ({child_ordering}, tag{suffix_columns}) VALUES " + ",".join(values) + ";")
        count = lambda: self.scalar(f"SELECT COUNT(*) FROM {tags};")
        self.script("01_cleanup_orphan_tags.sql", config)
        self.expect(count(), 9, "dry run preserves all rows")
        oversized = {**config, "tag_table": config["tag_table"] + "x" * 129}
        for filename in ["01_cleanup_orphan_tags.sql", "02_add_foreign_key.sql", "03_drop_foreign_key.sql"]:
            self.script(filename, oversized, fail="identifier")
        self.expect(count(), 9, "oversized identifiers cannot select a truncated name")
        self.script("02_add_foreign_key.sql", config, fail="orphan tag rows remain")
        self.expect(count(), 9, "migration rejects orphans without deleting them")
        self.script("01_cleanup_orphan_tags.sql", {**config, "execute_cleanup": "true" if pg else 1, "batch_size": "0" if pg else 0}, fail="batch_size must be")
        self.expect(count(), 9, "invalid batch size cannot delete rows")
        for expected in [7, 5, 4, 4]:
            self.script("01_cleanup_orphan_tags.sql", {**config, "execute_cleanup": "true" if pg else 1, "batch_size": "2" if pg else 2})
            self.expect(count(), expected, "bounded cleanup and resume")
        self.expect(self.scalar(f"SELECT COUNT(*) FROM {tags} WHERE {child_ordering}=2;"), 2, "soft-deleted parent's tags survive cleanup")
        self.script("02_add_foreign_key.sql", config)
        self.script("02_add_foreign_key.sql", config)
        self.script("01_cleanup_orphan_tags.sql", config)
        self.expect(count(), 4, "migration rerun and dry run preserve valid tags")
        invalid_suffix = "" if mapping == "custom" else ", 999, 'synthetic'"
        self.execute(f"INSERT INTO {tags} ({child_ordering}, tag{suffix_columns}) VALUES (999, 'invalid'{invalid_suffix});", fail="violates foreign key|Msg 547")
        self.expect(count(), 4, "FK rejects invalid child")
        self.execute(f"BEGIN TRANSACTION; DELETE FROM {journal} WHERE {ordering}=1; ROLLBACK;")
        self.expect(count(), 4, "rollback restores cascaded tags")
        self.execute(f"DELETE FROM {journal} WHERE {ordering}=1;")
        self.expect(count(), 2, "parent delete cascades")
        self.expect(self.scalar(f"SELECT COUNT(*) FROM {tags} WHERE {child_ordering}=2;"), 2, "boundary parent and tags retained")
        if mapping == "custom":
            self.expect(self.scalar(f"SELECT COUNT(*) FROM {tags} WHERE custom_timestamp=123;"), 2, "custom column preserved")
        # A validated FK whose triggers/checks are disabled must not be accepted as enforced.
        if pg:
            self.execute(f"ALTER TABLE {tags} DISABLE TRIGGER ALL;")
            self.script("02_add_foreign_key.sql", config, fail="disabled.*enforcement triggers")
            self.execute(f"ALTER TABLE {tags} ENABLE TRIGGER ALL;")
        else:
            self.execute(f"ALTER TABLE {tags} NOCHECK CONSTRAINT {q(FK_NAME)};")
            self.script("02_add_foreign_key.sql", config)
            self.execute(f"INSERT INTO {tags} ({child_ordering}, tag{suffix_columns}) VALUES (999, 'invalid'{invalid_suffix});", fail="Msg 547")
        self.checks += 1
        self.script("03_drop_foreign_key.sql", config)
        self.script("03_drop_foreign_key.sql", config)
        self.expect(count(), 2, "rollback migration preserves surviving data")
        # An identically named constraint with restrictive deletes is a conflict.
        self.execute(f"ALTER TABLE {tags} ADD CONSTRAINT {q(FK_NAME)} FOREIGN KEY ({child_ordering}) REFERENCES {journal} ({ordering});")
        self.script("02_add_foreign_key.sql", config, fail="different.*definition")
        self.script("03_drop_foreign_key.sql", config, fail="different.*definition")
        self.expect(count(), 2, "conflicting constraint is not replaced or dropped")
        self.execute(f"DROP TABLE {tags}; DROP TABLE {journal};")
        if mapping == "custom":
            self.execute(f"DROP SCHEMA {q(config['schema_name'])};")
        print(f"PASS {self.provider}: {mapping} mapping")

    def run(self):
        self.execute(f"CREATE DATABASE {self.quote(self.name)};", master=True)
        try:
            for mapping in ["default", "compat", "custom"]:
                self.run_case(mapping)
        finally:
            if self.provider == "SqlServer":
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
