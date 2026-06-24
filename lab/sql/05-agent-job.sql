/*
  Sample SQL Agent job for Get-SQLAgentJob / Invoke-SQLOSCmdAgentJob tests.
  Skips gracefully if SQL Server Agent is not running.
*/
SET NOCOUNT ON;
PRINT '--- 05-agent-job ---';

IF EXISTS (SELECT 1 FROM sys.dm_server_services WHERE servicename LIKE N'SQL Server Agent%' AND status_desc = N'Stopped')
BEGIN
    PRINT 'SQL Server Agent is stopped — job creation skipped. Start Agent to enable job tests.';
END
ELSE IF NOT EXISTS (SELECT 1 FROM sys.dm_server_services WHERE servicename LIKE N'SQL Server Agent%')
BEGIN
    PRINT 'SQL Server Agent not installed (Express) — job creation skipped.';
END
ELSE
BEGIN
    DECLARE @jobId BINARY(16);
    EXEC msdb.dbo.sp_add_job
        @job_name = N'SharpUpSQL_Lab_Probe',
        @enabled = 1,
        @description = N'Lab probe job for SharpUpSQL Agent tests',
        @job_id = @jobId OUTPUT;

    EXEC msdb.dbo.sp_add_jobstep
        @job_id = @jobId,
        @step_name = N'ProbeStep',
        @subsystem = N'TSQL',
        @command = N'SELECT @@VERSION AS LabProbe;',
        @database_name = N'master';

    EXEC msdb.dbo.sp_add_jobserver @job_id = @jobId;
    PRINT 'Agent job SharpUpSQL_Lab_Probe created.';
END
