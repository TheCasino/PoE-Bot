﻿namespace PoE.Bot
{
    using System;
    using Discord;
    using PoE.Bot.Helpers;
    using System.Net.Http;
    using PoE.Bot.Handlers;
    using Discord.Commands;
    using Discord.WebSocket;
    using System.Threading.Tasks;
    using PoE.Bot.Handlers.Objects;
    using PoE.Bot.Addons.Interactive;
    using Microsoft.Extensions.DependencyInjection;

    class PoE_Bot
    {
        static void Main(string[] args) => new PoE_Bot().InitializeAsync().GetAwaiter().GetResult();

        async Task InitializeAsync()
        {
            var Services = new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
                    MessageCacheSize = 20,
                    AlwaysDownloadUsers = true,
                    LogLevel = LogSeverity.Warning
                }))
                .AddSingleton(new CommandService(new CommandServiceConfig
                {
                    ThrowOnError = false,
                    IgnoreExtraArgs = false,
                    DefaultRunMode = RunMode.Sync,
                    CaseSensitiveCommands = false
                }))
                .AddSingleton<HttpClient>()
                .AddSingleton<DBHandler>()
                .AddSingleton<JobHandler>()
                .AddSingleton<GuildHelper>()
                .AddSingleton<EventHelper>()
                .AddSingleton<MainHandler>()
                .AddSingleton<InteractiveService>()
                .AddSingleton<Handlers.EventHandler>()
                .AddSingleton(new Random(Guid.NewGuid().GetHashCode()))
                .AddSingleton(x => x.GetRequiredService<DBHandler>().Execute<ConfigObject>(Operation.LOAD, Id: "Config"));

            var Provider = Services.BuildServiceProvider();
            await Provider.GetRequiredService<DBHandler>().InitializeAsync();
            await Provider.GetRequiredService<MainHandler>().InitializeAsync();
            await Provider.GetRequiredService<Handlers.EventHandler>().InitializeAsync();
            Provider.GetRequiredService<JobHandler>().Initialize();
            Provider.GetRequiredService<JobHandler>().RunJob(LogHandler.ForceGC, "garbage collector", 3);
            await Task.Delay(-1);
        }
    }
}
