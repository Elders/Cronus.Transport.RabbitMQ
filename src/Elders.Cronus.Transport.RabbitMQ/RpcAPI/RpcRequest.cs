using System.Runtime.Serialization;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public interface IRpcRequest { }

    public interface IRpcRequest<TResponse> : IRpcRequest
    {
        internal string Tenant { get; set; }
    }

    [DataContract(Name = "d6e57847-c558-4fc4-8501-cbe504efcf21")]
    public abstract class RpcRequest<TResponse> : IRpcRequest<RpcResponse<TResponse>>
    {
        [DataMember(Order = 0)]
        public string Tenant { get; set; }

        public RpcResponse<TResponse> Respond(TResponse response) => RpcResponse<TResponse>.SetResult(response);

        public RpcResponse<TResponse> Fail(string error) => RpcResponse<TResponse>.WithError(error);
    }
}
