using System;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public sealed class PipelineType
    {
        readonly string name;

        public static readonly PipelineType Direct = new PipelineType("direct");
        public static readonly PipelineType Fanout = new PipelineType("fanout");
        public static readonly PipelineType Headers = new PipelineType("headers");
        public static readonly PipelineType Topics = new PipelineType("topic");

        PipelineType(string name)
        {
            this.name = name;
        }

        public static PipelineType Parse(string type)
        {
            var result = new PipelineType(type);
            if (result.name == Direct.name || result.name == Fanout.name || result.name == Headers.name || result.name == Topics.name)
                return result;
            else
                throw new ArgumentException($"Invalid pipeline type '{type}'.");
        }

        public override String ToString()
        {
            return name;
        }
    }
}
