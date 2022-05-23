using System;
using System.Runtime.Serialization;
namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public interface IRpcResponse
    {
        public string Error { get; set; }

        public bool IsSuccessful { get { return string.IsNullOrEmpty(Error); } }
    }

    [DataContract(Name = "3d60f526-0285-41f9-95ec-301da89fd397")]
    public class RpcResponse<TResult> : IRpcResponse
    {
        public RpcResponse() { }

        public RpcResponse(string error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public RpcResponse(TResult result)
        {
            if (result is null)
                Error = "Unable to get result.";
            else
                Result = result;
        }

        [DataMember(Order = 1)]
        public TResult Result { get; set; }

        [DataMember(Order = 2)]
        public string Error { get; set; }

        [DataMember(Order = 3)]
        public bool IsSuccessful { get { return Result is not null && string.IsNullOrEmpty(Error); } }
    }
}
