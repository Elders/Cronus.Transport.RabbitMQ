using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public class RequestConsumer<TRequest, TResponse> : AsyncConsumerBase where TRequest : IRpcRequest<TResponse>
    {
        private IRequestHandler<TRequest, TResponse> handler;
        private string queue;

        public RequestConsumer(string queue, IModel model, IRequestHandler<TRequest, TResponse> handler, ISerializer serializer, ILogger logger)
          : base(model, serializer, logger)
        {
            this.queue = queue;
            this.handler = handler;
            model.QueueDeclare(queue);
            model.BasicQos(0, 1, false);
            model.BasicConsume(queue, autoAck: false, this); // Queue from which requests are consumes
            logger.Info(() => $"RPC request consumer started for {queue}.");
        }

        protected override async Task DeliverMessageToSubscribersAsync(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
        {
            IBasicProperties replyProps = null;
            IBasicProperties props = null;
            TRequest request = default;
            TResponse response = default;

            try // Proccess request and publish respons 
            {
                props = ev.BasicProperties;
                replyProps = model.CreateBasicProperties();
                replyProps.CorrelationId = props.CorrelationId; // correlate requests with the responses
                request = (TRequest)serializer.DeserializeFromBytes(ev.Body);

                logger.LogInformation($"Requested: {queue}");

                response = await handler.HandleAsync(request);
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => $"Request {queue}.{request} failed.")) { }
            finally
            {
                byte[] responseBytes = serializer.SerializeToBytes(response);
                model.BasicPublish("", routingKey: props.ReplyTo, replyProps, responseBytes);
                model.BasicAck(ev.DeliveryTag, false);
            }
        }
    }
}
