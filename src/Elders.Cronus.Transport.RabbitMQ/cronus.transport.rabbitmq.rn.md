#### 6.4.0-beta0003 - 09.04.2021
* Removes duplicate bindings for System Queues

#### 6.4.0-beta0002 - 31.03.2021
* Adds support for system queues and exchanges

#### 6.4.0-beta0001 - 29.03.2021
* Updates cronus
* Adds bindings for Aggregate commits

#### 6.3.1 - 09.02.2021
* Enables publisher confirms to all models including publishers

#### 6.3.0 - 09.02.2021
* Updates Cronus to v6.3.0
* Enables publisher confirms to all models

#### 6.2.2 - 10.12.2020
* Handles the recovery of VHost when it is deleted runtime
* Updates Cronus to 6.2.9

#### 6.2.1 - 11.11.2020
* Fixes the Public EventPublisher

#### 6.2.0 - 30.09.2020
* Reworks the connection management per bounded context when publishing mesages

#### 6.1.1 - 15.09.2020
* Fixes a consumer connection management bug

#### 6.1.0 - 24.08.2020
* Added support for Fanout Mode. In this Mode the message will be delivered to all consumers.
* Logs the message which has failed to be processed
* Allows to configure federated exchange max hops (#243)
* Adds support for consuming signal messages
* Adds infrastructure support for public events and comminication channel between bounded contexts

#### 6.0.1 - 23.04.2020
* Check if the WorkPool is initialized before stopping it
* Restart the consumer only if the options have changed

#### 6.0.0 - 16.04.2020
* Introduced Cronus:Transport:RabbitMq:Consumer:WorkersCount
* Replaces LibLog with CronusLogger
* Overrides IConsumer service
* Fixes options registration
* Rework the RabbitMqOptions to use options pattern
* The transport layer is now fully aware of the bounded context
* Fixes hangs related to connection issues
* Fixes a connection management issue
* Fixes a connection termination when publisher fails

#### 5.2.2
* Added support for connecting to cluster/multiple endpoints

#### 5.2.1
* Fixes a (re)connection issue

#### 5.2.0
* Uses ISubscriberCollection interface instead of SubscriberCollection
* Adds a warn log when a client tries to publish a message and the publisher is stopped/disposed

#### 5.1.0 - 10.12.2018
* Updates to DNC 2.2

#### 5.0.0 - 29.11.2018
* Improves logging when there are no subscribers for a consumer
* Removes boundedContext consumers and change how exchanges and queues are named
* Adds RabbitMqTransportDiscovery
* Prefetch only one message at a time
* Properly closing RabbitMQ connections

#### 4.0.6 - 28.02.2017
* Updates Cronus

#### 4.0.5 - 26.02.2017
* Updates Cronus

#### 4.0.4 - 23.02.2017
* Fixes how we construct exschange and queue names

#### 4.0.3 - 22.02.2017
* Fixes how we consume messages

#### 4.0.2 - 20.02.2017
* Downgrades Newtonsoft.Json to 10.0.3

#### 4.0.1 - 20.02.2017
* Targets dotnetstandard20 and .NET 4.5.1

#### 4.0.0 - 12.02.2017
* dotnetstandard20 support

#### 3.1.1 - 07.12.2017
* Fixed issue where we are not reconnecting to RabbitMq when the connection is dropped

#### 3.1.0 - 31.10.2017
* There are breaking changes in the transport.

#### 3.0.2 - 01.09.2017
* To be honest there is a problem in the public interface and the way we use endpoints has a memory leak unless you Open/Close the endpoint.

#### 3.0.1 - 01.12.2016
* Add setting for specifying admin port
* Adds a Virtual Host, if such not present in RabbitMQ, and assigning permissions for the user passed to the initial settings

#### 3.0.1-beta0002 - 01.12.2016
* Add setting for specifying admin port

#### 3.0.1-beta0001 - 01.12.2016
* Adds a Virtual Host, if such not present in RabbitMQ, and assigning permissions for the user passed to the initial settings

#### 3.0.0 - 08.09.2016
* Adds support for IScheduleMessages sent to pipelines
* Moves WithDefaultPublishersWithRabbitMq to Cronus
* Replaces RabbitMqEndpointPerBoundedContext with RabbitMqEndpointPerConsumer

#### 2.2.0 - 19.03.2016
* Schedule a message for future publishing is not possible using the 'rabbitmq_delayed_message_exchange' from http://www.rabbitmq.com/community-plugins.html
The plugin did not work well with the current RabbitMQ v3.5.3 so the RabbitMQ client is updated to the current latest v3.6.1

#### 2.1.3 - 06.07.2015
* Update packages

#### 2.1.2 - 02.07.2015
* Update packages

#### 2.1.1 - 02.07.2015
* Update packages

#### 2.1.0 - 02.07.2015
* Update packages

#### 2.0.1 - 25.05.2015
* Properly expose the configuration extensions

#### 2.0.0 - 16.05.2015
* Build for Cronus 2.*

#### 2.0.0-alpha04 - 15.05.2015
* Version for Cronus 2.0.alpha9

#### 1.2.6 - 21.04.2015
* Fix nuget package dependencies

#### 1.2.5 - 27.03.2015
* Fix issues with building exchanges and queues.

#### 1.2.4 - 25.03.2015
* Downgrade RabbitMq to v3.4.3

#### 1.2.3 - 23.03.2015
* Update to latest Cronus

#### 1.2.2 - 17.02.2015
* Improve error reporting.
* Update nuget packages

#### 1.2.1 - 13.01.2015
* Update Cronus package

#### 1.2.0 - 16.12.2014
* Add pipeline and endpoint strategies
* Fix bug when closing endpoint
* Fix bug with wrong endpoint for projections
* Fix bug with assigning wrong pipeline for app service endpoint
* Properly close rabbitMq channel

#### 1.0.3 - 02.10.2014
* Build for Cronus 1.1.40

#### 1.0.2 - 01.10.2014
* Build for Cronus 1.1.39 without ES publisher

#### 1.0.1 - 11.09.2014
* Fix bug with nuget package release

#### 1.0.0 - 21.06.2014
* Moved from Cronus repository
