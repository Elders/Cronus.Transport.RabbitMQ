using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Pipeline;
using Elders.Cronus.Serializer;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqContiniousConsumerFactory : IConsumerFactory
    {
        private readonly string consumerName;
        private readonly ISerializer serializer;
        private readonly IConnectionFactory connectionFactory;
        private readonly SubscriptionMiddleware middleware;

        public RabbitMqContiniousConsumerFactory(string consumerName, ISerializer serializer, IConnectionFactory connectionFactory, SubscriptionMiddleware middleware)
        {
            this.consumerName = consumerName;
            this.serializer = serializer;
            this.connectionFactory = connectionFactory;
            this.middleware = middleware;
        }

        public ContinuousConsumer CreateConsumer()
        {
            return new RabbitMqContinuousConsumer(consumerName, serializer, connectionFactory, middleware);
        }
    }
}
