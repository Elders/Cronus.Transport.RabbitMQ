using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public interface IRpc
    {
        internal void StartServer();

        internal Task StopConsumersAsync();
    }

    public interface IRpc<TRequest, TResponse> : IRpc
        where TRequest : IRpcRequest<TResponse>
        where TResponse : IRpcResponse, new()
    {
        public Task<TResponse> SendAsync(TRequest request);
    }

    public class RpcEndpoint<TRequest, TResponse> : IRpc<TRequest, TResponse>
        where TRequest : IRpcRequest<TResponse>
        where TResponse : IRpcResponse, new()
    {
        private ResponseConsumer<TRequest, TResponse> client;
        private RequestConsumer<TRequest, TResponse> server;
        private string route;
        private readonly ConsumerPerQueueChannelResolver channelResolver;
        private readonly BoundedContext boundedContext;
        private readonly RabbitMqConsumerOptions consumerOptions;
        private readonly RabbitMqOptions options;
        private readonly IRequestResponseFactory factory;
        private readonly ISerializer serializer;
        private readonly ILogger<RpcEndpoint<TRequest, TResponse>> logger;

        public RpcEndpoint(IOptionsMonitor<RabbitMqOptions> options, IOptionsMonitor<BoundedContext> boundedContext, ConsumerPerQueueChannelResolver channelResolver, IRequestResponseFactory factory, ISerializer serializer, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptionsMonitor, ILogger<RpcEndpoint<TRequest, TResponse>> logger)
        {
            this.channelResolver = channelResolver;
            this.consumerOptions = consumerOptionsMonitor.CurrentValue;
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

            TResponse response = new TResponse();

            try
            {
                response = await client.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(consumerOptions.RpcTimeout)).ConfigureAwait(false);
            }
            catch (TimeoutException timedOutEx)
            {
                string error = "The server not responding for too long...";
                logger.ErrorException(timedOutEx, () => error);
                response.Error = error;
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => ex.Message))
            {
                response.Error = ex.Message;
            }

            return response;
        }

        void IRpc.StartServer()
        {
            try
            {
                IRabbitMqOptions scopedOptions = options.GetOptionsFor(boundedContext.Name);
                IModel requestChannel = channelResolver.Resolve(route, scopedOptions, options.VHost);

                server = new RequestConsumer<TRequest, TResponse>(route, requestChannel, factory, serializer, logger);
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => $"Unable to start rpc server for {route}.")) { }
        }

        async Task IRpc.StopConsumersAsync()
        {
            await (client?.StopAsync()).ConfigureAwait(false);
            await (server?.StopAsync()).ConfigureAwait(false);
        }

        private ResponseConsumer<TRequest, TResponse> StartClient()
        {
            try
            {
                var attributes = typeof(TRequest).GetCustomAttributes(typeof(DataContractAttribute), false);
                var dataContractAttribute = attributes[0] as DataContractAttribute;
                string destinationBC = dataContractAttribute.Namespace;

                if (destinationBC is not null)
                {
                    IRabbitMqOptions scopedOptions = options.GetOptionsFor(destinationBC);
                    IModel requestChannel = channelResolver.Resolve(route, scopedOptions, destinationBC);
                    client = new ResponseConsumer<TRequest, TResponse>(route, requestChannel, serializer, logger);
                    return client;
                }
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => $"Unable to start rpc client for {route}.")) { }

            return null;
        }

        private string GetRoute() => $"{typeof(TRequest).Name}s";
    }
}
