using System;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Helpers;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Core.Execution
{
    public static class ConnectionTester
    {
        public static ConnectionTestResult Test(
            string instance,
            string ipAddress,
            string ipRange,
            SqlConnectionOptions options,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            if (string.IsNullOrWhiteSpace(instance))
            {
                instance = Environment.MachineName;
            }

            var computerName = InstanceParser.GetComputerNameFromInstance(instance);

            if (!string.IsNullOrWhiteSpace(ipRange) && !string.IsNullOrWhiteSpace(ipAddress))
            {
                bool outOfScope;
                if (!SubnetHelper.IsInScope(ipRange, ipAddress, out outOfScope))
                {
                    if (outOfScope)
                    {
                        if (!suppressVerbose)
                        {
                            verbose.WriteWarning("Skipping " + computerName + " (" + ipAddress + ")");
                        }

                        return new ConnectionTestResult
                        {
                            ComputerName = computerName,
                            Instance = instance,
                            Status = "Out of Scope"
                        };
                    }
                }
                else if (verbose.Enabled && !suppressVerbose)
                {
                    verbose.Write(computerName + " (" + ipAddress + ")");
                }
            }

            var connectionOptions = options != null ? options.Clone() : new SqlConnectionOptions();
            connectionOptions.Instance = ServerAddressHelper.FormatServer(
                instance,
                connectionOptions.Port,
                connectionOptions.ForceNamedPipe);

            if (connectionOptions.UsesPassTheHash)
            {
                return TestPassTheHash(connectionOptions, computerName, instance, verbose, suppressVerbose);
            }

            using (var connection = SqlConnectionFactory.CreateConnection(connectionOptions))
            {
                try
                {
                    connection.Open();

                    if (!suppressVerbose && verbose.Enabled)
                    {
                        verbose.Write(instance + " : Connection Success.");
                    }

                    return new ConnectionTestResult
                    {
                        ComputerName = computerName,
                        Instance = instance,
                        Status = "Accessible"
                    };
                }
                catch (Exception ex)
                {
                    if (!suppressVerbose && verbose.Enabled)
                    {
                        verbose.Write(instance + " : Connection Failed.");
                        verbose.Write(" Error: " + ex.Message);
                    }

                    return new ConnectionTestResult
                    {
                        ComputerName = computerName,
                        Instance = instance,
                        Status = "Not Accessible"
                    };
                }
            }
        }

        private static ConnectionTestResult TestPassTheHash(
            SqlConnectionOptions options,
            string computerName,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            try
            {
                using (var client = new PthTdsClient(options))
                {
                    client.TestConnection(verbose);
                }

                if (!suppressVerbose && verbose.Enabled)
                {
                    verbose.Write(instance + " : PTH Connection Success.");
                }

                return new ConnectionTestResult
                {
                    ComputerName = computerName,
                    Instance = instance,
                    Status = "Accessible"
                };
            }
            catch (Exception ex)
            {
                if (!suppressVerbose && verbose.Enabled)
                {
                    verbose.Write(instance + " : PTH Connection Failed.");
                    verbose.Write(" Error: " + ex.Message);
                }

                return new ConnectionTestResult
                {
                    ComputerName = computerName,
                    Instance = instance,
                    Status = "Not Accessible"
                };
            }
        }
    }
}
