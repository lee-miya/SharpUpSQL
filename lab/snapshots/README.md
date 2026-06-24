# Golden snapshot directory for PowerUpSQL baseline outputs.
#
# Snapshots are generated after lab setup:
#   .\lab\scripts\Export-GoldenSnapshots.ps1
#
# Directory layout mirrors test phases:
#   discovery/  - instance discovery commands
#   core/       - connection test, query execution
#   common/     - enumeration (info, database, login, link)
#   link/       - linked-server crawl
#   audit/      - Invoke-SQLAudit* outputs
#   enum/       - stored procedures, sysadmin check
#   ad/         - domain recon (Profile D, optional)
#
# Each .txt file header records capture timestamp and source command.
# manifest.json lists all snapshot paths and PowerUpSQL version used.
