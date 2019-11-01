﻿using System;
using System.Threading;
using Jasper.Messaging.Logging;
using Jasper.Messaging.Model;
using Jasper.Messaging.Runtime;
using Jasper.Messaging.Transports;
using Jasper.Messaging.Transports.Sending;
using Microsoft.Extensions.Logging;

namespace Jasper.RabbitMQ.Internal
{
    public class RabbitMqTransport : ExternalTransportBase<RabbitMqOptions, RabbitMqEndpoint>
    {
        public RabbitMqTransport(RabbitMqOptions options, ITransportLogger logger, JasperOptions jasperOptions) : base("rabbitmq", options, logger, jasperOptions)
        {
        }

        protected override ISender buildSender(TransportUri transportUri, RabbitMqEndpoint endpoint, CancellationToken cancellation)
        {
            endpoint.Connect();
            return endpoint.CreateSender(logger, cancellation);
        }

        protected override IListeningAgent buildListeningAgent(TransportUri transportUri, RabbitMqEndpoint endpoint,
            JasperOptions settings, HandlerGraph handlers)
        {
            if (endpoint == null)
            {
                throw new ArgumentOutOfRangeException(nameof(transportUri), $"Could not resolve a Rabbit MQ endpoint for the Uri '{transportUri}'");
            }

            if (transportUri.IsMessageSpecificTopic())
            {
                return new RabbitMqMessageSpecificTopicListeningAgent(endpoint, handlers, transportUri, logger, settings);
            }
            else
            {
                endpoint.Connect();
                return endpoint.CreateListeningAgent(transportUri.ToUri(), settings, logger);
            }


        }
    }
}
