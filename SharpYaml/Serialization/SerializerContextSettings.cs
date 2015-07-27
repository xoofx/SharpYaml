using SharpYaml.Serialization.Logging;

namespace SharpYaml.Serialization
{
    /// <summary>
    /// Some parameters that can be transmitted from caller
    /// </summary>
    public class SerializerContextSettings
    {
        public static readonly SerializerContextSettings Default = new SerializerContextSettings();

        /// <summary>
        /// Gets the logger.
        /// </summary>
        /// <value>
        /// The logger.
        /// </value>
        public ILogger Logger { get; set; }
    }
}