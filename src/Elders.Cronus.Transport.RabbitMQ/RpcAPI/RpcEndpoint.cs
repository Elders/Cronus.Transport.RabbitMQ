using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public interface IRpc
    {
        public void StartServer();

        public Task StopConsumersAsync();
    }

    public interface IRpc<TRequest, TResponse> : IRpc
        where TRequest : IRpcRequest<TResponse>
    {
        public Task<TResponse> SendAsync(TRequest request);
    }

    public class RpcEndpoint<TRequest, TResponse> : IRpc<TRequest, TResponse>
        where TRequest : IRpcRequest<TResponse>
    {
        private ResponseConsumer<TRequest, TResponse> client;
        private RequestConsumer<TRequest, TResponse> server;
        private string route;
        private readonly ConsumerPerQueueChannelResolver channelResolver;
        private readonly BoundedContext boundedContext;
        private readonly RabbitMqOptions options;
        private readonly IRequestResponseFactory factory;
        private readonly ISerializer serializer;
        private readonly ILogger<RpcEndpoint<TRequest, TResponse>> logger;

        public RpcEndpoint(IOptionsMonitor<RabbitMqOptions> options, IOptionsMonitor<BoundedContext> boundedContext, ConsumerPerQueueChannelResolver channelResolver, IRequestResponseFactory factory, ISerializer serializer, ILogger<RpcEndpoint<TRequest, TResponse>> logger)
        {
            this.channelResolver = channelResolver;
            this.boundedContext = boundedContext.CurrentValue;
            this.options = options.CurrentValue;
            this.factory = factory;
            this.serializer = serializer;
            this.logger = logger;

            route = GetRoute();
        }

        public async Task<TResponse> SendAsync(TRequest request)
        {
            client = client ?? StartClient();

            TResponse response = await client.SendAsync(request).ConfigureAwait(false);
            return response;
        }

        public void StartServer()
        {
            IRabbitMqOptions scopedOptions = options.GetOptionsFor(boundedContext.Name);
            IModel requestChannel = channelResolver.Resolve(route, scopedOptions, options.VHost);
            IRequestHandler<TRequest, TResponse> handler = factory.CreateHandler<TRequest, TResponse>();

            server = new RequestConsumer<TRequest, TResponse>(route, requestChannel, handler, serializer, logger);
        }

        public async Task StopConsumersAsync()
        {
            await client.StopAsync().ConfigureAwait(false);
            await server.StopAsync().ConfigureAwait(false);
        }

        private ResponseConsumer<TRequest, TResponse> StartClient()
        {
            IRabbitMqOptions scopedOptions = options.GetOptionsFor(boundedContext.Name);
            IModel requestChannel = channelResolver.Resolve(route, scopedOptions, options.VHost);
            client = new ResponseConsumer<TRequest, TResponse>(route, requestChannel, serializer, logger);
            return client;
        }

        private string GetRoute() => $"{boundedContext.Name}.{typeof(TRequest).Name}s";
    }
}
