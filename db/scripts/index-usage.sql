/* ---------------------------------------------------------------------------
   Index usage review
   Purpose : Show how each index on the business tables has been used since
             the last service restart, to spot unused or write-heavy
             indexes. Run against a database that has seen real (or
             seeded) traffic; a fresh database shows zeros.
   Usage   : sqlcmd -S (localdb)\MSSQLLocalDB -d Coms -i db/scripts/index-usage.sql -W
   --------------------------------------------------------------------------- */

SELECT s.name + '.' + t.name                     AS TableName,
       i.name                                     AS IndexName,
       i.type_desc                                AS IndexType,
       CASE WHEN i.is_unique = 1 THEN 'Y' ELSE '' END AS IsUnique,
       CASE WHEN i.has_filter = 1 THEN i.filter_definition ELSE '' END AS Filter,
       ISNULL(u.user_seeks, 0)                    AS Seeks,
       ISNULL(u.user_scans, 0)                    AS Scans,
       ISNULL(u.user_lookups, 0)                  AS Lookups,
       ISNULL(u.user_updates, 0)                  AS Updates,
       u.last_user_seek                           AS LastSeek,
       u.last_user_scan                           AS LastScan,
       ps.row_count                               AS Rows,
       CAST(ps.used_page_count * 8 / 1024.0 AS DECIMAL(10,2)) AS SizeMB
  FROM sys.indexes i
  JOIN sys.tables t  ON t.object_id = i.object_id
  JOIN sys.schemas s ON s.schema_id = t.schema_id
  LEFT JOIN sys.dm_db_index_usage_stats u
         ON u.object_id = i.object_id AND u.index_id = i.index_id AND u.database_id = DB_ID()
  LEFT JOIN
       (SELECT object_id, index_id, SUM(row_count) AS row_count, SUM(used_page_count) AS used_page_count
          FROM sys.dm_db_partition_stats
         GROUP BY object_id, index_id) ps
         ON ps.object_id = i.object_id AND ps.index_id = i.index_id
 WHERE s.name = 'dbo'
   AND i.name IS NOT NULL
   AND t.name <> 'SchemaVersions'
 ORDER BY TableName, i.index_id;

/* Indexes the optimiser wishes it had. Treat as hints, not orders:
   check whether an existing index could be widened instead. */
SELECT OBJECT_SCHEMA_NAME(d.object_id) + '.' + OBJECT_NAME(d.object_id) AS TableName,
       d.equality_columns   AS EqualityColumns,
       d.inequality_columns AS InequalityColumns,
       d.included_columns   AS IncludedColumns,
       s.user_seeks         AS Seeks,
       CAST(s.avg_total_user_cost * s.avg_user_impact * s.user_seeks AS DECIMAL(18,2)) AS ImpactScore
  FROM sys.dm_db_missing_index_details d
  JOIN sys.dm_db_missing_index_groups g       ON g.index_handle = d.index_handle
  JOIN sys.dm_db_missing_index_group_stats s  ON s.group_handle = g.index_group_handle
 WHERE d.database_id = DB_ID()
 ORDER BY ImpactScore DESC;
