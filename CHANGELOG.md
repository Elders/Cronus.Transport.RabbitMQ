# [7.0.0-preview.19](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.18...v7.0.0-preview.19) (2022-02-08)


### Bug Fixes

* Code cleanup ([19ac208](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/19ac208165f6f5bb8343e9886fa239d847000a4a))

# [7.0.0-preview.18](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.17...v7.0.0-preview.18) (2022-02-07)


### Bug Fixes

* add comment ([23d8e4e](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/23d8e4e63a0ac20792dfa08ac86d7add9c6edeac))
* Add TTL to heartbeat ([1b49cbb](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/1b49cbb4dd8513e3b6a6addd2f2f8988838a918f))
* update Cronus package ([4e3bdf9](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/4e3bdf99adb6453f44d4c8a129a3e4af44f95b2d))

# [7.0.0-preview.17](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.16...v7.0.0-preview.17) (2022-02-06)


### Bug Fixes

* CHANGELOG and Deploy ([fc018ff](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/fc018ff6c13d79d088f4b578ef376c2790004f82))

# [7.0.0-preview.15](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.14...v7.0.0-preview.15) (2022-01-26)


### Bug Fixes

* Remove unneeded flag & Deploy ([173ee70](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/173ee70e263709959adfb58b4911d7534fe748f1))
* Make publisher channels per exchange ([3cffc7b](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/3cffc7b3744235e9c7153a65fa73d673a54c9f2c))
* StackOverFlow exception ([085de67](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/085de678baedd0b7a62002b68ac8275c1b17c3af))
* Fix Resolve connection for bounded context ([7e43074](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/7e430741e6f9e659ddad2669e2dd6249844c79d2))


### Features

* Dispose connections when application is shutting down ([3341951](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/33419516a26b88dbaec3e60b7687f99a1f415827))
* Add RabbitMq startup for all services ([3557674](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/3557674f33eeed5c8e7f63b1b7b0a90be2f49e09))

# [7.0.0-preview.14](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.13...v7.0.0-preview.14) (2022-01-25)


### Bug Fixes

* Trigger CI ([5a20625](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/5a20625caa417419ae2af350455e93d3b247b013))

# [7.0.0-preview.13](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.12...v7.0.0-preview.13) (2022-01-25)


### Bug Fixes

* Rolls back the code which creates the exchanges and the queues ([08d4814](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/08d48143b637b1a39a04ef5bf4e65027297c98c7))

# [7.0.0-preview.12](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.11...v7.0.0-preview.12) (2022-01-24)


### Bug Fixes

* Resolve issue with connection closing in multi-threads ([589eddf](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/589eddfbe305f896a6e6e272d421471c8b7b0282))

# [7.0.0-preview.11](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.10...v7.0.0-preview.11) (2022-01-21)

# [7.0.0-preview.10](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.9...v7.0.0-preview.10) (2022-01-20)


### Bug Fixes

* Ensure exiting from infinity loop on FailedCronusMessage ([b57a778](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/b57a7785fbad4b36638ab753c1688d605a73ec0f))

# [7.0.0-preview.9](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.8...v7.0.0-preview.9) (2022-01-20)


### Bug Fixes

* Handle unknown deserialization problem when trying to serialize into a Cronus Message ([c869d39](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/c869d398b4fb9140a0706721c8de208f75416c5b))

# [7.0.0-preview.8](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.7...v7.0.0-preview.8) (2022-01-18)


### Bug Fixes

* Code Cleanup ([b6172aa](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/b6172aa41d4425f12bd912e194042f353c4810ec))

# [7.0.0-preview.7](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.6...v7.0.0-preview.7) (2022-01-18)


### Bug Fixes

* Code cleanup ([0b83ed5](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/0b83ed5bbdcc44a042107d4ea11590301af85f29))
* HandleBasicDeliver to enqueue in the  right way ([99dd144](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/99dd144f688747751131f45791410006fed0b647))
* Remove unneeded stuff ([9f7d059](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/9f7d059a02214d8e753ef26844bfcaf63c979ace))


### Features

* Update RabbitMQ client ([4b52fbe](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/4b52fbefe6bcb1a2a82b2ad75a01cc3562ac3792))

# [7.0.0-preview.6](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.5...v7.0.0-preview.6) (2022-01-14)


### Features

* Introduces an async consumer ([ea09935](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/ea09935dcd0c447482ff1f166718d0b57463ce0f))

# [7.0.0-preview.5](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.4...v7.0.0-preview.5) (2021-12-06)


### Bug Fixes

* Update Multi-threading-Scheduler ([dd0f80f](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/dd0f80f242d753a43347e1861a7246ceff99b441))

# [7.0.0-preview.4](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.3...v7.0.0-preview.4) (2021-12-06)


### Bug Fixes

* Fixes errors on startup when missing configurations for public rabbitMq ([756bab7](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/756bab7c7210ccd1442381337208c03170d57c79))

