using SharpYaml.Events;

namespace SharpYaml.Serialization
{
    internal sealed class DepthLimitingEventEmitter : ChainedEventEmitter
    {
        private readonly int maxDepth;
        private int currentDepth;

        public DepthLimitingEventEmitter(IEventEmitter nextEmitter, int maxDepth)
            : base(nextEmitter)
        {
            this.maxDepth = YamlDepthHelper.GetEffectiveMaxDepth(maxDepth, nameof(maxDepth));
        }

        public override void Emit(MappingStartEventInfo eventInfo)
        {
            EnterContainer();
            base.Emit(eventInfo);
        }

        public override void Emit(MappingEndEventInfo eventInfo)
        {
            base.Emit(eventInfo);
            currentDepth--;
        }

        public override void Emit(SequenceStartEventInfo eventInfo)
        {
            EnterContainer();
            base.Emit(eventInfo);
        }

        public override void Emit(SequenceEndEventInfo eventInfo)
        {
            base.Emit(eventInfo);
            currentDepth--;
        }

        public override void Emit(ParsingEvent parsingEvent)
        {
            if (parsingEvent is MappingStart || parsingEvent is SequenceStart)
            {
                EnterContainer();
            }

            base.Emit(parsingEvent);

            if (parsingEvent is MappingEnd || parsingEvent is SequenceEnd)
            {
                currentDepth--;
            }
        }

        private void EnterContainer()
        {
            if (currentDepth >= maxDepth)
            {
                throw YamlDepthHelper.CreateMaxDepthExceededException(maxDepth);
            }

            currentDepth++;
        }
    }
}
