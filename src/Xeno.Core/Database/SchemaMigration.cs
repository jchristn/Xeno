namespace Xeno.Core.Database
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A single, versioned, idempotent schema migration. Each migration has a monotonically increasing
    /// integer <see cref="Version"/>, a human-readable <see cref="Description"/>, and an ordered list of
    /// provider-specific SQL <see cref="Statements"/> that are applied together in one transaction and
    /// recorded in the <c>schema_migrations</c> tracking table. Migrations are applied at most once — a
    /// migration whose version is already recorded is skipped — so running them repeatedly is safe.
    ///
    /// <para>The initial schema (version 1) is created by each entity's idempotent
    /// <c>CREATE TABLE IF NOT EXISTS</c> and recorded as the <c>'0001'</c> baseline row. Every subsequent
    /// schema change should be expressed as a new <see cref="SchemaMigration"/> in the owning provider
    /// driver's forward-migration list rather than by editing the baseline DDL.</para>
    /// </summary>
    public class SchemaMigration
    {
        #region Public-Members

        /// <summary>
        /// Monotonic migration version. Must be greater than 1 (version 1 is the baseline schema).
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Human-readable description recorded alongside the version.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Ordered provider-specific SQL statements applied for this migration.
        /// </summary>
        public IReadOnlyList<string> Statements { get; }

        /// <summary>
        /// Zero-padded string identifier used as the primary key in the <c>schema_migrations</c> table
        /// (for example version 2 becomes <c>"0002"</c>), matching the baseline <c>'0001'</c> row.
        /// </summary>
        public string Id
        {
            get
            {
                return Version.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a schema migration.
        /// </summary>
        /// <param name="version">Monotonic version, must be greater than 1.</param>
        /// <param name="description">Human-readable description.</param>
        /// <param name="statements">Ordered provider-specific SQL statements.</param>
        public SchemaMigration(int version, string description, params string[] statements)
        {
            if (version <= 1) throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than 1; version 1 is the baseline schema.");
            if (String.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
            if (statements == null || statements.Length == 0) throw new ArgumentException("At least one statement is required.", nameof(statements));

            Version = version;
            Description = description;
            Statements = new List<string>(statements);
        }

        #endregion
    }
}
