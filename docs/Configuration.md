# Cronus.Transport.RabbitMq

#### `Cronus:Transport:RabbitMQ:ConsumerWorkersCount` >> *integer | Required: Yes | Default: 5*
Configures the number of threads which will be dedicated for consuming messages from RabbitMQ for *every* consumer.

---

#### `Cronus:Transport:RabbitMQ:Server` >> *string | Required: Yes | Default: 127.0.0.1*
DNS or IP of the RabbitMQ server

---

#### `Cronus:Transport:RabbitMQ:Port` >> *integer | Required: Yes | Default: 5672*
The port number on which the RabbitMQ server is running

---

#### `Cronus:Transport:RabbitMQ:VHost` >> *string | Required: Yes | Default: /*
The name of the virtual host. It is a good practice to not use the default `/` vhost. For more details see the [official docs](https://www.rabbitmq.com/vhosts.html). Cronus is not using this for managing multitenancy.

---

#### `Cronus:Transport:RabbitMQ:Username` >> *string | Required: Yes | Default: guest*
The RabbitMQ username

---

#### `Cronus:Transport:RabbitMQ:Password` >> *string | Required: Yes | Default: guest*
The RabbitMQ password

---

#### `Cronus:Transport:RabbitMQ:AdminPort` >> *integer | Required: Yes | Default: 5672*
RabbitMQ admin port used to create, delete rabbitmq resources

---

#### `Cronus:Transport:RabbitMQ:Consumer:WorkersCount` >> *integer | Required: No | Default: 5 | Range: [1-2147483647]*
RabbitMQ number of consumer workers

---

#### `Cronus:Transport:PublicRabbitMQ:Server` >> *string | Required: No | Default: 127.0.0.1*
DNS or IP of the Public RabbitMQ server

---

#### `Cronus:Transport:PublicRabbitMQ:VHost` >> *string | Required: Yes | Default: /*
The name of the virtual host. It is a good practice to not use the default `/` vhost. For more details see the [official docs](https://www.rabbitmq.com/vhosts.html). Cronus is not using this for managing multitenancy.

---

#### `Cronus:Transport:PublicRabbitMQ:FederatedExchange:MaxHops` >> *integer | Required: No | Default: 1 | Range: [1-2147483647]*
Specifies the max hops of the Federated Exchange