﻿namespace PoE.Bot.Modules
{
    using Addons;
    using Addons.Preconditions;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;
    using Helpers;
    using Objects;
    using Objects.PathOfBuilding;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    [Name("General Commands"), Ratelimit]
    public class GeneralModule : BotBase
    {
        public CommandService CommandService { get; set; }
        private IServiceProvider Provider { get; }

        [Command("About", RunMode = RunMode.Async), Summary("Displays information about the Server stats, and PoE Bot stats."), Remarks("About")]
        public async Task AboutAsync()
        {
            DiscordSocketClient client = Context.Client as DiscordSocketClient;
            var textChannels = await Context.Guild.GetTextChannelsAsync().ConfigureAwait(false);
            var categories = await Context.Guild.GetCategoriesAsync().ConfigureAwait(false);
            var voiceChannels = await Context.Guild.GetVoiceChannelsAsync().ConfigureAwait(false);
            var users = await Context.Guild.GetUsersAsync().ConfigureAwait(false);
            Embed embed = Extras.Embed(Extras.Info)
                .WithAuthor($"{Context.Client.CurrentUser.Username} Statistics 🤖", Context.Client.CurrentUser.GetAvatarUrl() ?? Context.Client.CurrentUser.GetDefaultAvatarUrl())
                .WithDescription((await client.GetApplicationInfoAsync().ConfigureAwait(false)).Description)
                .AddField("Wraeclast Info", $"Wraeclast was created on {Context.Guild.CreatedAt.DateTime.ToLongDateString()} @ {Context.Guild.CreatedAt.DateTime.ToLongTimeString()}")
                .AddField("Kitava", (await Context.Guild.GetUserAsync(Context.Guild.OwnerId).ConfigureAwait(false)).Mention, true)
                .AddField("Map", Context.Guild.VoiceRegionId, true)
                .AddField("Mod", Context.Guild.VerificationLevel.ToString(), true)
                .AddField($"Chat [{(textChannels.Count - categories.Count) + voiceChannels.Count}]",
                    $"categories: {categories.Count}\n" +
                    $"Text: {textChannels.Count - categories.Count}\n" +
                    $"Voice: {voiceChannels.Count}\n", true)
                .AddField($"Characters [{users.Count()}]",
                    $"Exiles: {users.Count(x => x.IsBot is false)}\n" +
                    $"Lieutenants: {users.Count(x => x.IsBot is true)}\n", true)
                .AddField($"Roles [{Context.Guild.Roles.Count}]",
                    $"Separated: {Context.Guild.Roles.Count(x => x.IsHoisted is true)}\n" +
                    $"Mentionable: {Context.Guild.Roles.Count(x => x.IsMentionable is true)}", true)
                .AddField("Wraeclast Stats", "Wraeclast has the following data logged:")
                .AddField("Items",
                    $"Tags: {Context.Server.Tags.Count}\n" +
                    $"Currencies: {Context.Server.Prices.Count}\n" +
                    $"Shop Items: {Context.Server.Shops.Count}", true)
                .AddField("Streams",
                    $"Mixer: {Context.Server.Streams.Count(s => s.StreamType is StreamType.Mixer)}\n" +
                    $"Twitch: {Context.Server.Streams.Count(s => s.StreamType is StreamType.Twitch)}", true)
                .AddField("Leaderboard", $"Variants: {Context.Server.Leaderboards.Count(x => x.Enabled is true)}", true)
                .AddField("Lieutenant Info", "Info about myself:")
                .AddField("Uptime", $"{(DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss")}", true)
                .AddField("Memory", $"Heap Size: {Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2)} MB", true)
                .AddField("Izaro", $"[@{(await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false)).Owner}](https://discord.me/poe_xbox)", true)
                .Build();
            await ReplyAsync(embed: embed).ConfigureAwait(false);
        }

        [Command("AFK"), Summary("Adds, Deletes or Updates your AFK status."), Remarks("AFK <action> <afkMessage>")]
        public Task AfkAsync(CommandAction action, [Remainder] string afkMessage = "Running around Wraeclast, slaying monsters. Shoot me a DM.")
        {
            switch (action)
            {
                case CommandAction.Add:
                    if (Context.Server.AFK.ContainsKey(Context.User.Id))
                        return ReplyAsync($"{Extras.Cross} Exile, it seems you are already in Wraeclast.");

                    Context.Server.AFK.Add(Context.User.Id, afkMessage);
                    return ReplyAsync($"Exiles will be notified that you are in Wraeclast when you are mentioned {Extras.OkHand}", save: DocumentType.Server);

                case CommandAction.Delete:
                    if (!Context.Server.AFK.ContainsKey(Context.User.Id))
                        return ReplyAsync($"{Extras.Cross} Exile, it seems you are not in Wraeclast.");

                    Context.Server.AFK.Remove(Context.User.Id);
                    return ReplyAsync($"You've returned from Wraeclast {Extras.OkHand}", save: DocumentType.Server);

                case CommandAction.Update:
                    if (!Context.Server.AFK.ContainsKey(Context.User.Id))
                        return ReplyAsync($"{Extras.Cross} Exile, it seems you are not in Wraeclast.");

                    Context.Server.AFK[Context.User.Id] = afkMessage;
                    return ReplyAsync($"Your Wraeclast messages has been modified {Extras.OkHand}", save: DocumentType.Server);

                default:
                    return ReplyAsync($"{Extras.Cross} Action is either `Add`, `Delete`, or `Update`.");
            }
        }

        [Command("Feedback", RunMode = RunMode.Async), Summary("Give feedback on my performance or suggest new features!"), Remarks("Feedback")]
        public async Task FeedbackAsync()
        {
            var message = MethodHelper.CalculateResponse(await WaitAsync("Wisdom is the offspring of Suffering and Time. *Please provide your feedback in a couple sentences.*", timeout: TimeSpan.FromMinutes(1)).ConfigureAwait(false));
            if (!message.Item1)
            {
                await ReplyAsync(message.Item2).ConfigureAwait(false);
                return;
            }
            IMessageChannel channel = (Context.Client as DiscordSocketClient).GetChannel(Context.Config.FeedbackChannel) as IMessageChannel;
            await channel.SendMessageAsync(message.Item2).ConfigureAwait(false);
            await ReplyAsync($"Behold the machinery at maximum efficiency! {Extras.OkHand}").ConfigureAwait(false);
        }

        [Command("Hide"), Summary("Hides the League sections you aren't interested in."), Remarks("Hide <name>"), RequireChannel("role-setup")]
        public Task HideAsync(IRole role)
            => Context.Message.DeleteAsync().ContinueWith(_ =>
            {
                SocketGuildUser user = Context.User as SocketGuildUser;
                if (!Context.Server.SelfRoles.Contains(role.Id))
                    return ReplyAndDeleteAsync($"{Extras.Cross} Exile, you can't assign this role.");
                else if (!user.Roles.Contains(role))
                    return ReplyAndDeleteAsync($"{Extras.Cross} Exile, you don't have this role.");

                user.RemoveRoleAsync(role);
                return ReplyAndDeleteAsync($"Hm. How fascinating. *Role has been removed from you.* {Extras.OkHand}");
            });

        [Command("IAgree"), Alias("Agree"), Summary("You agree to the rules."), Remarks("IAgree"), RequireChannel("role-setup")]
        public Task IAgreeAsync()
            => Context.Message.DeleteAsync().ContinueWith(_ =>
            {
                IGuildUser user = Context.User as IGuildUser;
                if (user.RoleIds.Contains(Context.Server.MainRole))
                    return ReplyAndDeleteAsync($"{Extras.Cross} Exile, you have already agreed to the rules.");

                user.AddRoleAsync(Context.Guild.GetRole(Context.Server.MainRole));
                return ReplyAndDeleteAsync($"It seems this new arena suits me! *You have been given access to the server.* {Extras.OkHand}");
            });

        [Command("Invites"), Summary("Returns a list of Invites on the server."), Remarks("Invites")]
        public async Task InvitesAsync()
            => await PagedReplyAsync(MethodHelper.Pages((await Context.Guild.GetInvitesAsync().ConfigureAwait(false)).Select(i =>
                $"**{(i.Inviter as IGuildUser).Nickname ?? i.Inviter.Username}**\n#{i.ChannelName} *{i.Uses} Uses*\n{i.Url}\n{i.CreatedAt?.ToString("f")}\n")), $"{Context.Guild.Name}'s Invites").ConfigureAwait(false);

        [Command("Lab"), Summary("Get the link for PoE Lab."), Remarks("Lab")]
        public Task LabAsync()
            => ReplyAsync(embed: Extras.Embed(Extras.Info)
                .WithTitle("Please turn off any Ad Blockers you have to help the team keep doing Izaros work.")
                .WithDescription("[Homepage](https://www.poelab.com/) - [Support](https://www.poelab.com/support/) - [Lab Info](https://www.poelab.com/all-enchantments/) - [Lab Guide](https://www.poelab.com/new-to-the-labyrinth/) - [Puzzle Solutions](https://www.poelab.com/puzzle-solutions/) - [Trial Tracker](https://www.poelab.com/trial-tracker/)")
                .Build());

        [Command("Ping", RunMode = RunMode.Async), Summary("Returns the current estimated round-trip latency"), Remarks("Ping")]
        public async Task PingAsync()
        {
            ulong target = 0;
            CancellationTokenSource cts = new CancellationTokenSource();
            DiscordSocketClient client = Context.Client as DiscordSocketClient;

            Task WaitTarget(SocketMessage message)
            {
                if (message.Id != target)
                    return Task.CompletedTask;
                cts.Cancel();
                return Task.CompletedTask;
            }

            int latency = client.Latency;
            Stopwatch watch = Stopwatch.StartNew();
            IUserMessage m = await ReplyAsync($"**Heartbeat**: {latency}ms, **Init**: ---, **RTT**: ---").ConfigureAwait(false);
            long init = watch.ElapsedMilliseconds;
            target = m.Id;
            watch.Restart();
            client.MessageReceived += WaitTarget;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                long rtt = watch.ElapsedMilliseconds;
                watch.Stop();
                await m.ModifyAsync(x => x.Content = $"**Heartbeat**: {latency}ms, **Init**: {init}ms, **RTT**: {rtt}ms").ConfigureAwait(false);
                return;
            }
            finally
            {
                client.MessageReceived -= WaitTarget;
            }
            watch.Stop();
            await m.ModifyAsync(x => x.Content = $"**Heartbeat**: {latency}ms, **Init**: {init}ms, **RTT**: Timedout").ConfigureAwait(false);
        }

        [Command("PoB"), Summary("Parses the PasteBin export from Path of Building and shows the information about the build."), Remarks("PoB <pasteBinURL>")]
        public async Task PoBAsync([Remainder] string pasteBinURL)
        {
            Parser parser = new Parser();
            PasteBinFetcher pastebinFetcher = new PasteBinFetcher(Context.HttpClient);

            try
            {
                string base64 = await pastebinFetcher.GetRawCode(pasteBinURL).ConfigureAwait(false);
                Character character = parser.ParseCode(base64);
                EmbedBuilder embed = Extras.Embed(Extras.Info)
                    .AddField(PathOfBuildingHelper.GenerateDefenseField(character))
                    .AddField(PathOfBuildingHelper.GenerateOffenseField(character))
                    .WithFooter($"Pastebin: {pasteBinURL}")
                    .WithTitle($"{(PathOfBuildingHelper.IsSupport(character) ? "Support" : character.Skills.MainSkillGroup.Gems.Where(e => e.Enabled).Select(e => e.Name).First())} - {(character.Ascendancy == string.Empty ? character.Class : character.Ascendancy)} (Lvl: {character.Level})")
                    .WithThumbnailUrl($"https://raw.githubusercontent.com/Kyle-Undefined/PoE.Bot/master/Path-Of-Building/Images/Classes/{(character.Ascendancy == string.Empty ? character.Class : character.Ascendancy)}.png")
                    .WithAuthor(Context.User)
                    .WithCurrentTimestamp();

                if (PathOfBuildingHelper.ShowCharges(character.Config) && (character.Summary.EnduranceCharges > 0 || character.Summary.PowerCharges > 0 || character.Summary.FrenzyCharges > 0))
                    embed.AddField(PathOfBuildingHelper.GenerateChargesField(character));

                embed.AddField(PathOfBuildingHelper.GenerateSkillsField(character));

                if (character.Config.Contains("Input"))
                    embed.AddField(PathOfBuildingHelper.GenerateConfigField(character));

                embed.AddField("**Info**:", $"[Tree]({PathOfBuildingHelper.GenerateTreeURL(character.Tree, Context.HttpClient)}) - powered by [Path of Building](https://github.com/Openarl/PathOfBuilding) - Help from [Faust](https://github.com/FWidm/discord-pob) and [thezensei](https://github.com/andreandersen/LiftDiscord/).");

                await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"Error fetching build data. Reason: {ex.Message}").ConfigureAwait(false);
            }
        }

        [Command("Price"), Remarks("Price <name: Any Alias> [league: Defaults to Challenge]"), Summary("Pulls the price for the requested currency, in the chosen league, all values based on Chaos."), BanChannel("price-checkers")]
        public Task PriceAsync(string name, Leagues league = Leagues.Challenge)
        {
            if (!Context.Server.Prices.Any(p => p.Alias.Contains(name.ToLower()) && p.League == league))
                return ReplyAsync($"{Extras.Cross} What in God's name is that smell? *`{name}` is not in the `{league}` list.*");

            PriceObject price = Context.Server.Prices.FirstOrDefault(p => p.Alias.Contains(name.ToLower()) && p.League == league);
            Embed embed = Extras.Embed(Extras.Info)
                .AddField($"{price.Name.Replace("_", " ")} in {league} league", $"```{price.Alias}```")
                .AddField("Ratio", $"```{price.Quantity}:{price.Price}c```")
                .AddField("Last Updated", $"```{price.LastUpdated}```")
                .WithFooter("Please be mindful of the Last Updated date and time, as these prices are gathered through community feedback. As you do your trades, if you could kindly report your ratios to a @Price Checker, we would greatly appreciate it as it keeps the prices current.")
                .Build();

            return ReplyAsync(embed: embed);
        }

        [Command("PriceList"), Remarks("PriceList [league: Defaults to Challenge]"), Summary("Pulls the price for the all currency, in the specified league, defaults to Challenge")]
        public Task PriceListAsync(Leagues league = Leagues.Challenge)
        {
            var prices = Context.Server.Prices.Where(x => x.League == league).Select(x =>
                $"**{x.Name.Replace("_", " ")}**\n" +
                $"*{x.Alias}*\n" +
                $"Ratio: {x.Quantity}:{x.Price}c\n" +
                $"Last Updated: {x.LastUpdated}\n");
            return PagedReplyAsync(MethodHelper.Pages(prices), $"{league} Price List");
        }

        [Command("Remind"), Summary("Set a reminder for later. Time is formatted like: Number(d/h/m/s) Example: 5h for 5 Hours."), Remarks("Remind <time> <message>")]
        public async Task RemindAsync(TimeSpan time, [Remainder] string message)
        {
            var reminders = new List<RemindObject>();
            if (Context.Server.Reminders.ContainsKey(Context.User.Id))
                Context.Server.Reminders.TryGetValue(Context.User.Id, out reminders);

            reminders.Add(new RemindObject
            {
                Message = message.Replace("`", string.Empty),
                TextChannel = Context.Channel.Id,
                RequestedDate = DateTime.Now,
                ExpiryDate = DateTime.Now.Add(time)
            });

            Context.Server.Reminders.AddOrUpdate(Context.User.Id, reminders, (key, value) => reminders);
            await ReplyAsync($"This land has forgotten Karui strength, {Context.User.Mention}. I will remind it. ({StringHelper.FormatTimeSpan(time)})", save: DocumentType.Server).ConfigureAwait(false);
        }

        [Command("Reminder"), Summary("Deletes a reminder, or Lists your reminders."), Remarks("Reminder <action> [number]")]
        public Task ReminderAsync(CommandAction action = CommandAction.List, int number = int.MinValue)
        {
            switch (action)
            {
                case CommandAction.Delete:
                    if (Context.Server.Reminders.All(x => x.Key != Context.User.Id))
                        return ReplyAsync($"{Extras.Cross} Exile, you don't have reminders.");

                    if (number is int.MinValue)
                        number = 0;
                    Context.Server.Reminders.TryGetValue(Context.User.Id, out var remindersDelete);
                    try
                    {
                        remindersDelete.RemoveAt(number);
                    }
                    catch
                    {
                        return ReplyAsync($"{Extras.Cross} Exile, invalid reminder number was provided.");
                    }
                    if (!remindersDelete.Any())
                        Context.Server.Reminders.TryRemove(Context.User.Id, out _);
                    else
                        Context.Server.Reminders.TryUpdate(Context.User.Id, remindersDelete, Context.Server.Reminders.FirstOrDefault(x => x.Key == Context.User.Id).Value);
                    return ReplyAsync($"Reminder #{number} deleted {Extras.Trash}", save: DocumentType.Server);

                case CommandAction.List:
                    if (Context.Server.Reminders.All(x => x.Key != Context.User.Id))
                        return ReplyAsync($"{Extras.Cross} Exile, you don't have reminders.");

                    var reminder = Context.Server.Reminders.First(x => x.Key == Context.User.Id);
                    var reminders = new List<string>();
                    reminders.AddRange(reminder.Value.Select((r, i) => $"Reminder #**{i}** | Expires on: **{r.ExpiryDate}**\n**Message:** {r.Message}"));
                    return PagedReplyAsync(reminders, "Your Current Reminders");

                default:
                    return ReplyAsync($"{Extras.Cross} Action is either `Delete` or `List`.");
            }
        }

        [Command("Report"), Summary("Reports a user to guild moderators."), Remarks("Report <@user> <reason>")]
        public async Task ReportAsync(IGuildUser user, [Remainder] string reason)
            => await Context.Message.DeleteAsync().ContinueWith(async _ =>
            {
                ITextChannel rep = await Context.Guild.GetTextChannelAsync(Context.Server.RepLog).ConfigureAwait(false);
                Embed embed = Extras.Embed(Extras.Report)
                    .WithAuthor((Context.User as IGuildUser).Nickname ?? Context.User.Username, Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                    .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                    .WithTitle($"Report for {user.Nickname ?? user.Username}")
                    .WithDescription($"**Reason:**\n{reason}")
                    .Build();

                await rep.SendMessageAsync(embed: embed).ConfigureAwait(false);
            }).ConfigureAwait(false);

        [Command("Show"), Summary("Shows the League sections you are interested in."), Remarks("Show <name>"), RequireChannel("role-setup")]
        public Task ShowAsync(IRole role)
            => Context.Message.DeleteAsync().ContinueWith(_ =>
            {
                SocketGuildUser user = Context.User as SocketGuildUser;
                IRole mainRole = Context.Guild.GetRole(Context.Server.MainRole);
                if (!user.Roles.Contains(mainRole))
                    return ReplyAndDeleteAsync($"{Extras.Cross} Exile, you haven't accepted the rules, please type !iagree first.");
                else if (!Context.Server.SelfRoles.Contains(role.Id))
                    return ReplyAndDeleteAsync($"{Extras.Cross} Exile, you can't assign this role.");
                else if (user.Roles.Contains(role))
                    return ReplyAndDeleteAsync($"{Extras.Cross} Exile, you already have this role.");

                user.AddRoleAsync(role);
                return ReplyAndDeleteAsync($"It seems this new arena suits me! *Role has been added to you.* {Extras.OkHand}");
            });

        [Command("Trial"), Remarks("Trial <trial>"), RequireChannel("lab-and-trials"), Summary("Announce a Trial of Ascendancy that you have come across. Any part of the Trial Name.")]
        public Task TrialAsync([Remainder] string trial)
            => ReplyAsync($"The essence of an empire must be shared equally amongst all of its citizens. *{Context.User.Mention} has found the {Context.Guild.Roles.FirstOrDefault(r => r.Name.ToLower().Contains(trial.ToLower())).Mention}*");

        [Command("Trials"), Remarks("Trials <action> <trial>"), Summary("Add or Delete a Trial of Ascendancy that you're looking for to be notified when someone has found it. Any part of the Trial Name or All for all Trials. Lists Trials you're looking for as well."), RequireChannel("lab-and-trials")]
        public Task TrialsAsync(CommandAction action, [Remainder] string trial = null)
        {
            switch (action)
            {
                case CommandAction.Add:
                    if (trial.ToLower() is "all")
                        (Context.User as SocketGuildUser).AddRolesAsync(Context.Guild.Roles.Where(r => r.Name.Contains("Trial of")));
                    else
                        (Context.User as SocketGuildUser).AddRoleAsync(Context.Guild.Roles.FirstOrDefault(r => r.Name.ToLower().Contains(trial.ToLower())));
                    return ReplyAsync($"Some things that slumber should never be awoken. *Trial{(trial.ToLower() is "all" ? "s were" : " was")} added to your list.* {Extras.OkHand}");

                case CommandAction.Delete:
                    if (trial.ToLower() is "all")
                        (Context.User as SocketGuildUser).RemoveRolesAsync(Context.Guild.Roles.Where(r => r.Name.Contains("Trial of")));
                    else
                        (Context.User as SocketGuildUser).RemoveRoleAsync(Context.Guild.Roles.FirstOrDefault(r => r.Name.ToLower().Contains(trial.ToLower())));
                    return ReplyAsync($"Woooooah, the weary traveller draws close to the end of the path.. *Trial{(trial.ToLower() is "all" ? "s were" : " was")} removed from your list.* {Extras.OkHand}");

                case CommandAction.List:
                    return ReplyAsync($"The Emperor beckons, and the world attends. *{String.Join(", ", (Context.User as SocketGuildUser).Roles.Where(r => r.Name.Contains("Trial of")).Select(r => r.Name))}*");

                default:
                    return ReplyAsync($"{Extras.Cross} Action is either `Add`, `Delete` or `List`.");
            }
        }

        [Command("Wiki"), Summary("Searches an item on the Path of Exile Wiki."), Remarks("Wiki <item>")]
        public async Task WikiAsync([Remainder] string item)
            => await ReplyAsync(embed: await WikiHelper.WikiGetItemAsync(item, Context.HttpClient).ConfigureAwait(false)).ConfigureAwait(false);
    }
}