using System;
using System.Runtime.Serialization;
namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    /// <summary>
    /// The main purpose of this class is to transfer the response via the netwok.
    /// </summary>
    [DataContract(Name = "618aa313-be8b-40c8-accf-6d2cdfd97d81")]
    internal class RpcResponseTransmission : IRpcResponse
    {
        public RpcResponseTransmission() { }

        private RpcResponseTransmission(string error)
        {
            Error = error;
        }

        public RpcResponseTransmission(IRpcResponse something)
        {
            Data = something.Data;
            Error = something.Error;
        }

        [DataMember(Order = 1)]
        public object Data { get; set; }

        [DataMember(Order = 2)]
        public string Error { get; set; }

        public static RpcResponseTransmission WithError(string error)
        {
            return new RpcResponseTransmission(error: error);
        }

        public static RpcResponseTransmission WithError(Exception error)
        {
            return new RpcResponseTransmission(error: error.Message); // TODO: We could add the entire stack trace
        }
    }
}