# [7.0.0-preview.3](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.2...v7.0.0-preview.3) (2021-12-01)


### Bug Fixes

* Fixes DeserializeResponse settings ([c5211b6](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/c5211b671bdb13e6ac77a964467982c39b642d4f))

# [7.0.0-preview.2](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v7.0.0-preview.1...v7.0.0-preview.2) (2021-12-01)


### Bug Fixes

* Trigger CI ([ff32e95](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/ff32e95133455392ca74b14b9e39d824c9a1ee18))

# [7.0.0-preview.1](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0...v7.0.0-preview.1) (2021-12-01)


### Bug Fixes

* Last Chance Deploy before Mynkow fix it ([aad9182](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/aad9182ce3cb22bcd324bff963ba492886fc283f))
* Updates docs ([2c7f6b5](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/2c7f6b579e8626dbfec727690273cfc9269ad1f5))
* Updates MTS ([f012043](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/f0120430b353ac259d61edc28cfe05b50db3f427))

# [7.0.0-preview.1](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0...v7.0.0-preview.1) (2021-12-01)


### Bug Fixes

* Last Chance Deploy before Mynkow fix it ([aad9182](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/aad9182ce3cb22bcd324bff963ba492886fc283f))
* Updates docs ([2c7f6b5](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/2c7f6b579e8626dbfec727690273cfc9269ad1f5))
* Updates MTS ([f012043](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/f0120430b353ac259d61edc28cfe05b50db3f427))

# [7.0.0-preview.1](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0...v7.0.0-preview.1) (2021-12-01)


### Bug Fixes

* Last Chance Deploy before Mynkow fix it ([aad9182](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/aad9182ce3cb22bcd324bff963ba492886fc283f))
* Updates MTS ([f012043](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/f0120430b353ac259d61edc28cfe05b50db3f427))

# [7.0.0-preview.1](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0...v7.0.0-preview.1) (2021-12-01)


### Bug Fixes

* Last Chance Deploy before Mynkow fix it ([aad9182](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/aad9182ce3cb22bcd324bff963ba492886fc283f))
* Updates MTS ([f012043](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/f0120430b353ac259d61edc28cfe05b50db3f427))

# [7.0.0-preview.1](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0...v7.0.0-preview.1) (2021-12-01)


### Bug Fixes

* Last Chance Deploy before Mynkow fix it ([aad9182](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/aad9182ce3cb22bcd324bff963ba492886fc283f))
* Updates MTS ([f012043](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/f0120430b353ac259d61edc28cfe05b50db3f427))

# [7.0.0-preview.1](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0...v7.0.0-preview.1) (2021-11-30)


### Bug Fixes

* Last Chance Deploy before Mynkow fix it ([aad9182](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/aad9182ce3cb22bcd324bff963ba492886fc283f))

# [7.0.0-preview.1](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0...v7.0.0-preview.1) (2021-11-25)

# [6.4.0](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.3.1...v6.4.0) (2021-11-25)


### Bug Fixes

* Adds Thread.Sleep on CreateConnection ([07f98cd](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/07f98cd13785d24f97cc018efe6544e3e392ed1a))
* Code cleanup ([a639230](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/a6392309c3538919d54895010ebedd55d6ae6584))
* Code fix ([9722d5d](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/9722d5d92567519c50e8c4f4a241a4ffd03ca258))
* Fixes copyright attribute ([e660954](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/e660954b291eca40a73996fd7788e8b52211095c))
* Fixes how exchange names are defined ([581fb59](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/581fb59858eb6c3945d154477013c1068e9e5704))
* Fixes how the bounded context is resolved on publish ([83f54c1](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/83f54c1b50f9f76324ca0db3ae2c77676c6c68a9))
* Perform performance ([60e9629](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/60e96293603d89f08aba91d77635bb651993f548))
* Properly stops consumer ([0f96707](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/0f9670765d446838ecc2a3a19b6645e8a8cac130))
* Reduces the amount of messages sent on re-publish ([1f25344](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/1f25344545997beb778ef987a664f2bf1f0e0c2f))
* Removes gitversion ([e8f97cc](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/e8f97cc92a11630fef0f8db1f7de79ee5c03545c))
* Removes message priority ([0db50a4](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/0db50a4b46612696d8f419647d9a0f33f74f5a11))
* Run pipeline ([8e081e2](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/8e081e213b81b3330b8293e07f12548c9e9c73dc))
* trigger ci ([938578c](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/938578c6ba4c1ad7b0f4f15bb6bd346f217d4ff0))
* Updates Cronus ([c658999](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/c658999c03fc4a03354b57314ba73e8f587a9a98))
* Updates Cronus ([27c8863](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/27c88631d833072c762317ba499198a24fe098c4))
* Updates Cronus ([3e0abe3](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/3e0abe3f17f2b9636f6634c77ccc322409d54835))
* Updates Cronus ([ddc3cea](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/ddc3cea1a9772c61f27d9068e9a40cce59ad7bc8))
* Wait a bit when starting a consumer. It needs warming up ([e79ca67](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/e79ca67701d974d575b7bd99e1475072efeae36b))


