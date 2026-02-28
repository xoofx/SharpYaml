using System;

namespace SharpYaml.Serialization.Logging
{
    /// <summary>
    /// Logger interface.
    /// </summary>
    internal interface ILogger
    {
        void Log(LogLevel level, Exception ex, string message);
    }
}
