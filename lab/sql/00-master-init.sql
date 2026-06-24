/*
  SharpUpSQL lab master initializer (sqlcmd :r include mode).
  Prefer Setup-SharpUpSQLLab.ps1 which runs scripts individually.

  sqlcmd -S localhost,1433 -U sa -P "LabAdmin123!" -i 00-master-init.sql
*/
SET NOCOUNT ON;
PRINT '=== SharpUpSQL Lab Init: ' + @@SERVERNAME + ' ===';

:r 01-logins-and-roles.sql
:r 02-sample-database.sql
:r 03-audit-fixtures.sql
:r 05-agent-job.sql

PRINT '=== Base init complete. Run 04-linked-server.sql on PRIMARY only. ===';
