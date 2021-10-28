# [6.4.0-preview.5](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.4...v6.4.0-preview.5) (2021-10-28)


### Bug Fixes

* Properly stops consumer ([0f96707](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/0f9670765d446838ecc2a3a19b6645e8a8cac130))

# [6.4.0-preview.4](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.3...v6.4.0-preview.4) (2021-10-21)


### Bug Fixes

* Reduces the amount of messages sent on re-publish ([1f25344](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/1f25344545997beb778ef987a664f2bf1f0e0c2f))

# [6.4.0-preview.3](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.2...v6.4.0-preview.3) (2021-06-28)


### Bug Fixes

* Updates Cronus ([c658999](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/c658999c03fc4a03354b57314ba73e8f587a9a98))
* Wait a bit when starting a consumer. It needs warming up ([e79ca67](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/e79ca67701d974d575b7bd99e1475072efeae36b))

# [6.4.0-preview.2](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.1...v6.4.0-preview.2) (2021-05-11)


### Bug Fixes

* Fixes how the bounded context is resolved on publish ([83f54c1](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/83f54c1b50f9f76324ca0db3ae2c77676c6c68a9))
* Removes message priority ([0db50a4](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/0db50a4b46612696d8f419647d9a0f33f74f5a11))
* Updates Cronus ([27c8863](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/27c88631d833072c762317ba499198a24fe098c4))

# [6.4.0-preview.1](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.3.2-preview.1...v6.4.0-preview.1) (2021-05-07)


### Features

* Consolidates the release notes ([552792a](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/552792a6201b21ae9ae0831e38bcae36517dd094))

## [6.3.2-preview.1](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.3.1...v6.3.2-preview.1) (2021-05-07)

#### 6.4.0-beta0007 - 05.05.2021
* Fixes how exchange names are defined

#### 6.4.0-beta0006 - 27.04.2021
* Adds logging when a connection is established or destroyed

#### 6.4.0-beta0005 - 27.04.2021
* Adds logging when a connection is established or destroyed

#### 6.4.0-beta0004 - 27.04.2021
* Fixes connection replaceability issue

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
* Reworks the connection management per bounded context when publishing messages

#### 6.1.1 - 15.09.2020
* Fixes a consumer connection management bug

#### 6.1.0 - 24.08.2020
* Added support for Fan-out Mode. In this Mode the message will be delivered to all consumers.
* Logs the message which has failed to be processed
* Allows to configure federated exchange max hops (#243)
* Adds support for consuming signal messages
* Adds infrastructure support for public events and communication channel between bounded contexts

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
* Pre-fetch only one message at a time
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
