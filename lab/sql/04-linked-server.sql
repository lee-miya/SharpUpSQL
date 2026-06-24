/*
  Linked server from PRIMARY (1433) to LINKED target (1434).
  Adjust @linked_data_source for Docker hostnames (sql-linked,1433) if needed.
*/
SET NOCOUNT ON;
PRINT '--- 04-linked-server ---';

DECLARE @linked_sysname SYSNAME = N'LINKED_SRV';
DECLARE @linked_data_source NVARCHAR(4000) = N'localhost,1434';

IF EXISTS (SELECT 1 FROM sys.servers WHERE name = @linked_sysname)
BEGIN
    EXEC master.dbo.sp_dropserver @server = @linked_sysname, @droplogins = 'droplogins';
END

EXEC master.dbo.sp_addlinkedserver
    @server     = @linked_sysname,
    @srvproduct = N'',
    @provider   = N'MSOLEDBSQL',
    @datasrc    = @linked_data_source;

EXEC master.dbo.sp_addlinkedsrvlogin
    @rmtsrvname  = @linked_sysname,
    @useself     = N'False',
    @locallogin  = NULL,
    @rmtuser     = N'sa',
    @rmtpassword = N'LabAdmin123!';

EXEC master.dbo.sp_serveroption @linked_sysname, N'rpc', N'true';
EXEC master.dbo.sp_serveroption @linked_sysname, N'rpc out', N'true';
EXEC master.dbo.sp_serveroption @linked_sysname, N'data access', N'true';

-- Target DB on linked instance
EXEC(N'
    IF DB_ID(''LinkedTargetDB'') IS NULL
        CREATE DATABASE LinkedTargetDB;
') AT LINKED_SRV;

PRINT 'Linked server LINKED_SRV -> ' + @linked_data_source + ' configured.';
