using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;


namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public interface IRpc
    {
        public Tuple<AsyncConsumerBase, AsyncConsumerBase> StartConsumers();
    }

    public interface IRpc<TRequest, TResponse> : IRpc
        where TRequest : IRpcRequest<TResponse>
    {
        public string QueueName { get; set; }
        public ResponseConsumer<TRequest, TResponse> Client { get; set; }
        public RequestConsumer<TRequest, TResponse> Server { get; set; }

        Task<TResponse> SendAsync(TRequest request);
    }

    public class RpcEndpoint<TRequest, TResponse> : IRpc<TRequest, TResponse>
        where TRequest : IRpcRequest<TResponse>
    {
        public string QueueName { get; set; } = "requests";
        public ResponseConsumer<TRequest, TResponse> Client { get; set; }
        public RequestConsumer<TRequest, TResponse> Server { get; set; }

        private readonly ConsumerPerQueueChannelResolver channelResolver;
        private readonly BoundedContext boundedContext;
        private readonly RabbitMqOptions options;
        private readonly IRequestResponseFactory factory;
        private readonly ISerializer serializer;
        private readonly ILogger<RpcEndpoint<TRequest, TResponse>> logger;

        public RpcEndpoint() { }

        public RpcEndpoint(IOptionsMonitor<RabbitMqOptions> options, IOptionsMonitor<BoundedContext> boundedContext, ConsumerPerQueueChannelResolver channelResolver, IRequestResponseFactory factory, ISerializer serializer, ILogger<RpcEndpoint<TRequest, TResponse>> logger)
        {
            this.channelResolver = channelResolver;
            this.boundedContext = boundedContext.CurrentValue;
            this.options = options.CurrentValue;
            this.factory = factory;
            this.serializer = serializer;
            this.logger = logger;
        }

        public async Task<TResponse> SendAsync(TRequest request)
        {

            TResponse response = await Client.SendAsync(request).ConfigureAwait(false);
            return response;
        }

        public Tuple<AsyncConsumerBase, AsyncConsumerBase> StartConsumers()
        {
            IRabbitMqOptions scopedOptions = options.GetOptionsFor(boundedContext.Name);
            IModel requestChannel = channelResolver.Resolve("requests", scopedOptions, options.VHost);
            IRequestHandler<TRequest, TResponse> handler = factory.CreateHandler<TRequest, TResponse>();

            Server = new RequestConsumer<TRequest, TResponse>(QueueName, requestChannel, handler, serializer, logger);
            Client = new ResponseConsumer<TRequest, TResponse>(QueueName, requestChannel, serializer, logger);

            return new Tuple<AsyncConsumerBase, AsyncConsumerBase>(Client, Server);
        }
    }
}
