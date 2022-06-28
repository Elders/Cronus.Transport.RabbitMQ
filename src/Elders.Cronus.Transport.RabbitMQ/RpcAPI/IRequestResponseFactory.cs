using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Elders.Cronus.MessageProcessing;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public interface IRequestHandler<TRequest, TResponse>
        where TRequest : IRpcRequest<TResponse>
    {
        Task<TResponse> HandleAsync(TRequest request);
    }

    public interface IRequestResponseFactory
    {
        void RegisterHandlers(ILookup<Type, Type> handlers);
        ILookup<Type, Type> GetHandlers();
        IRequestHandler<TRequest, TResponse> CreateHandler<TRequest, TResponse>(string tenant) where TRequest : IRpcRequest<TResponse>;
    }

    public class RequestResponseFactory : IRequestResponseFactory
    {
        private ILookup<Type, Type> _requestHandlerTypes;
        private readonly IServiceProvider provider;

        public RequestResponseFactory(IServiceProvider provider)
        {
            _requestHandlerTypes = new List<Type>().ToLookup(x => x);
            this.provider = provider;
        }

        public void RegisterHandlers(ILookup<Type, Type> handlers)
        {
            _requestHandlerTypes = handlers;
        }

        public ILookup<Type, Type> GetHandlers()
        {
            return _requestHandlerTypes;
        }

        public IRequestHandler<TRequest, TResponse> CreateHandler<TRequest, TResponse>(string tenant) where TRequest : IRpcRequest<TResponse>
        {
            IGrouping<Type, Type> handlers = _requestHandlerTypes.FirstOrDefault(x => x.Contains(typeof(TRequest)));

            if (handlers is null)
                throw new ApplicationException("No handler registered for type: " + typeof(TRequest).FullName);

            // All Our Gods Have Abandoned Us and we are no more able to inject generics like this and to cast implementation to it's generic interface. See https://user-images.githubusercontent.com/40130484/175905317-818e1a0a-ff28-4e74-9470-a327a9d72e15.png
            // But with Reflection we don't need the gods anymore. We can even provide request/response pattern without a dynamic  cast.
            ConstructorInfo constructor = handlers.Key.GetConstructors().FirstOrDefault(c => c.GetParameters().Length != 0);
            ParameterInfo[] injections = constructor?.GetParameters();
            object[] implementations = new object[injections.Length];

            using (var scope = provider.CreateScope())
            {
                for (int i = 0; i < injections.Length; i++)
                {
                    CronusContextFactory contextFactory = scope.ServiceProvider.GetRequiredService<CronusContextFactory>();
                    contextFactory.GetContext(tenant, scope.ServiceProvider);

                    implementations[i] = scope.ServiceProvider.GetRequiredService(injections[i].ParameterType);
                }
            }

            IRequestHandler<TRequest, TResponse> handler = (IRequestHandler<TRequest, TResponse>)Activator.CreateInstance(handlers.Key, implementations);

            return handler;
        }
    }

    public interface IRpcRequest { }

    public interface IRpcRequest<TResponse> : IRpcRequest
    {
        internal string Tenant { get; set; }
    }
}
