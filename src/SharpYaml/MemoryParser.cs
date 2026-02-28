using System.Collections;
using System.Collections.Generic;
using SharpYaml.Events;

namespace SharpYaml
{
    /// <summary>Represents the Memory Parser.</summary>
    public class MemoryParser : IParser
    {
        private readonly IEnumerator<ParsingEvent> parsingEvents;

        /// <summary>Initializes a new instance of this type.</summary>
        public MemoryParser(IEnumerable<ParsingEvent> parsingEvents)
        {
            this.parsingEvents = parsingEvents.GetEnumerator();
        }

        /// <summary>Gets current.</summary>
        public ParsingEvent Current { get { return parsingEvents.Current; } }

        /// <summary>Advances the enumerator to the next element.</summary>
        public bool MoveNext()
        {
            return parsingEvents.MoveNext();
        }
    }
}
