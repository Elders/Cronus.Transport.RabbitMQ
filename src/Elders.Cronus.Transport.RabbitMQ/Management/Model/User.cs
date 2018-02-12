namespace Elders.Cronus.Transport.RabbitMQ.Management.Model
{
    public class User
    {
        public string Name { get; set; }
        public string PasswordHash { get; set; }
        public string Tags { get; set; }
    }
}
