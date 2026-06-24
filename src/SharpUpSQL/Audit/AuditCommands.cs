using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpUpSQL.Commands;
using SharpUpSQL.Core.Helpers;

namespace SharpUpSQL.Audit
{
    public abstract class AuditCommandBase : SharpUpSqlCommandBase
    {
        protected abstract IEnumerable<SqlAuditResult> Run(AuditContext context);

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            foreach (var target in InstanceTargetResolver.Resolve(context))
            {
                var auditContext = AuditContext.From(context, target);
                foreach (var result in Run(auditContext))
                {
                    if (!auditContext.NoOutput)
                    {
                        yield return result;
                    }
                }
            }
        }
    }

    public sealed class InvokeSqlAuditDefaultLoginPwCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditDefaultLoginPw"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditDefaultLoginPw(context);
        }
    }

    public sealed class InvokeSqlAuditWeakLoginPwCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditWeakLoginPw"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditWeakLoginPw(context);
        }
    }

    public sealed class InvokeSqlAuditPrivImpersonateLoginCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditPrivImpersonateLogin"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditPrivImpersonateLogin(context);
        }
    }

    public sealed class InvokeSqlAuditPrivServerLinkCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditPrivServerLink"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditPrivServerLink(context);
        }
    }

    public sealed class InvokeSqlAuditPrivTrustworthyCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditPrivTrustworthy"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditPrivTrustworthy(context);
        }
    }

    public sealed class InvokeSqlAuditPrivDbChainingCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditPrivDbChaining"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditPrivDbChaining(context);
        }
    }

    public sealed class InvokeSqlAuditPrivCreateProcedureCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditPrivCreateProcedure"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditPrivCreateProcedure(context);
        }
    }

    public sealed class InvokeSqlAuditPrivXpDirtreeCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditPrivXpDirtree"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditPrivXpDirtree(context);
        }
    }

    public sealed class InvokeSqlAuditPrivXpFileexistCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditPrivXpFileexist"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditPrivXpFileexist(context);
        }
    }

    public sealed class InvokeSqlAuditRoleDbDdlAdminCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditRoleDbDdlAdmin"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditRoleDbDdlAdmin(context);
        }
    }

    public sealed class InvokeSqlAuditRoleDbOwnerCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditRoleDbOwner"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditRoleDbOwner(context);
        }
    }

    public sealed class InvokeSqlAuditSampleDataByColumnCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditSampleDataByColumn"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditSampleDataByColumn(context);
        }
    }

    public sealed class InvokeSqlAuditSqliSpExecuteAsCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditSQLiSpExecuteAs"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditSqliSpExecuteAs(context);
        }
    }

    public sealed class InvokeSqlAuditSqliSpSignedCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditSQLiSpSigned"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditSqliSpSigned(context);
        }
    }

    public sealed class InvokeSqlAuditPrivAutoExecSpCommand : AuditCommandBase
    {
        public override string Name { get { return "Invoke-SQLAuditPrivAutoExecSp"; } }
        protected override IEnumerable<SqlAuditResult> Run(AuditContext context)
        {
            return AuditEngine.AuditPrivAutoExecSp(context);
        }
    }

    public sealed class InvokeSqlAuditCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Invoke-SQLAudit"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            foreach (var target in InstanceTargetResolver.Resolve(context))
            {
                var auditContext = AuditContext.From(context, target);
                if (!AuditEngine.TestConnection(auditContext))
                {
                    continue;
                }

                var results = AuditEngine.RunAll(auditContext).ToList();
                if (!string.IsNullOrEmpty(auditContext.OutFolder))
                {
                    Directory.CreateDirectory(auditContext.OutFolder);
                    var safeInstance = InstanceHelper.SanitizeFileName(auditContext.Instance);
                    var path = Path.Combine(
                        auditContext.OutFolder,
                        "PowerUpSQL_Audit_Results_" + safeInstance + ".csv");
                    AuditEngine.WriteAuditCsv(path, results);
                    WriteVerbose(context, "Audit results written to " + Path.GetFullPath(path));
                }

                if (!auditContext.NoOutput)
                {
                    foreach (var result in results)
                    {
                        yield return result;
                    }
                }
            }
        }
    }

    public sealed class InvokeSqlEscalatePrivCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Invoke-SQLEscalatePriv"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            foreach (var target in InstanceTargetResolver.Resolve(context))
            {
                var auditContext = AuditContext.From(context, target);
                if (!AuditEngine.TestConnection(auditContext))
                {
                    continue;
                }

                WriteVerbose(context, auditContext.Instance + " : Checking if you're already a sysadmin...");
                if (AuditEngine.IsCurrentLoginSysadmin(auditContext))
                {
                    WriteVerbose(context, auditContext.Instance + " : You are, so nothing to do here. :)");
                    continue;
                }

                WriteVerbose(context, auditContext.Instance + " : You're not a sysadmin, attempting to change that...");
                auditContext.Exploit = true;
                auditContext.NoOutput = true;
                AuditEngine.RunAll(auditContext).ToList();

                if (AuditEngine.IsCurrentLoginSysadmin(auditContext))
                {
                    WriteVerbose(context, auditContext.Instance + " : Success! You are now a sysadmin!");
                }
                else
                {
                    WriteVerbose(context, auditContext.Instance + " : Fail. We couldn't get you sysadmin access today.");
                }
            }

            yield break;
        }
    }
}
