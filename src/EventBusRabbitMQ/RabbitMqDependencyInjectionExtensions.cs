using GSK.IPS.EventBusRabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using System;
using RabbitMQ.Client;
using OpenTelemetry.Instrumentation.AspNetCore;

namespace Microsoft.Extensions.Hosting;

public static class RabbitMqDependencyInjectionExtensions
{
    // {
    //   "EventBus": {
    //     "SubscriptionClientName": "...",
    //     "RetryCount": 10
    //   }
    // }

    private const string SectionName = "EventBus";

    public static IEventBusBuilder AddRabbitMqEventBus(this IHostApplicationBuilder builder, string connectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        //builder.AddRabbitMQ(connectionName, configureConnectionFactory: factory =>
        //{
        //    ((ConnectionFactory)factory).DispatchConsumersAsync = true;
        //});
        string eventBusConnection = builder.Configuration["ConnectionStrings:" + connectionName];

        builder.Services.AddSingleton(GetRabbitMqConnection(eventBusConnection));

        // RabbitMQ.Client doesn't have built-in support for OpenTelemetry, so we need to add it ourselves
        builder.Services.AddOpenTelemetry()
           .WithTracing(tracing =>
           {
               tracing.AddSource(RabbitMQTelemetry.ActivitySourceName);
           });

        // Options support
        builder.Services.Configure<EventBusOptions>(builder.Configuration.GetSection(SectionName));

        // Abstractions on top of the core client API
        builder.Services.AddSingleton<RabbitMQTelemetry>();
        builder.Services.AddSingleton<IEventBus, RabbitMQEventBus>();

        // Start consuming messages as soon as the application starts
        builder.Services.AddSingleton<IHostedService>(sp => (RabbitMQEventBus)sp.GetRequiredService<IEventBus>());

        return new EventBusBuilder(builder.Services);
    }

    private static IConnection GetRabbitMqConnection(string eventBusConnection)
    {
        ConnectionFactory factory = new ConnectionFactory()
        {
            Uri = new Uri(eventBusConnection),
            RequestedHeartbeat = new TimeSpan(0,0,15),
            //every N seconds the server will send a heartbeat.  If the connection does not receive a heartbeat within
            //N*2 then the connection is considered dead.
            //suggested from http://public.hudl.com/bits/archives/2013/11/11/c-rabbitmq-happy-servers/
            AutomaticRecoveryEnabled = true
        };
        //factory.UserName = "guest";
        //factory.Password = "guest";
        ////factory.VirtualHost = "/vhost";
        //factory.HostName = "rabbitmq";
        //factory.Port = 5672;
        return factory.CreateConnection();
    }

    private class EventBusBuilder(IServiceCollection services) : IEventBusBuilder
    {
        public IServiceCollection Services => services;
    }
}