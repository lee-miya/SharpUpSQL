/*
  Intentionally weak configurations for Invoke-SQLAudit* and escalation tests.
*/
SET NOCOUNT ON;
PRINT '--- 03-audit-fixtures ---';

USE master;

-- Ad Hoc Distributed Queries (needed for linked OPENQUERY / AD recon)
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;
EXEC sp_configure 'Ad Hoc Distributed Queries', 1;
RECONFIGURE;

-- xp_cmdshell enabled for OS command tests (lab only)
EXEC sp_configure 'xp_cmdshell', 1;
RECONFIGURE;

-- Ole Automation (Invoke-SQLOSCmdCOle)
EXEC sp_configure 'Ole Automation Procedures', 1;
RECONFIGURE;

-- CLR (Invoke-SQLOSCmdCLR) — enable if supported
BEGIN TRY
    EXEC sp_configure 'clr enabled', 1;
    RECONFIGURE;
END TRY
BEGIN CATCH
    PRINT 'CLR enable skipped: ' + ERROR_MESSAGE();
END CATCH

USE LabDB;

-- TRUSTWORTHY + EXECUTE AS for audit PrivTrustworthy
ALTER DATABASE LabDB SET TRUSTWORTHY ON;

IF OBJECT_ID('dbo.usp_ExecAsOwner', 'P') IS NULL
    EXEC(N'
        CREATE PROCEDURE dbo.usp_ExecAsOwner
        WITH EXECUTE AS OWNER
        AS
        BEGIN
            SELECT SYSTEM_USER AS RunAs, IS_SRVROLEMEMBER(''sysadmin'') AS IsSysadmin;
        END');

-- Cross-db chaining fixture
IF DB_ID('LabDB_Chain') IS NULL
BEGIN
    CREATE DATABASE LabDB_Chain;
END
GO

USE LabDB_Chain;
ALTER DATABASE LabDB_Chain SET DB_CHAINING ON;
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'lowpriv_reader')
    CREATE USER [lowpriv_reader] FOR LOGIN [lowpriv_reader];
GRANT SELECT ON SCHEMA::dbo TO [lowpriv_reader];
GO

USE LabDB;
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'lowpriv_reader')
    CREATE USER [lowpriv_reader] FOR LOGIN [lowpriv_reader];

-- xp_dirtree / xp_fileexist grants for lowpriv (UNC / audit tests)
USE master;
GRANT EXECUTE ON xp_dirtree TO [lowpriv_reader];
GRANT EXECUTE ON xp_fileexist TO [lowpriv_reader];

-- DDL trigger sample (persistence enumeration)
USE LabDB;
IF OBJECT_ID('dbo.trg_DdlAudit', 'TR') IS NULL
    EXEC(N'
        CREATE TRIGGER dbo.trg_DdlAudit ON DATABASE
        FOR DDL_DATABASE_LEVEL_EVENTS
        AS
        BEGIN
            SET NOCOUNT ON;
            -- Lab marker trigger; no exploit payload
        END');

-- DML trigger sample
IF OBJECT_ID('dbo.trg_CustomersAudit', 'TR') IS NULL
    EXEC(N'
        CREATE TRIGGER dbo.trg_CustomersAudit ON dbo.Customers
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
        END');

PRINT 'Audit fixtures applied.';
