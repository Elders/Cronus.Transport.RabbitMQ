using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Pipeline;
using Elders.Cronus.Serializer;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqContiniousConsumerFactory : IConsumerFactory
    {
        private readonly ISubscriber subscriber;
        private readonly ISerializer serializer;
        private readonly IConnectionFactory connectionFactory;
        private readonly SubscriptionMiddleware middleware;

        public RabbitMqContiniousConsumerFactory(ISubscriber subscriber, ISerializer serializer, IConnectionFactory connectionFactory, SubscriptionMiddleware middleware)
        {
            this.subscriber = subscriber;
            this.serializer = serializer;
            this.connectionFactory = connectionFactory;
            this.middleware = middleware;
        }

        public ContinuousConsumer CreateConsumer()
        {
            return new RabbitMqContiniousConsumer(subscriber, serializer, connectionFactory, middleware);
        }
    }
}
