using Elders.Cronus.Pipeline.Transport.RabbitMQ.Config;

namespace Elders.Cronus.Pipeline.Hosts
{
    public class RabbitMqCronusHost : CronusHost
    {
        public RabbitMqCronusHost(CronusConfiguration configuration)
            : base(configuration)
        {

        }

        protected override void OnHostStop()
        {
            if (RabbitMqTransportSettings.Session != null)
            {
                RabbitMqTransportSettings.Session.Close();// PLS fix this, This should not be static call so the .Session prop should not be static
                RabbitMqTransportSettings.Session = null;
            }
        }
    }
}