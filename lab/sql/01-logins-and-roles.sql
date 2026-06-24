/*
  Logins and roles for Profiles A (sysadmin) and C (low privilege).
*/
SET NOCOUNT ON;
PRINT '--- 01-logins-and-roles ---';

-- Mixed-mode auth reminder (must be enabled at install time)
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;

-- Weak password login for audit tests
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'weak_sa_guess')
    CREATE LOGIN [weak_sa_guess] WITH PASSWORD = 'sa', CHECK_POLICY = OFF;
ELSE
    ALTER LOGIN [weak_sa_guess] WITH PASSWORD = 'sa', CHECK_POLICY = OFF;

-- Low-privilege reader (Profile C)
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'lowpriv_reader')
    CREATE LOGIN [lowpriv_reader] WITH PASSWORD = 'WeakPass123!', CHECK_POLICY = OFF;
ELSE
    ALTER LOGIN [lowpriv_reader] WITH PASSWORD = 'WeakPass123!', CHECK_POLICY = OFF;

-- Login with IMPERSONATE on sa (escalation fixture)
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'impersonate_sa')
    CREATE LOGIN [impersonate_sa] WITH PASSWORD = 'Impersonate1!', CHECK_POLICY = OFF;
ELSE
    ALTER LOGIN [impersonate_sa] WITH PASSWORD = 'Impersonate1!', CHECK_POLICY = OFF;

GRANT IMPERSONATE ON LOGIN::[sa] TO [impersonate_sa];

-- Generic test login with known weak password
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'testuser')
    CREATE LOGIN [testuser] WITH PASSWORD = 'Password1!', CHECK_POLICY = OFF;

PRINT 'Logins configured.';
