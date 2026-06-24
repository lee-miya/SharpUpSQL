/*
  Sample database, tables, and PII-like data for enumeration and column-audit tests.
*/
SET NOCOUNT ON;
PRINT '--- 02-sample-database ---';

IF DB_ID('LabDB') IS NULL
    CREATE DATABASE LabDB;
GO

USE LabDB;
GO

-- DbOwner escalation: lowpriv_reader is db_owner (intentionally weak)
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'lowpriv_reader')
    CREATE USER [lowpriv_reader] FOR LOGIN [lowpriv_reader];
IF IS_ROLEMEMBER('db_owner', 'lowpriv_reader') = 0
    ALTER ROLE db_owner ADD MEMBER [lowpriv_reader];

-- Sample customers table with PII patterns
IF OBJECT_ID('dbo.Customers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers (
        CustomerId   INT IDENTITY(1,1) PRIMARY KEY,
        FullName     NVARCHAR(100) NOT NULL,
        Email        NVARCHAR(200) NULL,
        Phone        NVARCHAR(20) NULL,
        CreditCard   NVARCHAR(20) NULL,
        SSN          NVARCHAR(11) NULL,
        CreatedAt    DATETIME2 DEFAULT SYSUTCDATETIME()
    );

    INSERT INTO dbo.Customers (FullName, Email, Phone, CreditCard, SSN)
    VALUES
        (N'Alice Test', N'alice@test.lab', N'555-0100', N'4111111111111111', N'123-45-6789'),
        (N'Bob Test',   N'bob@test.lab',   N'555-0101', N'5500000000000004', N'987-65-4321');
END

-- Views and procs for enumeration tests
IF OBJECT_ID('dbo.vw_CustomerSummary', 'V') IS NULL
    EXEC(N'CREATE VIEW dbo.vw_CustomerSummary AS SELECT CustomerId, FullName, Email FROM dbo.Customers');

IF OBJECT_ID('dbo.usp_GetCustomer', 'P') IS NULL
    EXEC(N'
        CREATE PROCEDURE dbo.usp_GetCustomer @Id INT AS
        BEGIN
            SET NOCOUNT ON;
            SELECT * FROM dbo.Customers WHERE CustomerId = @Id;
        END');

-- Dynamic SQL proc (SQLi audit target)
IF OBJECT_ID('dbo.usp_DynamicSearch', 'P') IS NULL
    EXEC(N'
        CREATE PROCEDURE dbo.usp_DynamicSearch @Term NVARCHAR(100) AS
        BEGIN
            DECLARE @sql NVARCHAR(MAX) = N''SELECT * FROM dbo.Customers WHERE FullName LIKE ''''%'' + @Term + ''%'''' '';
            EXEC sp_executesql @sql;
        END');

-- Temp table marker for Get-SQLTableTemp discovery
IF OBJECT_ID('tempdb..#LabTempProbe') IS NOT NULL DROP TABLE #LabTempProbe;
SELECT 1 AS Probe INTO #LabTempProbe;

PRINT 'LabDB sample objects created.';
