using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Elders.Cronus.Transport.RabbitMQ.Internal
{
    internal interface IQueueingBasicConsumer : IBasicConsumer
    {
        void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body);
        void OnCancel();
        SharedQueue<BasicDeliverEventArgs> Queue { get; }
    }
}
