﻿namespace PoE.Bot.Modules
{
    using Discord;
    using System.Linq;
    using PoE.Bot.Addons;
    using Discord.Commands;
    using System.Threading.Tasks;
    using PoE.Bot.Objects;
    using PoE.Bot.Addons.Preconditions;

    [Name("Shop Commands"), Group("Shop"), RequireChannel("shops"), Ratelimit]
    public class ShopModule : BotBase
    {
        [Command("Add"), Remarks("Adds the item to your shop."), Summary("Shop Add <League> <Item>")]
        public Task AddAsync(Leagues League, [Remainder] string Item)
        {
            Context.Server.Shops.Add(new ShopObject
            {
                UserId = Context.User.Id,
                League = League,
                Item = Item
            });

            return ReplyAsync($"Your humble servant thanks you, my God. *`{Item}` has been added to your shop.* {Extras.OkHand}", Save: 's');
        }

        [Command("Delete"), Remarks("Deletes the item from your shop. Item has to be an exact match to what you want to delete"), Summary("Shop Delete <League> <Item>")]
        public Task Delete(Leagues League, [Remainder] string Item)
        {
            if (!Context.Server.Shops.Where(s => s.Item == Item && s.League == League && s.UserId == Context.User.Id).Any())
                return ReplyAsync($"{Extras.Cross} I'm no beast of burden. *`{Item}` was not found in your shop.*");
            var Shop = Context.Server.Shops.FirstOrDefault(s => s.Item == Item && s.League == League && s.UserId == Context.User.Id);
            Context.Server.Shops.Remove(Shop);
            return ReplyAsync($"Your trust is the only reward I need, my Lord of light. *`{Item}` has been removed from your shop.* {Extras.OkHand}", Save: 's');
        }

        [Command("Search"), Remarks("Searches all shops for the item"), Summary("Shop Search <Item>")]
        public Task SearchAsync([Remainder] string Item)
        {
            var ShopItems = Context.Server.Shops.Where(x => x.Item.Contains(Item)).Select(x => $"League: **{x.League}**\n{x.Item}\nOwned By: {(Context.Guild.GetUserAsync(x.UserId).GetAwaiter().GetResult()).Mention}\n");
            if (!ShopItems.Any())
                return ReplyAsync($"{Extras.Cross} I'm no beast of burden. *`{Item}` was not found in any shop.*");
            return PagedReplyAsync(Context.GuildHelper.Pages(ShopItems), $"Search Results");
        }

        [Command("SearchLeague"), Remarks("Searches all shops for the item, in specified League"), Summary("Shop SearchLeague <League> <Item>")]
        public Task SearchLeagueAsync(Leagues League, [Remainder] string Item)
        {
            var ShopItems = Context.Server.Shops.Where(x => x.Item.Contains(Item) && x.League == League).Select(x => $"{x.Item}\nOwned By: {(Context.Guild.GetUserAsync(x.UserId).GetAwaiter().GetResult()).Mention}\n");
            if (!ShopItems.Any())
                return ReplyAsync($"{Extras.Cross} I'm no beast of burden. *`{Item}` was not found in any {League} shops.*");
            return PagedReplyAsync(Context.GuildHelper.Pages(ShopItems), $"League {League} Search Results");
        }

        [Command("User"), Remarks("Gets all shops for a user"), Summary("Shop User [@User]")]
        public Task UserAsync(IGuildUser User = null)
        {
            User = User ?? Context.User as IGuildUser;
            var ShopItems = Context.Server.Shops.Where(x => x.UserId == User.Id).Select(x => $"League: **{x.League}**\nItem:\n{x.Item}\n").ToArray();
            if (!Context.Server.Shops.Any() || !ShopItems.Any())
                return ReplyAsync($"{Extras.Cross} I'm no beast of burden. *`{User}` doesn't have any items in their shop.*");
            return PagedReplyAsync(Context.GuildHelper.Pages(ShopItems), $"{User.Username}'s Personal Shop");
        }
    }
}
