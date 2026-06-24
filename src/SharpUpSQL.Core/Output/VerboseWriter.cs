using System;

namespace SharpUpSQL.Core.Output
{
    public sealed class VerboseWriter
    {
        public bool Enabled { get; set; }

        public void Write(string message)
        {
            if (Enabled)
            {
                Console.Error.WriteLine(message);
            }
        }

        public void WriteWarning(string message)
        {
            Console.Error.WriteLine("WARNING: " + message);
        }
    }
}
