using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public class RequestConsumer<TRequest, TResponse> : AsyncConsumerBase
        where TRequest : IRpcRequest<TResponse>
        where TResponse : IRpcResponse, new()
    {
        private IRequestHandler<TRequest, TResponse> handler;
        private string queue;
        private static string expiration = "30000";

        public RequestConsumer(string queue, IModel model, IRequestHandler<TRequest, TResponse> handler, ISerializer serializer, ILogger logger)
          : base(model, serializer, logger)
        {
            this.queue = queue;
            this.handler = handler;
            model.QueueDeclare(queue, exclusive: false);
            model.BasicQos(0, 1, false);
            model.BasicConsume(queue, autoAck: false, this); // We should do manual acknowledgement to spread the load equally over multiple servers
            logger.Info(() => $"RPC request consumer started for {queue}.");
        }

        protected override async Task DeliverMessageToSubscribersAsync(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
        {
            TRequest request = default;
            TResponse response = default;

            try // Proccess request and publish response
            {
                request = serializer.DeserializeFromBytes<TRequest>(ev.Body);
                response = await handler.HandleAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => $"Request listened on {queue} failed."))
            {
                response = new TResponse();
                response.Error = ex.Message;
            }
            finally
            {
                IBasicProperties replyProps = model.CreateBasicProperties();
                replyProps.CorrelationId = ev.BasicProperties.CorrelationId; // correlate requests with the responses
                replyProps.ReplyTo = ev.BasicProperties.ReplyTo;
                replyProps.Persistent = false;
                replyProps.Expiration = expiration;

                byte[] responseBytes = serializer.SerializeToBytes(response);
                model.BasicPublish("", routingKey: replyProps.ReplyTo, replyProps, responseBytes);
                model.BasicAck(ev.DeliveryTag, false);
            }
        }
    }
}
