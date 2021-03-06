﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Baseline.Dates;
using IntegrationTests;
using Jasper.Attributes;
using Jasper.Configuration;
using Jasper.Persistence;
using Jasper.Persistence.Marten;
using Jasper.RabbitMQ.Internal;
using Jasper.Tracking;
using Jasper.Util;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Oakton;
using Shouldly;
using TestingSupport;
using Xunit;
using ConsoleWriter = Oakton.ConsoleWriter;

namespace Jasper.RabbitMQ.Tests
{
    [Collection("marten")]
    public class end_to_end : RabbitMQContext
    {

        [Fact]
        public async Task send_message_to_and_receive_through_rabbitmq()
        {
            using (var host = JasperHost.For<RabbitMqUsingApp>())
            {
                await host
                    .TrackActivity()
                    .IncludeExternalTransports()
                    .SendMessageAndWait(new ColorChosen {Name = "Red"});

                var colors = host.Get<ColorHistory>();

                colors.Name.ShouldBe("Red");
            }
        }


        [Fact]
        public async Task send_message_to_and_receive_through_rabbitmq_with_durable_transport_option()
        {
            var publisher = JasperHost.For(_ =>
            {
                _.Extensions.UseMessageTrackingTestingSupport();

                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                    x.DeclareQueue("messages2");
                    x.AutoProvision = true;
                });

                _.Endpoints
                    .PublishAllMessages()
                    .ToRabbit("messages2")
                    .Durably();

                _.Extensions.UseMarten(x =>
                {
                    x.Connection(Servers.PostgresConnectionString);
                    x.AutoCreateSchemaObjects = AutoCreate.All;
                    x.DatabaseSchemaName = "sender";
                });

                _.Advanced.StorageProvisioning = StorageProvisioning.Rebuild;

            });

            var receiver = JasperHost.For(_ =>
            {
                _.Extensions.UseMessageTrackingTestingSupport();

                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                    x.DeclareQueue("messages2");
                    x.AutoProvision = true;
                });

                _.Endpoints.ListenToRabbitQueue("messages2");
                _.Services.AddSingleton<ColorHistory>();

                _.Extensions.UseMarten(x =>
                {
                    x.Connection(Servers.PostgresConnectionString);
                    x.AutoCreateSchemaObjects = AutoCreate.All;
                    x.DatabaseSchemaName = "receiver";
                });

