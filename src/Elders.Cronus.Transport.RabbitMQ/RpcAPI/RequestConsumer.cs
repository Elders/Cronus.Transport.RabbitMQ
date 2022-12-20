using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public class RequestConsumer<TRequest, TResponse> : AsyncConsumerBase
        where TRequest : IRpcRequest<TResponse>
        where TResponse : IRpcResponse, new()
    {
        private string queue;
        private readonly IRequestResponseFactory factory;
        private readonly IServiceProvider serviceProvider;
        private static string _timeout = "30000";

        public RequestConsumer(string queue, IModel model, IRequestResponseFactory factory, ISerializer serializer, IServiceProvider serviceProvider, ILogger logger)
          : base(model, serializer, logger)
        {
            this.queue = queue;
            this.factory = factory;
            this.serviceProvider = serviceProvider;
            model.QueueDeclare(queue, exclusive: false);
            model.BasicQos(0, 1, false);
            model.BasicConsume(queue, autoAck: false, this); // We should do manual acknowledgement to spread the load equally over multiple servers
            logger.Info(() => $"RPC request consumer started for {queue}.");
        }

        protected override async Task DeliverMessageToSubscribersAsync(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
        {
            using (IServiceScope scope = serviceProvider.CreateScope())
            {
                TRequest request = default;
                RpcResponseTransmission response = default;

                try // Proccess request and publish response
                {
                    request = (TRequest)serializer.DeserializeFromBytes(ev.Body);

                    IRequestHandler<TRequest, TResponse> handler = factory.CreateHandler<TRequest, TResponse>(request.Tenant, scope.ServiceProvider);
                    TResponse handlerResponse = await handler.HandleAsync(request).ConfigureAwait(false);
                    response = new RpcResponseTransmission(handlerResponse);
                }
                catch (Exception ex) when (logger.ErrorException(ex, () => $"Request listened on {queue} failed."))
                {
                    response = RpcResponseTransmission.WithError(ex);
                }
                finally
                {
                    IBasicProperties replyProps = model.CreateBasicProperties();
                    replyProps.CorrelationId = ev.BasicProperties.CorrelationId; // correlate requests with the responses
                    replyProps.ReplyTo = ev.BasicProperties.ReplyTo;
                    replyProps.Persistent = false;
                    replyProps.Expiration = _timeout;

                    byte[] responseBytes = serializer.SerializeToBytes(response);
                    model.BasicPublish("", routingKey: replyProps.ReplyTo, replyProps, responseBytes);
                    model.BasicAck(ev.DeliveryTag, false);
                }
            }
        }
    }
}
