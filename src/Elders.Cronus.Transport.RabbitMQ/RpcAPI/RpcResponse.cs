using System;
using System.Runtime.Serialization;
namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public interface IRpcResponse
    {
        internal object Data { get; set; }

        public string Error { get; set; }

        public bool IsSuccessful { get { return Data is not null && string.IsNullOrEmpty(Error); } }
    }

    public class RpcResponse<TResult> : IRpcResponse
    {
        public RpcResponse() { }

        private RpcResponse(string error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        private RpcResponse(TResult result)
        {
            IRpcResponse temp = this;
            Result = result;
            temp.Data = result;
        }

        [DataMember(Order = 1)]
        public TResult Result { get; set; }

        [DataMember(Order = 2)]
        public string Error { get; set; }

        public bool IsSuccessful { get { return Result is not null && string.IsNullOrEmpty(Error); } }

        object IRpcResponse.Data
        {
            get { return Result; }
            set { Result = (TResult)value; }
        }

        public static RpcResponse<TResult> WithError(string error)
        {
            return new RpcResponse<TResult>(error);
        }

        public static RpcResponse<TResult> SetResult(TResult result)
        {
            return new RpcResponse<TResult>(result);
        }
    }
}
