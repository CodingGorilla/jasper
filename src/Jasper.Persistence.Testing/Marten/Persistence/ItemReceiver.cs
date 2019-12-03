﻿using IntegrationTests;
using Jasper.Configuration;
using Jasper.Messaging.Tracking;
using Jasper.Persistence.Marten;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Jasper.Persistence.Testing.Marten.Persistence
{
    public class ItemReceiver : JasperOptions
    {
        public ItemReceiver()
        {
            Include<MartenBackedPersistence>();

            Settings.Alter<StoreOptions>(_ =>
            {
                _.Connection(Servers.PostgresConnectionString);
                _.DatabaseSchemaName = "receiver";
            });

            Services.AddSingleton<MessageTracker>();

            Endpoints.ListenAtPort(2345).Durably();

        }
    }
}
