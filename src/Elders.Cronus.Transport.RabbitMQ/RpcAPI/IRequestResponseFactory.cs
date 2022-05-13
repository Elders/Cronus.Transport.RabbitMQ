using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        IRequestHandler<TRequest, TResponse> CreateHandler<TRequest, TResponse>() where TRequest : IRpcRequest<TResponse>;
    }

    public class RequestResponseFactory : IRequestResponseFactory
    {
        private ILookup<Type, Type> _requestHandlerTypes;
        private readonly IServiceProvider _services;

        public RequestResponseFactory(IServiceProvider services)
        {
            _requestHandlerTypes = new List<Type>().ToLookup(x => x);
            _services = services;
        }

        public void RegisterHandlers(ILookup<Type, Type> handlers)
        {
            _requestHandlerTypes = handlers;
        }

        public ILookup<Type, Type> GetHandlers()
        {
            return _requestHandlerTypes;
        }

        public IRequestHandler<TRequest, TResponse> CreateHandler<TRequest, TResponse>() where TRequest : IRpcRequest<TResponse>
        {
            IGrouping<Type, Type> handlers = _requestHandlerTypes.FirstOrDefault(x => x.Contains(typeof(TRequest)));

            if (handlers.Any() == false)
                throw new ApplicationException("No handler registered for type: " + typeof(TRequest).FullName);

            IRequestHandler<TRequest, TResponse> handler = (IRequestHandler<TRequest, TResponse>)Activator.CreateInstance(handlers.Key);
            return handler;
        }
    }

    public interface IRpcRequest { }

    public interface IRpcRequest<TResponse> : IRpcRequest { }

    public interface IRpcResponse { }
}