### Features

* Consolidates the release notes ([552792a](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/552792a6201b21ae9ae0831e38bcae36517dd094))
* Release ([6861518](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/6861518d2ae543f30c8a1a0bedf289f2ae1d6fd9))

# [6.4.0-preview.8](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.7...v6.4.0-preview.8) (2021-11-04)


### Bug Fixes

* trigger ci ([938578c](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/938578c6ba4c1ad7b0f4f15bb6bd346f217d4ff0))

# [6.4.0-preview.7](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.6...v6.4.0-preview.7) (2021-11-04)


### Bug Fixes

* Adds Thread.Sleep on CreateConnection ([07f98cd](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/07f98cd13785d24f97cc018efe6544e3e392ed1a))
* Code fix ([9722d5d](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/9722d5d92567519c50e8c4f4a241a4ffd03ca258))
* Perform performance ([60e9629](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/60e96293603d89f08aba91d77635bb651993f548))
* Run pipeline ([8e081e2](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/8e081e213b81b3330b8293e07f12548c9e9c73dc))

# [6.4.0-preview.7](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.6...v6.4.0-preview.7) (2021-11-04)


### Bug Fixes

* Adds Thread.Sleep on CreateConnection ([07f98cd](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/07f98cd13785d24f97cc018efe6544e3e392ed1a))
* Code fix ([9722d5d](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/9722d5d92567519c50e8c4f4a241a4ffd03ca258))
* Perform performance ([60e9629](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/60e96293603d89f08aba91d77635bb651993f548))
* Run pipeline ([8e081e2](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/8e081e213b81b3330b8293e07f12548c9e9c73dc))

# [6.4.0-preview.7](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.6...v6.4.0-preview.7) (2021-11-03)


### Bug Fixes

* Adds Thread.Sleep on CreateConnection ([07f98cd](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/07f98cd13785d24f97cc018efe6544e3e392ed1a))
* Code fix ([9722d5d](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/9722d5d92567519c50e8c4f4a241a4ffd03ca258))
* Perform performance ([60e9629](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/60e96293603d89f08aba91d77635bb651993f548))
* Run pipeline ([8e081e2](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/8e081e213b81b3330b8293e07f12548c9e9c73dc))

# [6.4.0-preview.7](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.6...v6.4.0-preview.7) (2021-11-03)


### Bug Fixes

* Adds Thread.Sleep on CreateConnection ([07f98cd](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/07f98cd13785d24f97cc018efe6544e3e392ed1a))
* Code fix ([9722d5d](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/9722d5d92567519c50e8c4f4a241a4ffd03ca258))
* Perform performance ([60e9629](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/60e96293603d89f08aba91d77635bb651993f548))

# [6.4.0-preview.7](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.6...v6.4.0-preview.7) (2021-11-03)


### Bug Fixes

* Adds Thread.Sleep on CreateConnection ([07f98cd](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/07f98cd13785d24f97cc018efe6544e3e392ed1a))
* Code fix ([9722d5d](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/9722d5d92567519c50e8c4f4a241a4ffd03ca258))
* Perform performance ([60e9629](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/60e96293603d89f08aba91d77635bb651993f548))

# [6.4.0-preview.7](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.6...v6.4.0-preview.7) (2021-11-03)


### Bug Fixes

* Adds Thread.Sleep on CreateConnection ([07f98cd](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/07f98cd13785d24f97cc018efe6544e3e392ed1a))
* Code fix ([9722d5d](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/9722d5d92567519c50e8c4f4a241a4ffd03ca258))
* Perform performance ([60e9629](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/60e96293603d89f08aba91d77635bb651993f548))

# [6.4.0-preview.7](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.6...v6.4.0-preview.7) (2021-11-03)


### Bug Fixes

* Code fix ([9722d5d](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/9722d5d92567519c50e8c4f4a241a4ffd03ca258))
* Perform performance ([60e9629](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/60e96293603d89f08aba91d77635bb651993f548))

# [6.4.0-preview.7](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.6...v6.4.0-preview.7) (2021-11-03)


### Bug Fixes

* Perform performance ([60e9629](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/60e96293603d89f08aba91d77635bb651993f548))

# [6.4.0-preview.6](https://github.com/Elders/Cronus.Transport.RabbitMQ/compare/v6.4.0-preview.5...v6.4.0-preview.6) (2021-10-28)


### Bug Fixes

* Code cleanup ([a639230](https://github.com/Elders/Cronus.Transport.RabbitMQ/commit/a6392309c3538919d54895010ebedd55d6ae6584))

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
* Fix nuget package dependencies.

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
