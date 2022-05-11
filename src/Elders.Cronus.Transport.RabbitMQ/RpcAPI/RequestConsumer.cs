using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public interface IRpcServer { }

    public class RequestConsumer<TRequest, TResponse> : AsyncConsumerBase, IRpcServer where TRequest : IRpcRequest<TResponse>
    {
        private IRequestHandler<TRequest, TResponse> handler;

        public RequestConsumer(string queue, IModel model, IRequestHandler<TRequest, TResponse> handler, ISerializer serializer, ILogger logger)
          : base(model, serializer, logger)
        {
            this.handler = handler;
            model.QueueDeclare(queue);
            model.BasicQos(0, 1, false);
            model.BasicConsume(queue, autoAck: false, this); // Queue from which requests are consumes
            logger.Info(() => $"RPC Api request consumer started for {queue}.");
        }

        protected override Task DeliverMessageToSubscribersAsync(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
        {
            IBasicProperties replyProps = null;
            IBasicProperties props = null;
            TRequest request = default;
            TResponse response = default;

            try // Proccess request and publish respons^e 
            {
                props = ev.BasicProperties;
                replyProps = model.CreateBasicProperties();
                replyProps.CorrelationId = props.CorrelationId; // correlate requests with the responses
                request = (TRequest)serializer.DeserializeFromBytes(ev.Body);

                logger.LogInformation($"Requested: {request}");

                response = handler.HandleAsync(request);
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => $"Request {request} failed."))
            {
                //response = new ResponseResult($"Unable to proccess request {request.A} * {request.B}");
            }
            finally
            {
                byte[] responseBytes = serializer.SerializeToBytes(response);
                model.BasicPublish("", routingKey: props.ReplyTo, replyProps, responseBytes);
                model.BasicAck(ev.DeliveryTag, false);
            }

            return Task.CompletedTask;
        }
    }
}
