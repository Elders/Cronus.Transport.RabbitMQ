using Elders.Cronus.Transport.RabbitMQ.RpcAPI;
using System.Runtime.Serialization;

namespace Elders.Cronus.Transport.RabbitMQ.Playground.Rpc.Requests
{
    [DataContract(Namespace = "rmqresponses", Name = "4413d9ce-37c2-4638-b2ac-a1d60b119694")]
    public class NumberRequest : RpcRequest<NumberResponse>
    {
        [DataMember(Order = 1)]
        public int Number { get; set; }

        public NumberRequest()
        {

        }

        public NumberRequest(int number)
        {
            Number = number;
            Tenant = "elders";
        }
    }

    [DataContract(Namespace = "rmqresponses", Name = "edf9cefe-f42d-4b5d-ba0f-6360484c377e")]
    public class NumberResponse
    {
        [DataMember(Order = 1)]
        public int DoubleNumber { get; set; }
    }
}