                _.Advanced.StorageProvisioning = StorageProvisioning.Rebuild;
            });


            try
            {

                await publisher
                    .TrackActivity()
                    .AlsoTrack(receiver)
                    .SendMessageAndWait(new ColorChosen {Name = "Orange"});


                receiver.Get<ColorHistory>().Name.ShouldBe("Orange");
            }
            finally
            {
                publisher.Dispose();
                receiver.Dispose();
            }
        }


        [Fact]
        public async Task reply_uri_mechanics()
        {
            var publisher = JasperHost.For(_ =>
            {
                _.ServiceName = "Publisher";
                _.Extensions.UseMessageTrackingTestingSupport();

                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                    x.DeclareQueue("messages20");
                    x.DeclareQueue("messages21");
                    x.AutoProvision = true;
                });

                _.Endpoints
                    .PublishAllMessages()
                    .ToRabbit("messages20")
                    .Durably();

                _.Endpoints.ListenToRabbitQueue("messages21").UseForReplies();

                _.Extensions.UseMarten(x =>
                {
                    x.Connection(Servers.PostgresConnectionString);
                    x.AutoCreateSchemaObjects = AutoCreate.All;
                    x.DatabaseSchemaName = "sender";
                });

                _.Advanced.StorageProvisioning = StorageProvisioning.Rebuild;

            });

            var receiver = JasperHost.For(_ =>
            {
                _.ServiceName = "Receiver";

                _.Extensions.UseMessageTrackingTestingSupport();

                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                    x.DeclareQueue("messages20");
                    x.AutoProvision = true;
                });

                _.Endpoints.ListenToRabbitQueue("messages20");
                _.Services.AddSingleton<ColorHistory>();

                _.Extensions.UseMarten(x =>
                {
                    x.Connection(Servers.PostgresConnectionString);
                    x.AutoCreateSchemaObjects = AutoCreate.All;
                    x.DatabaseSchemaName = "receiver";
                });

                _.Advanced.StorageProvisioning = StorageProvisioning.Rebuild;
            });


            try
            {

                var session = await publisher
                    .TrackActivity()
                    .AlsoTrack(receiver)
                    .SendMessageAndWait(new PingMessage{Number = 1});


                // TODO -- let's make an assertion here?
                var records = session.FindEnvelopesWithMessageType<PongMessage>(EventType.Received);
                records.Any(x => x.ServiceName == "Publisher").ShouldBeTrue();
            }
            finally
            {
                publisher.Dispose();
                receiver.Dispose();
            }
        }




        [Fact]
        public async Task send_message_to_and_receive_through_rabbitmq_with_routing_key()
        {
            var queueName = "messages5";
            var exchangeName = "exchange1";

            var publisher = JasperHost.For(_ =>
            {
                _.Extensions.UseMessageTrackingTestingSupport();

                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                    x.DeclareQueue(queueName);
                    x.DeclareExchange(exchangeName);
                    x.DeclareBinding(new Binding
                    {
                        ExchangeName = exchangeName,
                        BindingKey = "key2",
                        QueueName =  queueName
                    });

                    x.AutoProvision = true;
                });

                _.Endpoints.PublishAllMessages().ToRabbit("key2", exchangeName);

            });

            var receiver = JasperHost.For(_ =>
            {
                _.Extensions.UseMessageTrackingTestingSupport();

                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                    x.DeclareQueue("messages5");
                    x.DeclareExchange(exchangeName);
                    x.DeclareBinding(new Binding
                    {
                        ExchangeName = exchangeName,
                        BindingKey = "key2",
                        QueueName =  queueName
                    });

                    x.AutoProvision = true;
                });

                _.Services.AddSingleton<ColorHistory>();

                _.Endpoints.ListenToRabbitQueue(queueName);
            });

            try
            {
                await publisher
                    .TrackActivity()
                    .AlsoTrack(receiver)
                    .SendMessageAndWait(new ColorChosen {Name = "Orange"});

                receiver.Get<ColorHistory>().Name.ShouldBe("Orange");
            }
            finally
            {
                publisher.Dispose();
                receiver.Dispose();
            }
        }


        [Fact]
        public async Task schedule_send_message_to_and_receive_through_rabbitmq_with_durable_transport_option()
        {
            var uri = "rabbitmq://default/messages11/durable";

            var publisher = JasperHost.For(_ =>
            {
                _.Advanced.ScheduledJobFirstExecution = 1.Seconds();
                _.Advanced.ScheduledJobPollingTime = 1.Seconds();
                _.ServiceName = "Publisher";

                _.Extensions.UseMessageTrackingTestingSupport();

                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                    x.DeclareQueue("messages11");


                    x.AutoProvision = true;
                });


                _.Endpoints.PublishAllMessages().ToRabbit("messages11").Durably();



                _.Extensions.UseMarten(x =>
                {
                    x.Connection(Servers.PostgresConnectionString);
                    x.AutoCreateSchemaObjects = AutoCreate.All;
                    x.DatabaseSchemaName = "rabbit_sender";
                });
            });

            publisher.RebuildMessageStorage();

            var receiver = JasperHost.For(_ =>
            {
                _.ServiceName = "Receiver";

                _.Extensions.UseMessageTrackingTestingSupport();

                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                });



                _.Endpoints.ListenToRabbitQueue("messages11");
                _.Services.AddSingleton<ColorHistory>();

                _.Extensions.UseMarten(x =>
                {
                    x.Connection(Servers.PostgresConnectionString);
                    x.AutoCreateSchemaObjects = AutoCreate.All;
                    x.DatabaseSchemaName = "rabbit_receiver";
                });
            });

            receiver.RebuildMessageStorage();



            try
            {
                await publisher
                    .TrackActivity()
                    .AlsoTrack(receiver)
                    .WaitForMessageToBeReceivedAt<ColorChosen>(receiver)
                    .Timeout(15.Seconds())
                    .ExecuteAndWait(c => c.ScheduleSend(new ColorChosen {Name = "Orange"}, 5.Seconds()));

                receiver.Get<ColorHistory>().Name.ShouldBe("Orange");
            }
            finally
            {
                publisher.Dispose();
                receiver.Dispose();
            }
        }


        [Fact]
        public async Task use_fan_out_exchange()
        {
            var exchangeName = "fanout";
            var queueName1 = "messages12";
            var queueName2 = "messages13";
            var queueName3 = "messages14";


            var publisher = JasperHost.For(_ =>
            {
                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                    x.DeclareExchange(exchangeName, ex => ex.ExchangeType = ExchangeType.Fanout);
                    x.DeclareQueue(queueName1);
                    x.DeclareQueue(queueName2);
                    x.DeclareQueue(queueName3);
                    x.DeclareBinding(new Binding
                    {
                        BindingKey = "key1",
                        QueueName = queueName1,
                        ExchangeName = exchangeName
                    });

                    x.DeclareBinding(new Binding
                    {
                        BindingKey = "key1",
                        QueueName = queueName2,
                        ExchangeName = exchangeName
                    });

                    x.DeclareBinding(new Binding
                    {
                        BindingKey = "key1",
                        QueueName = queueName3,
                        ExchangeName = exchangeName
                    });

                    x.AutoProvision = true;
                });

                _.Extensions.UseMessageTrackingTestingSupport();

                _.Endpoints.PublishAllMessages().ToRabbitExchange("fanout");

            });

            var receiver1 = JasperHost.For(_ =>
            {
                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                });



                _.Extensions.UseMessageTrackingTestingSupport();
                _.Endpoints.ListenToRabbitQueue(queueName1);
                _.Services.AddSingleton<ColorHistory>();
            });

            var receiver2 = JasperHost.For(_ =>
            {
                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                });


                _.Extensions.UseMessageTrackingTestingSupport();
                _.Endpoints.ListenToRabbitQueue(queueName2);
                _.Services.AddSingleton<ColorHistory>();
            });

            var receiver3 = JasperHost.For(_ =>
            {
                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                });


                _.Extensions.UseMessageTrackingTestingSupport();
                _.Endpoints.ListenToRabbitQueue(queueName3);

                _.Services.AddSingleton<ColorHistory>();
            });


            try
            {
                var session = await publisher
                    .TrackActivity()
                    .AlsoTrack(receiver1, receiver2, receiver3)
                    .WaitForMessageToBeReceivedAt<ColorChosen>(receiver1)
                    .WaitForMessageToBeReceivedAt<ColorChosen>(receiver2)
                    .WaitForMessageToBeReceivedAt<ColorChosen>(receiver3)
                    .SendMessageAndWait(new ColorChosen {Name = "Purple"});


                receiver1.Get<ColorHistory>().Name.ShouldBe("Purple");
                receiver2.Get<ColorHistory>().Name.ShouldBe("Purple");
                receiver3.Get<ColorHistory>().Name.ShouldBe("Purple");
            }
            finally
            {
                publisher.Dispose();
                receiver1.Dispose();
                receiver2.Dispose();
                receiver3.Dispose();
            }
        }





        [Fact]
        public async Task send_message_to_and_receive_through_rabbitmq_with_named_topic()
        {

            var queueName = "messages4";

            var publisher = JasperHost.For(_ =>
            {
                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                    x.DeclareExchange("topics", ex => { ex.ExchangeType = ExchangeType.Topic; });
                    x.DeclareQueue("messages4");
                    x.DeclareBinding(new Binding
                    {
                        BindingKey = "special",
                        ExchangeName = "topics",
                        QueueName = queueName
                    });

                    x.AutoProvision = true;
                });

                _.Endpoints.PublishAllMessages().ToRabbit("special", "topics");

                _.Handlers.DisableConventionalDiscovery();

                _.Extensions.UseMessageTrackingTestingSupport();

            });

            var receiver = JasperHost.For(_ =>
            {
                _.Endpoints.ConfigureRabbitMq(x =>
                {
                    x.ConnectionFactory.HostName = "localhost";
                });

                _.Endpoints.ListenToRabbitQueue(queueName);

                _.Extensions.UseMessageTrackingTestingSupport();

                _.Handlers.DisableConventionalDiscovery().IncludeType<SpecialTopicGuy>();

            });



            try
            {
                var message = new SpecialTopic();
                var session = await publisher.TrackActivity().AlsoTrack(receiver).SendMessageAndWait(message);


                var received = session.FindSingleTrackedMessageOfType<SpecialTopic>(EventType.MessageSucceeded);
                received
                    .Id.ShouldBe(message.Id);


            }
            finally
            {
                publisher.Dispose();
                receiver.Dispose();
            }
        }







    }

    public class SpecialTopicGuy
    {
        public void Handle(SpecialTopic topic)
        {

        }
    }


    public class RabbitMqUsingApp : JasperOptions
    {
        public RabbitMqUsingApp()
        {
            Extensions.UseMessageTrackingTestingSupport();

            Endpoints.ConfigureRabbitMq(x =>
            {
                x.ConnectionFactory.HostName = "localhost";
                x.DeclareQueue("messages3");
                x.AutoProvision = true;
            });

            Endpoints.ListenToRabbitQueue("messages3");

            Services.AddSingleton<ColorHistory>();

            Endpoints.PublishAllMessages().ToRabbit("messages3");

        }
    }

    public class ColorHandler
    {
        public void Handle(ColorChosen message, ColorHistory history, Envelope envelope)
        {
            history.Name = message.Name;
            history.Envelope = envelope;
        }
    }

    public class ColorHistory
    {
        public string Name { get; set; }
        public Envelope Envelope { get; set; }
    }

    public class ColorChosen
    {
        public string Name { get; set; }
    }


    [MessageIdentity("A")]

    public class TopicA
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }

    [MessageIdentity("B")]
    public class TopicB
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }

    [MessageIdentity("C")]
    public class TopicC
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }

    public class SpecialTopic
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }

    // The [MessageIdentity] attribute is only necessary
    // because the projects aren't sharing types
    // You would not do this if you were distributing
    // message types through shared assemblies
    [MessageIdentity("TryToReconnect")]
    public class PingMessage
    {
        public int Number { get; set; }
    }

    [MessageIdentity("Pong")]
    public class PongMessage
    {
        public int Number { get; set; }
    }

    public static class PongHandler
    {
        // "Handle" is recognized by Jasper as a message handling
        // method. Handler methods can be static or instance methods
        public static void Handle(PongMessage message)
        {
            ConsoleWriter.Write(ConsoleColor.Blue, $"Got pong #{message.Number}");
        }
    }

    public static class PingHandler
    {
        // Simple message handler for the PingMessage message type
        public static Task Handle(
            // The first argument is assumed to be the message type
            PingMessage message,

            // Jasper supports method injection similar to ASP.Net Core MVC
            // In this case though, IMessageContext is scoped to the message
            // being handled
            IMessageContext context)
        {
            ConsoleWriter.Write(ConsoleColor.Blue, $"Got ping #{message.Number}");

            var response = new PongMessage
            {
                Number = message.Number
            };

            // This usage will send the response message
            // back to the original sender. Jasper uses message
            // headers to embed the reply address for exactly
            // this use case
            return context.RespondToSender(response);
        }
    }

}
