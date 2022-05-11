using System;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public interface IRpcClient<TRequest, TResponse> where TRequest : IRpcRequest<TResponse>
    {
        public Task<TResponse> SendAsync(TRequest request);
    }

    public class ResponseConsumer<TRequest, TResponse> : AsyncConsumerBase, IRpcClient<TRequest, TResponse> where TRequest : IRpcRequest<TResponse>
    {
        private readonly BlockingCollection<TResponse> responses = new BlockingCollection<TResponse>();
        private readonly IBasicProperties props;
        private readonly string queue;
        private readonly string requestId;

        public ResponseConsumer(string queue, IModel model, ISerializer serializer, ILogger logger)
          : base(model, serializer, logger)
        {
            this.queue = queue;

            model.QueueDeclare(queue);
            props = model.CreateBasicProperties();
            requestId = Guid.NewGuid().ToString(); // Create a new request id
            props.CorrelationId = requestId;
            props.ReplyTo = queue;
            model.BasicConsume(queue: queue, autoAck: true, consumer: this);

            logger.Info(() => $"RPC Api response consumer started for {queue}.");
        }

        protected override Task DeliverMessageToSubscribersAsync(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer) // await responses and add to collection
        {
            TResponse response = default;
            try
            {
                response = (TResponse)serializer.DeserializeFromBytes(ev.Body);

                if (response is not null && ev.BasicProperties.CorrelationId == requestId)
                {
                    responses.Add(response);
                }
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => "Failed to get response."))
            {
                responses.Add(response);
            }

            return Task.CompletedTask;
        }

        public async Task<TResponse> SendAsync(TRequest request)
        {
            byte[] messageBytes = serializer.SerializeToBytes(request); // convert to bytes

            model.BasicPublish(exchange: "", routingKey: queue, basicProperties: props, body: messageBytes); // publish request

            TResponse response = await Task.Run(() => responses.Take()); // Proccess in the task to not to block the thread
            return response;
        }
    }
}
