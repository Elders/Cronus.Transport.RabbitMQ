namespace Elders.Cronus.Transport.RabbitMQ.Playground.Rpc.Requests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(x => x.AddEnvironmentVariables())
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                    services.AddCronus(hostContext.Configuration);
                })
                .Build();

            host.Run();
        }
    }
}
