using System;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public class ResponseConsumer<TRequest, TResponse> : AsyncConsumerBase
        where TRequest : IRpcRequest<TResponse>
        where TResponse : IRpcResponse, new()
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<TResponse>> requestTracker = new ConcurrentDictionary<string, TaskCompletionSource<TResponse>>();
        private static HashSet<string> occupiedNames = new HashSet<string>();
        private static string _timeout = "30000";
        private readonly string queueName;
        private readonly string queueToConsume;

        public ResponseConsumer(string queueName, IModel model, ISerializer serializer, ILogger logger)
          : base(model, serializer, logger)
        {
            this.queueName = queueName;
            queueToConsume = DeclareUniqueQueue();
            model.BasicConsume(queue: queueToConsume, autoAck: true, consumer: this);

            logger.Info(() => $"RPC response consumer started for {queueToConsume}.");
        }

        public Task<TResponse> SendAsync(TRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            string correlationId = Guid.NewGuid().ToString(); // Create a new request id
            IBasicProperties props = model.CreateBasicProperties();
            props.CorrelationId = correlationId;
            props.ReplyTo = queueToConsume;
            props.Expiration = _timeout;
            props.Persistent = false;

            byte[] messageBytes = serializer.SerializeToBytes(request);

            TaskCompletionSource<TResponse> taskSource = new TaskCompletionSource<TResponse>();
            requestTracker.TryAdd(correlationId, taskSource);

            model.BasicPublish(exchange: "", routingKey: queueName, basicProperties: props, body: messageBytes); // publish request
            logger.LogDebug($"Publish request with id {correlationId}, to {queueName}");

            cancellationToken.Register(() => requestTracker.TryRemove(correlationId, out _));
            return taskSource.Task;
        }

        protected override Task DeliverMessageToSubscribersAsync(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer) // await responses and add to collection
        {
            RpcResponseTransmission transient = new RpcResponseTransmission();
            TaskCompletionSource<TResponse> task = default;
            TResponse response = new TResponse();

            try
            {
                if (requestTracker.TryRemove(ev.BasicProperties.CorrelationId, out task) == false) // confirm that request is not proccessed
                {
                    return Task.CompletedTask;
                }

                transient = (RpcResponseTransmission)serializer.DeserializeFromBytes(ev.Body);

                response.Data = transient.Data;
                response.Error = transient.Error;

                task.TrySetResult(response);

                return Task.CompletedTask;
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => $"Unable to process response!"))
            {
                response.Data = transient.Data;
                response.Error = transient.Error;
                task?.TrySetResult(response);
            }

            return Task.CompletedTask;
        }

        private string DeclareUniqueQueue()
        {
            string queue = $"{queueName}.client.{Guid.NewGuid()}";
            return model.QueueDeclare(queue).QueueName;
        }
    }
}
