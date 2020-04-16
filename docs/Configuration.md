# Cronus.Transport.RabbitMq

#### `Cronus:Transport:RabbiMQ:ConsumerWorkersCount` >> *integer | Required: Yes | Default: 5*
Configures the number of threads which will be dedicated for consuming messages from RabbitMQ for *every* consumer.

---

#### `Cronus:Transport:RabbiMQ:Server` >> *string | Required: Yes | Default: 127.0.0.1*
DNS or IP to the RabbitMQ server

---

#### `Cronus:Transport:RabbiMQ:Port` >> *integer | Required: Yes | Default: 5672*
The port number on which the RabbitMQ server is running

---

#### `Cronus:Transport:RabbiMQ:VHost` >> *string | Required: Yes | Default: /*
The name of the virtual host. It is a good practice to not use the default `/` vhost. For more details see the [official docs](https://www.rabbitmq.com/vhosts.html). Cronus is not using this for managing multitenancy.

---

#### `Cronus:Transport:RabbiMQ:Username` >> *string | Required: Yes | Default: guest*
The RabbitMQ username

---

#### `Cronus:Transport:RabbiMQ:Password` >> *string | Required: Yes | Default: guest*
The RabbitMQ password

---

#### `Cronus:Transport:RabbiMQ:AdminPort` >> *integer | Required: Yes | Default: 5672*
RabbitMQ admin port used to create, delete rabbitmq resources

---

#### `Cronus:Transport:RabbiMQ:Consumer:WorkersCount` >> *integer | Required: No | Default: 5 | Range: [1-2147483647]*
RabbitMQ number of consumer workers
