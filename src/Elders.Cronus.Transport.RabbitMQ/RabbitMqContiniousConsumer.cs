using System;
using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Transport.RabbitMQ.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqContinuousConsumer<T> : ContinuousConsumer<T>
    {
        private readonly ISerializer serializer;

        private Dictionary<Guid, ulong> deliveryTags;

        private QueueingBasicConsumerWithManagedConnection consumer;

        public RabbitMqContinuousConsumer(BoundedContext boundedContext, ISerializer serializer, IConnectionFactory connectionFactory, SubscriberCollection<T> subscriberCollection)
            : base(subscriberCollection)
        {
            this.deliveryTags = new Dictionary<Guid, ulong>();
            this.serializer = serializer;
            this.consumer = new QueueingBasicConsumerWithManagedConnection(connectionFactory, subscriberCollection, boundedContext);
        }

        protected override CronusMessage GetMessage()
        {
            if (ReferenceEquals(null, consumer))
                return null;

            return consumer.Do((consumer) =>
            {
                BasicDeliverEventArgs dequeuedMessage = null;
                consumer.Queue.Dequeue((int)30, out dequeuedMessage);
                if (ReferenceEquals(null, dequeuedMessage) == false)
                {
                    var cronusMessage = (CronusMessage)serializer.DeserializeFromBytes(dequeuedMessage.Body);
                    deliveryTags[cronusMessage.Id] = dequeuedMessage.DeliveryTag;
                    return cronusMessage;
                }

                return null;
            });
        }

        protected override void MessageConsumed(CronusMessage message)
        {
            if (ReferenceEquals(null, consumer)) return;
            try
            {
                consumer.Do((consumer) =>
                {
                    ulong deliveryTag;
                    if (deliveryTags.TryGetValue(message.Id, out deliveryTag))
                        consumer.Model.BasicAck(deliveryTag, false);
                    return true;
                });
            }
            finally
            {
                deliveryTags.Remove(message.Id);
            }
        }

        protected override void WorkStart() { }

        protected override void WorkStop()
        {
            consumer?.Abort();
            consumer = null;
            deliveryTags?.Clear();
            deliveryTags = null;
        }

        class QueueingBasicConsumerWithManagedConnection
        {
            static readonly ILog log = LogProvider.GetLogger(typeof(QueueingBasicConsumerWithManagedConnection));

            private DateTime timestampSinceConsumerIsNotWorking;
            private IModel model;
            private static IConnection connection;
            private readonly IConnectionFactory connectionFactory;
            private readonly SubscriberCollection<T> subscriberCollection;
            private readonly BoundedContext boundedContext;
            private QueueingBasicConsumer consumer;
            private bool aborting;
            private readonly string queueName;

            public QueueingBasicConsumerWithManagedConnection(IConnectionFactory connectionFactory, SubscriberCollection<T> subscriberCollection, BoundedContext boundedContext)
            {
                this.connectionFactory = connectionFactory;
                this.subscriberCollection = subscriberCollection;
                this.boundedContext = boundedContext;
                queueName = $"{boundedContext}.{typeof(T).Name}";
            }

            public TResult Do<TResult>(Func<QueueingBasicConsumer, TResult> consumerAction)
            {
                try
                {
                    EnsureHealthyConsumerForSubscriber();
                    TResult result = consumerAction(consumer);
                    return result;
                }
                catch (Exception ex)
                {
                    log.WarnException(ex.Message, ex);
                    return default(TResult);
                }
            }

            public void Abort()
            {
                lock (connectionFactory)
                {
                    aborting = true;

                    consumer = null;

                    model?.Abort();
                    model = null;

                    connection?.Abort();
                    connection = null;
                }
            }

            /// <summary>
            /// Ensures that we have a running consumer
            /// </summary>
            void EnsureHealthyConsumerForSubscriber()
            {
                RecoverConnection();
                RecoverModel();
                RecoverConsumer();
            }

            /// <summary>
            /// By rabbitmq design, Each IConnection instance is, in the current implementation, backed by a single background thread that reads from the socket and dispatches the resulting events to the application.
            /// So the connection MUST be one per process so we need to ensure that we have only one. This is the reason for the lock
            /// </summary>
            void RecoverConnection()
            {
                if (aborting)
                    return;

                if (ReferenceEquals(null, connection) || connection.IsOpen == false)
                {
                    lock (connectionFactory)
                    {
                        if (aborting)
                            return;

                        if (ReferenceEquals(null, connection) || connection.IsOpen == false)
                        {
                            connection?.Abort();
                            connection = connectionFactory.CreateConnection();
                        }
                    }
                }
            }

            void RecoverModel()
            {
                if (aborting)
                    return;

                if (model == null || model.IsClosed)
                {
                    model?.Abort();
                    model = connection.CreateModel();

                    var routingHeaders = new Dictionary<string, object>();
                    routingHeaders.Add("x-match", "any");
                    var messageTypes = subscriberCollection.Subscribers.SelectMany(x => x.GetInvolvedMessageTypes()).Distinct().ToList();

                    foreach (var msgType in messageTypes.Select(x => x.GetContractId()))
                    {
                        routingHeaders.Add(msgType, null);
                    }

                    model.QueueDeclare(queueName, true, false, false, routingHeaders);

                    var exchanges = messageTypes.GroupBy(x => RabbitMqNamer.GetExchangeName(boundedContext.Name, x)).Distinct();
                    foreach (var item in exchanges)
                    {
                        model.ExchangeDeclare(item.Key, PipelineType.Headers.ToString(), true);
                        var args = new Dictionary<string, object>();
                        args.Add("x-delayed-type", PipelineType.Headers.ToString());
                        model.ExchangeDeclare(item.Key + ".Scheduler", "x-delayed-message", true, false, args);

                        var bindHeaders = new Dictionary<string, object>();
                        bindHeaders.Add("x-match", "any");

                        foreach (var msgType in item.Select(x => x.GetContractId()))
                        {
                            bindHeaders.Add(msgType, null);
                        }
                        model.QueueBind(queueName, item.Key, string.Empty, bindHeaders);
                        model.QueueBind(queueName, item.Key + ".Scheduler", string.Empty, bindHeaders);
                        model.BasicQos(0, 1, false);
                    }
                }
            }

            void RecoverConsumer()
            {
                if (aborting)
                    return;

                if (consumer == null || consumer.IsRunning == false)
                {
                    if ((DateTime.UtcNow - timestampSinceConsumerIsNotWorking).TotalSeconds > 5)
                    {
                        timestampSinceConsumerIsNotWorking = DateTime.UtcNow;
                        consumer = new QueueingBasicConsumer(model);
                        string consumerTag = model.BasicConsume(queueName, false, consumer);
                    }
                }
            }
        }
    }
}
