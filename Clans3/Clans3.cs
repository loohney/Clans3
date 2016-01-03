using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace Clans3
{
    [ApiVersion(1,22)]
    public class Clans3 : TerrariaPlugin
    {
        public override string Name { get { return "Clans3"; } }
        public override string Author { get { return "Zaicon"; } }
        public override string Description { get { return "Clan Plugin for TShock"; } }
        public override Version Version { get { return new Version(1, 0, 0, 0); } }

        
        public static List<Clan> clans;

        public Clans3(Main game)
            :base(game)
        {
            base.Order = 1;
        }

        #region Init/Dispose
        public override void Initialize()
        {
            clans = new List<Clan>();

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.ServerChat.Register(this, onChat);
           
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.ServerChat.Deregister(this, onChat);
            }
            base.Dispose(disposing);
        }
        #endregion

        #region Hooks
        private void OnInitialize(EventArgs args)
        {
            DB.DBConnect();
            DB.loadClans();

            Commands.ChatCommands.Add(new Command("clans.use", ClansMain, "clan"));
            Commands.ChatCommands.Add(new Command("clans.use", CChat, "c"));
            Commands.ChatCommands.Add(new Command("clans.reload", CReload, "clanreload"));
            Commands.ChatCommands.Add(new Command("clans.mod", ClansStaff, "clanstaff", "cs"));
        }

        private void onChat(ServerChatEventArgs args)
        {
            TSPlayer plr = TShock.Players[args.Who];

            if (plr == null || !plr.Active || args.Handled || !plr.IsLoggedIn || args.Text.StartsWith(TShock.Config.CommandSpecifier) || args.Text.StartsWith(TShock.Config.CommandSilentSpecifier) || findClan(plr.User.ID) == -1 || plr.mute)
            {
                return;
            }

            int clanindex = findClan(plr.User.ID);
            string prefix = clans[clanindex].prefix == "" ? plr.Group.Prefix : "(" + clans[clanindex].prefix + ") " + plr.Group.Prefix;

            TSPlayer.All.SendMessage(string.Format(TShock.Config.ChatFormat, plr.Group.Name, prefix, plr.Name, plr.Group.Suffix, args.Text), new Color(plr.Group.R, plr.Group.G, plr.Group.B));
            TSPlayer.Server.SendMessage(string.Format(TShock.Config.ChatFormat, plr.Group.Name, prefix, plr.Name, plr.Group.Suffix, args.Text), new Color(plr.Group.R, plr.Group.G, plr.Group.B));

            args.Handled = true;
        }
        #endregion

        #region Clan Cmds
        private void ClansMain(CommandArgs args)
        {
            int clanindex = findClan(args.Player.User.ID);
            #region clan help
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "help")
            {
                List<string> cmds = new List<string>();
                
                if (clanindex != -1 && clans[clanindex].owner == args.Player.User.ID)
                {
                    cmds.Add("prefix <chat prefix> - Add or change your clan's chat prefix.");
                    cmds.Add("promote <player name> - Make a clanmember a clan admin.");
                    cmds.Add("demote <player name> - Make a clan admin a regular clanmember.");
                }
                if (clanindex != -1 && (clans[clanindex].admins.Contains(args.Player.User.ID) || clans[clanindex].owner == args.Player.User.ID))
                {
                    cmds.Add("kick <player name> - Kick a clanmember from your clan.");
                    cmds.Add("ban <player name> - Prevents a player from joining your clan.");
                    cmds.Add("unban <player name> - Allows a banned player to join your clan.");
                }
                if (clanindex != -1 && (clans[clanindex].members.Contains(args.Player.User.ID) || clans[clanindex].admins.Contains(args.Player.User.ID) || clans[clanindex].owner == args.Player.User.ID))
                {
                    cmds.Add("invite <player name> - Sends a message to a player inviting them to join your clan.");
                    cmds.Add("members - Lists all clanmembers in your clan.");
                    cmds.Add("leave - Leaves your current clan.");
                }
                if (clanindex == -1)
                {
                    cmds.Add("create <clan name> - Creates a new clan.");
                    cmds.Add("join <clan name> - Joins an existing clan.");
                }

                cmds.Add("list - Lists all existing clans.");

                int pagenumber;

                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pagenumber))
                    return;

                PaginationTools.SendPage(args.Player, pagenumber, cmds, new PaginationTools.Settings
                    {
                        HeaderFormat = "Clan Sub-Commands ({0}/{1}):",
                        FooterFormat = "Type {0}clan help {{0}} for more sub-commands.".SFormat(args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier)
                    }
                );

            }
            #endregion
            #region clan leave
            else if (args.Parameters.Count == 1 && args.Parameters[0].ToLower() == "leave")
            {
                //If player is in a Clan
                if (clanindex != -1)
                {
                    //If player is owner of the clan, pass ownership if possible
                    if (clans[clanindex].owner == args.Player.User.ID)
                    {
                        if (clans[clanindex].admins.Count > 0)
                        {
                            clans[clanindex].owner = clans[clanindex].admins[0];
                            clans[clanindex].admins.RemoveAt(0);
                            DB.changeOwner(args.Player.User.ID, clans[clanindex]);
                            string newowner = TShock.Users.GetUserByID(clans[clanindex].owner).Name;
                            args.Player.SendSuccessMessage($"You have left your clan! Ownership of the clan has now passed to {newowner}");
                            var list = TShock.Utils.FindPlayer(newowner);
                            if (list.Count == 1 && list[0].User.ID == clans[clanindex].owner)
                                list[0].SendInfoMessage($"You are now owner of the {clans[clanindex].name} clan!");
                            TShock.Log.Info($"{args.Player.User.Name} left the {clans[clanindex].name} clan.");
                            TShock.Log.Info($"{newowner} is now the owner of the {clans[clanindex].name} clan.");
                        }
                        else if (clans[clanindex].members.Count > 0)
                        {
                            clans[clanindex].owner = clans[clanindex].members[0];
                            clans[clanindex].members.RemoveAt(0);
                            DB.changeOwner(args.Player.User.ID, clans[clanindex]);
                            string newowner = TShock.Users.GetUserByID(clans[clanindex].owner).Name;
                            args.Player.SendSuccessMessage($"You have left your clan! Ownership of the clan has now passed to {newowner}");
                            var list = TShock.Utils.FindPlayer(newowner);
                            if (list.Count == 1 && list[0].User.ID == clans[clanindex].owner)
                                list[0].SendInfoMessage($"You are now owner of the {clans[clanindex].name} clan!");
                            TShock.Log.Info($"{args.Player.User.Name} left the {clans[clanindex].name} clan.");
                            TShock.Log.Info($"{newowner} is now the owner of the {clans[clanindex].name} clan.");
                        }
                        else
                        {
                            DB.removeClan(clans[clanindex].owner);
                            TShock.Log.Info($"{args.Player.User.Name} left the {clans[clanindex].name} clan.");
                            TShock.Log.Info($"The {clans[clanindex].name} clan has been deleted.");
                            clans.RemoveAt(clanindex);
                            args.Player.SendSuccessMessage("You have left your clan! There are no members in it, so it has been removed.");
                        }
                    }
                    //If player is not owner of the clan
                    else
                    {
                        //If player is admin
                        if (clans[clanindex].admins.Contains(args.Player.User.ID))
                        {
                            clans[clanindex].admins.Remove(args.Player.User.ID);
                            DB.changeMembers(clans[clanindex].owner, clans[clanindex]);
                            args.Player.SendSuccessMessage("You have left your clan.");
                        }
                        //If player is not admin
                        else
                        {
                            clans[clanindex].members.Remove(args.Player.User.ID);
                            DB.changeMembers(clans[clanindex].owner, clans[clanindex]);
                            args.Player.SendSuccessMessage("You have left your clan.");
                        }
                        TShock.Log.Info($"{args.Player.User.Name} left the {clans[clanindex].name} clan.");
                    }
                }
                //If player is not in a clan
                else
                {
                    args.Player.SendErrorMessage("You are not in a clan!");
                }
            }
            #endregion
            #region clan list
            else if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "list")
            {
                int pagenumber;
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pagenumber))
                    return;

                SortedDictionary<int, string> cdict = new SortedDictionary<int, string>();

                foreach(Clan clan in clans)
                {
                    int count = 1 + clan.admins.Count + clan.members.Count;
                    if (!clan.banned.Contains(args.Player.User.ID))
                        cdict.Add(count, clan.name);
                }
                
                PaginationTools.SendPage(args.Player, pagenumber, cdict.Values.ToList().Reverse<string>().ToList(), new PaginationTools.Settings
                {
                    HeaderFormat = "List of Clans ({0}/{1}):",
                    FooterFormat = "Type {0}clan list {{0}} for more clans.".SFormat(args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier)
                });
            }
            #endregion
            #region clan members
            else if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "members")
            {
                if (clanindex == -1)
                {
                    args.Player.SendErrorMessage("You are not in a Clan!");
                }
                else
                {
                    int pagenumber;
                    if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pagenumber))
                        return;

                    List<string> members = new List<string>();

                    members.Add(TShock.Users.GetUserByID(clans[clanindex].owner).Name + " (Owner)");

                    foreach(int userid in clans[clanindex].admins)
                        members.Add(TShock.Users.GetUserByID(userid).Name + " (Admin)");
                    foreach(int userid in clans[clanindex].members)
                        members.Add(TShock.Users.GetUserByID(userid).Name);

                    PaginationTools.SendPage(args.Player, pagenumber, members,
                        new PaginationTools.Settings
                        {
                            HeaderFormat = clans[clanindex].name + " Clan Members ({0}/{1}):",
                            FooterFormat = "Type {0}clan members {{0}} for more clan members.".SFormat(args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier)
                        }
                    );
                }
            }
            #endregion
            else if (args.Parameters.Count > 1)
            {
                string type = args.Parameters[0].ToLower();
                
                var tempparams = args.Parameters;
                tempparams.RemoveAt(0);

                string input = string.Join(" ", tempparams);
                //Clan Create
                #region clan create
                if (type == "create")
                {
                    if (clanindex != -1)
                    {
                        args.Player.SendErrorMessage("You cannot create a clan while you are in one!");
                        return;
                    }
                    List<int> clanlist = findClanByName(input);
                    if (clanlist.Count > 0)
                    {
                        foreach (int index in clanlist)
                        {
                            if (clans[index].name == input)
                            {
                                args.Player.SendErrorMessage("A clan with this name has already been created!");
                                return;
                            }
                        }
                    }
                    if (input.Contains("[c/") || input.Contains("[i"))
                    {
                        args.Player.SendErrorMessage("You cannot use item/color tags in clan names!");
                        return;
                    }
                    clans.Add(new Clan(input, args.Player.User.ID));
                    DB.newClan(input, args.Player.User.ID);
                    args.Player.SendSuccessMessage($"You have created the {input} clan! Use /clan prefix <prefix> to set the chat prefix.");
                    TShock.Log.Info($"{args.Player.User.Name} created the {input} clan.");
                    return;
                }
                #endregion
                #region clan prefix
                //Clan Prefix
                if (type == "prefix")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    else if (clans[clanindex].owner != args.Player.User.ID)
                    {
                        args.Player.SendErrorMessage("Only the clan's creator can change its chat prefix.");
                        return;
                    }

                    else if (input.Contains("[c/") || input.Contains("[i"))
                    {
                        args.Player.SendErrorMessage("You cannot use item/color tags in clan prefixes!");
                        return;
                    }
                    else if (input.Length > 20)
                    {
                        args.Player.SendErrorMessage("Prefix length too long!");
                        return;
                    }

                    clans[clanindex].prefix = input;
                    DB.clanPrefix(args.Player.User.ID, input);
                    args.Player.SendSuccessMessage($"Successfully changed the clan prefix to \"{input}\"!");
                    TShock.Log.Info($"{args.Player.User.Name} changed the {clans[clanindex].name} clan's prefix to \"{input}\".");
                    return;
                }
                #endregion
                #region clan invite
                //Clan Invite
                if (type == "invite")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }

                    var list = TShock.Utils.FindPlayer(input);

                    if (list.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No players found by the name {input}.");
                        return;
                    }
                    else if (list.Count > 1)
                    {
                        TShock.Utils.SendMultipleMatchError(args.Player, list.Select(p => p.Name));
                        return;
                    }

                    int index = findClan(list[0].User.ID);
                    if (index == clanindex)
                    {
                        args.Player.SendErrorMessage("This player is already part of your clan!");
                        return;
                    }
                    if (index != -1)
                    {
                        args.Player.SendErrorMessage("This player is already in a clan!");
                        return;
                    }
                    if (clans[clanindex].banned.Contains(list[0].User.ID))
                    {
                        args.Player.SendErrorMessage("This player is banned from your clan and cannot be invited.");
                        return;
                    }

                    args.Player.SendSuccessMessage($"{list[0].Name} has been invited to join the {clans[clanindex].name} clan!");
                    list[0].SendInfoMessage($"{args.Player.Name} has invited you to join the {clans[clanindex].name} clan! Use /clan join \"{clans[clanindex].name}\" to join the Clan!");
                    return;
                }
                #endregion
                #region clan join
                //Clan Join
                if (type == "join")
                {
                    if (clanindex != -1)
                    {
                        args.Player.SendErrorMessage("You cannot join multiple clans!");
                        return;
                    }

                    List<int> clanindexlist = findClanByName(input);

                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name {input}.");
                        return;
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> names = new List<string>();
                        foreach (int num in clanindexlist)
                        {
                            names.Add(clans[num].name);
                        }
                        args.Player.SendErrorMessage($"Multiple matches found: {string.Join(", ", names)}");
                        return;
                    }
                    clanindex = clanindexlist[0];
                    if (clans[clanindex].banned.Contains(args.Player.User.ID))
                    {
                        args.Player.SendErrorMessage("You have been banned from this clan!");
                        return;
                    }
                    clans[clanindexlist[0]].members.Add(args.Player.User.ID);
                    foreach (TSPlayer plr in TShock.Players)
                    {
                        if (plr != null && plr.Active && plr.IsLoggedIn && plr.Index != args.Player.Index)
                        {
                            int index = findClan(plr.User.ID);
                            if (index == clanindexlist[0])
                                plr.SendInfoMessage($"{args.Player.Name} just joined your clan!");
                        }
                    }
                    DB.changeMembers(clans[clanindexlist[0]].owner, clans[clanindex]);
                    TShock.Log.Info($"{args.Player.User.Name} joined the {clans[clanindexlist[0]].name} clan.");
                    args.Player.SendSuccessMessage($"You have joined the {clans[clanindexlist[0]].name} clan!");
                    return;
                }
                #endregion
                #region clan kick
                //Clan Kick
                if (type == "kick")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (!clans[clanindex].admins.Contains(args.Player.User.ID) && clans[clanindex].owner != args.Player.User.ID)
                    {
                        args.Player.SendErrorMessage("You cannot kick players out of your clan!");
                        return;
                    }
                    var list = TShock.Utils.FindPlayer(input);
                    if (list.Count == 0)
                    {
                        var plr2 = TShock.Users.GetUserByName(input);
                        if (plr2 != null)
                        {
                            int index = findClan(plr2.ID);
                            if (index == -1 || index != clanindex)
                            {
                                args.Player.SendErrorMessage($"{plr2.Name} is not a member of your clan!");
                                return;
                            }
                            else
                            {
                                if (clans[clanindex].owner == plr2.ID)
                                {
                                    args.Player.SendErrorMessage("You cannot kick the owner of your clan!");
                                    return;
                                }
                                else if (clans[clanindex].admins.Contains(plr2.ID))
                                {
                                    args.Player.SendErrorMessage("You cannot kick an admin of your clan!");
                                    return;
                                }
                                clans[clanindex].members.Remove(plr2.ID);
                                DB.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                args.Player.SendSuccessMessage($"You have removed {plr2.Name} from your clan!");
                                TShock.Log.Info($"{args.Player.User.Name} removed {plr2.Name} from the {clans[clanindex].name} clan.");
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage($"No player found by the name {input}.");
                            return;
                        }
                        return;
                    }
                    else if (list.Count > 1)
                    {
                        TShock.Utils.SendMultipleMatchError(args.Player, list.Select(p => p.Name));
                        return;
                    }

                    TSPlayer plr = list[0];

                    if (clans[clanindex].owner == plr.User.ID)
                    {
                        args.Player.SendErrorMessage("You cannot kick the owner of your clan!");
                        return;
                    }
                    else if (clans[clanindex].admins.Contains(plr.User.ID))
                    {
                        args.Player.SendErrorMessage("You cannot kick an admin of your clan!");
                        return;
                    }
                    clans[clanindex].members.Remove(plr.User.ID);
                    DB.changeMembers(clans[clanindex].owner, clans[clanindex]);
                    args.Player.SendSuccessMessage($"You have removed {plr.Name} from your clan!");
                    plr.SendInfoMessage($"You have been kicked out of {clans[clanindex].name} by {args.Player.Name}!");
                    TShock.Log.Info($"{args.Player.User.Name} removed {plr.Name} from the {clans[clanindex].name} clan.");
                    return;
                }
                #endregion
                #region clan un/ban
                //Clan Ban
                if (type == "ban")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (!clans[clanindex].admins.Contains(args.Player.User.ID) && clans[clanindex].owner != args.Player.User.ID)
                    {
                        args.Player.SendErrorMessage("You cannot ban players from your clan!");
                        return;
                    }
                    var list = TShock.Utils.FindPlayer(input);
                    if (list.Count == 0)
                    {
                        var plr2 = TShock.Users.GetUserByName(input);
                        if (plr2 != null)
                        {
                            int index = findClan(plr2.ID);
                            if (index == -1 || index != clanindex)
                            {
                                args.Player.SendErrorMessage($"{plr2.Name} is not a member of your clan!");
                                return;
                            }
                            else
                            {
                                if (clans[clanindex].owner == plr2.ID)
                                {
                                    args.Player.SendErrorMessage("You cannot ban the owner of your clan!");
                                    return;
                                }
                                else if (clans[clanindex].admins.Contains(plr2.ID))
                                {
                                    args.Player.SendErrorMessage("You cannot ban an admin of your clan!");
                                    return;
                                }
                                clans[clanindex].members.Remove(plr2.ID);
                                clans[clanindex].banned.Add(plr2.ID);
                                DB.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                DB.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                                args.Player.SendSuccessMessage($"You have banned {plr2.Name} from your clan!");
                                TShock.Log.Info($"{args.Player.User.Name} banned {plr2.Name} from the {clans[clanindex].name} clan.");
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage($"No player found by the name {input}.");
                            return;
                        }
                        return;
                    }
                    else if (list.Count > 1)
                    {
                        TShock.Utils.SendMultipleMatchError(args.Player, list.Select(p => p.Name));
                        return;
                    }

                    TSPlayer plr = list[0];

                    if (clans[clanindex].owner == plr.User.ID)
                    {
                        args.Player.SendErrorMessage("You cannot ban the owner of your clan!");
                        return;
                    }
                    else if (clans[clanindex].admins.Contains(plr.User.ID))
                    {
                        args.Player.SendErrorMessage("You cannot ban an admin of your clan!");
                        return;
                    }
                    clans[clanindex].members.Remove(plr.User.ID);
                    clans[clanindex].banned.Add(plr.User.ID);
                    DB.changeMembers(clans[clanindex].owner, clans[clanindex]);
                    DB.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                    args.Player.SendSuccessMessage($"You have banned {plr.Name} from your clan!");
                    plr.SendInfoMessage($"You have been banned from {clans[clanindex].name} by {args.Player.Name}!");
                    TShock.Log.Info($"{args.Player.User.Name} banned {plr.Name} from the {clans[clanindex].name} clan.");
                    return;
                }
                //Clan Unban
                if (type == "unban")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (clans[clanindex].owner != args.Player.User.ID && !clans[clanindex].admins.Contains(args.Player.User.ID))
                    {
                        args.Player.SendErrorMessage("You cannot unban players from your clan!");
                        return;
                    }
                    var list = TShock.Utils.FindPlayer(input);
                    if (list.Count == 0)
                    {
                        var plr2 = TShock.Users.GetUserByName(input);
                        if (plr2 != null)
                        {
                            if (!clans[clanindex].banned.Contains(plr2.ID))
                            {
                                args.Player.SendErrorMessage($"{plr2.Name} is not banned from your clan!");
                            }
                            else
                            {
                                clans[clanindex].banned.Remove(plr2.ID);
                                DB.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                                args.Player.SendSuccessMessage($"You have unbanned {plr2.Name} from your clan!");
                                TShock.Log.Info($"{args.Player.User.Name} unbanned {plr2.Name} from the {clans[clanindex].name} clan.");
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage($"No player found by the name {input}.");
                        }
                        return;
                    }
                    else if (list.Count > 1)
                    {
                        TShock.Utils.SendMultipleMatchError(args.Player, list.Select(p => p.Name));
                        return;
                    }

                    TSPlayer plr = list[0];

                    if (!clans[clanindex].banned.Contains(plr.User.ID))
                    {
                        args.Player.SendErrorMessage($"{plr.Name} is not banned from your clan!");
                        return;
                    }

                    clans[clanindex].banned.Remove(plr.User.ID);
                    DB.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                    args.Player.SendSuccessMessage($"You have unbanned {plr.Name} from your clan!");
                    plr.SendInfoMessage($"You have been unbanned from {clans[clanindex].name} by {args.Player.Name}!");
                    TShock.Log.Info($"{args.Player.User.Name} unbanned {plr.Name} from the {clans[clanindex].name} clan.");
                    return;
                }
                #endregion
                #region clan promote/demote
                //Clan Promote
                if (type == "promote")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (clans[clanindex].owner != args.Player.User.ID)
                    {
                        args.Player.SendErrorMessage("You cannot promote clan members to admin in this clan!");
                        return;
                    }
                    var list = TShock.Utils.FindPlayer(input);
                    if (list.Count == 0)
                    {
                        var plr = TShock.Users.GetUserByName(input);
                        if (plr == null)
                        {
                            args.Player.SendErrorMessage($"No player found by the name {input}");
                            return;
                        }
                        if (clans[clanindex].admins.Contains(plr.ID))
                        {
                            args.Player.SendErrorMessage($"{plr.Name} is already an admin in this clan!");
                            return;
                        }
                        if (!clans[clanindex].members.Contains(plr.ID))
                        {
                            args.Player.SendErrorMessage($"{plr.Name} is not a member of your clan!");
                            return;
                        }
                        clans[clanindex].admins.Add(plr.ID);
                        clans[clanindex].members.Remove(plr.ID);
                        DB.changeMembers(args.Player.User.ID, clans[clanindex]);
                        args.Player.SendSuccessMessage($"{plr.Name} is now an admin of your clan!");
                        TShock.Log.Info($"{args.Player.User.Name} made {plr.Name} an admin of the {clans[clanindex].name} clan.");
                        return;
                    }
                    if (list.Count > 1)
                    {
                        TShock.Utils.SendMultipleMatchError(args.Player, list.Select(p => p.Name));
                        return;
                    }
                    if (clans[clanindex].admins.Contains(list[0].User.ID))
                    {
                        args.Player.SendErrorMessage($"{list[0].Name} is already an admin in this clan!");
                        return;
                    }
                    if (!clans[clanindex].members.Contains(list[0].User.ID))
                    {
                        args.Player.SendErrorMessage($"{list[0].Name} is not a member of your clan!");
                        return;
                    }
                    clans[clanindex].admins.Add(list[0].User.ID);
                    clans[clanindex].members.Remove(list[0].User.ID);
                    DB.changeMembers(args.Player.User.ID, clans[clanindex]);
                    args.Player.SendSuccessMessage($"{list[0].Name} is now an admin of your clan!");
                    TShock.Log.Info($"{args.Player.User.Name} made {list[0].User.Name} an admin of the {clans[clanindex].name} clan.");
                    list[0].SendInfoMessage($"You are now an admin of the {clans[clanindex].name} clan by {args.Player.Name}.");
                    return;
                }
                //Clan Demote
                if (type == "demote")
                {
                    if (clanindex == -1)
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (clans[clanindex].owner != args.Player.User.ID)
                    {
                        args.Player.SendErrorMessage("You cannot demote clan members in this clan!");
                        return;
                    }
                    var list = TShock.Utils.FindPlayer(input);
                    if (list.Count == 0)
                    {
                        var plr = TShock.Users.GetUserByName(input);
                        if (plr == null)
                        {
                            args.Player.SendErrorMessage($"No player found by the name {input}");
                            return;
                        }
                        if (!clans[clanindex].admins.Contains(plr.ID))
                        {
                            args.Player.SendErrorMessage($"{plr.Name} is not an admin in this clan!");
                            return;
                        }
                        clans[clanindex].admins.Remove(plr.ID);
                        clans[clanindex].members.Add(plr.ID);
                        DB.changeMembers(args.Player.User.ID, clans[clanindex]);
                        args.Player.SendSuccessMessage($"{plr.Name} is no longer an admin of your clan!");
                        TShock.Log.Info($"{args.Player.User.Name} demoted {plr.Name} from admin in the {clans[clanindex].name} clan.");
                        return;
                    }
                    if (list.Count > 1)
                    {
                        TShock.Utils.SendMultipleMatchError(args.Player, list.Select(p => p.Name));
                        return;
                    }
                    if (!clans[clanindex].admins.Contains(list[0].User.ID))
                    {
                        args.Player.SendErrorMessage($"{list[0].Name} is not an admin in this clan!");
                        return;
                    }
                    clans[clanindex].admins.Remove(list[0].User.ID);
                    clans[clanindex].members.Add(list[0].User.ID);
                    DB.changeMembers(args.Player.User.ID, clans[clanindex]);
                    args.Player.SendSuccessMessage($"{list[0].Name} is no longer an admin of your clan!");
                    TShock.Log.Info($"{args.Player.User.Name} demoted {list[0].User.Name} from admin in the {clans[clanindex].name} clan.");
                    list[0].SendInfoMessage($"You have been demoted from the {clans[clanindex]} by {args.Player.Name}.");
                    return;
                }
                #endregion
                args.Player.SendErrorMessage("Invalid syntax. Use /clan help for help.");
            }
            else
            {
                args.Player.SendErrorMessage("Invalid syntax. Use /clan help for help.");
            }
            
        }

        private void ClansStaff(CommandArgs args)
        {
            //cs <option> <clan name> [params]

            //2+
            //cs members <clan name>
            //3
            //cs kick <clan> <player>
            //3
            //cs ban <clan> <player>
            //3
            //cs unban <clan> <player>
            //3+
            //cs prefix <clan> <prefix+>
            //2+
            //cs delete <clan+> -- admins+?

            #region clan help
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "help")
            {
                int pagenumber;

                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pagenumber))
                    return;

                List<string> cmds = new List<string>();

                cmds.Add("members <clan name> [page #] - List all of the members in the specified clan.");
                cmds.Add("prefix <clan name> <prefix> - Changes the specified clan's chat prefix.");
                cmds.Add("kick <clan name> <player name> - Kicks the specified player from the specified clan.");
                cmds.Add("ban <clan name> <player name> - Bans the specified player from the specified clan.");
                cmds.Add("unban <clan name> <player name> - Unbans the specified player from the specified clan.");
                if (args.Player.Group.HasPermission("clans.admin"))
                    cmds.Add("delete <clan name> - Deletes the specified clan. Do not use without good reason.");

                PaginationTools.SendPage(args.Player, pagenumber, cmds,
                    new PaginationTools.Settings
                    {
                        HeaderFormat = "ClanStaff Sub-Commands ({0}/{1}):",
                        FooterFormat = "Type {0}cs help {{0}} for more sub-commands.".SFormat(args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier)
                    }
                );
            }
            #endregion
            
            else if (args.Parameters.Count > 1)
            {
                string type = args.Parameters[0].ToLower();
                string clan = args.Parameters[1];

                #region cs members
                //cs members <clan name> [page #]
                if (type == "members")
                {
                    var clanindexlist = findClanByName(clan);
                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name \"{clan}\".");
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> matches = new List<string>();

                        foreach (int index in clanindexlist)
                            matches.Add(clans[index].name);

                        TShock.Utils.SendMultipleMatchError(args.Player, matches);
                    }
                    else
                    {
                        int clanindex = clanindexlist[0];

                        int pagenumber;
                        if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pagenumber))
                            return;

                        List<string> members = new List<string>();

                        members.Add(TShock.Users.GetUserByID(clans[clanindex].owner).Name + " (Owner)");

                        foreach (int userid in clans[clanindex].admins)
                            members.Add(TShock.Users.GetUserByID(userid).Name + " (Admin)");
                        foreach (int userid in clans[clanindex].members)
                            members.Add(TShock.Users.GetUserByID(userid).Name);

                        PaginationTools.SendPage(args.Player, pagenumber, members,
                            new PaginationTools.Settings
                            {
                                HeaderFormat = clans[clanindex].name + " Clan Members ({0}/{1}):",
                                FooterFormat = "Type {0}cs members {1} {{0}} for more clan members.".SFormat(args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier, clans[clanindex].name)
                            }
                        );
                    }
                }
                #endregion
                #region cs kick
                //cs kick <clan> <player>
                else if (type == "kick" && args.Parameters.Count == 3)
                {
                    var clanindexlist = findClanByName(clan);
                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name \"{clan}\".");
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> matches = new List<string>();

                        foreach (int index in clanindexlist)
                            matches.Add(clans[index].name);

                        TShock.Utils.SendMultipleMatchError(args.Player, matches);
                    }
                    else
                    {
                        string name = args.Parameters[2];
                        var plr = TShock.Users.GetUserByName(name);

                        if (plr == null)
                        {
                            args.Player.SendErrorMessage("No players found by the name \"{name}\"");
                        }
                        else
                        {
                            int clanindex = clanindexlist[0];
                            if (plr.ID == clans[clanindex].owner)
                            {
                                args.Player.SendErrorMessage("You cannot kick the owner of the clan!");
                            }
                            else if (clans[clanindex].admins.Contains(plr.ID))
                            {
                                clans[clanindex].admins.Remove(plr.ID);
                                DB.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                args.Player.SendSuccessMessage($"Successfully kicked {plr.Name} from the {clans[clanindex].name} clan.");
                                TShock.Log.Info($"{args.Player.User.Name} kicked {plr.Name} from the {clans[clanindex].name} clan.");
                            }
                            else if (clans[clanindex].members.Contains(plr.ID))
                            {
                                clans[clanindex].members.Remove(plr.ID);
                                DB.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                args.Player.SendSuccessMessage($"Successfully kicked {plr.Name} from the {clans[clanindex].name} clan.");
                                TShock.Log.Info($"{args.Player.User.Name} kicked {plr.Name} from the {clans[clanindex].name} clan.");
                            }
                            else
                            {
                                args.Player.SendErrorMessage($"{plr.Name} is not in the {clans[clanindex].name} clan!");
                            } //end if (plr.ID == clans[clanindex].owner)
                        } //end if (plr == null)
                    } //end if (clanindexlist.Count == 0)
                } //end if (type == "kick" && args.Parameters.Count == 3)
                #endregion
                #region cs ban
                //cs ban <clan> <player>
                else if (type == "ban" && args.Parameters.Count == 3)
                {
                    var clanindexlist = findClanByName(clan);
                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name \"{clan}\".");
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> matches = new List<string>();

                        foreach (int index in clanindexlist)
                            matches.Add(clans[index].name);

                        TShock.Utils.SendMultipleMatchError(args.Player, matches);
                    }
                    else
                    {
                        string name = args.Parameters[2];
                        var plr = TShock.Users.GetUserByName(name);

                        if (plr == null)
                        {
                            args.Player.SendErrorMessage("No players found by the name \"{name}\"");
                        }
                        else
                        {
                            int clanindex = clanindexlist[0];
                            if (plr.ID == clans[clanindex].owner)
                            {
                                args.Player.SendErrorMessage("You cannot ban the owner of the clan!");
                            }
                            else if (clans[clanindex].admins.Contains(plr.ID))
                            {
                                clans[clanindex].admins.Remove(plr.ID);
                                clans[clanindex].banned.Add(plr.ID);
                                DB.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                DB.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                                args.Player.SendSuccessMessage($"Successfully banned {plr.Name} from the {clans[clanindex].name} clan.");
                                TShock.Log.Info($"{args.Player.User.Name} banned {plr.Name} from the {clans[clanindex].name} clan.");
                            }
                            else if (clans[clanindex].members.Contains(plr.ID))
                            {
                                clans[clanindex].members.Remove(plr.ID);
                                clans[clanindex].banned.Add(plr.ID);
                                DB.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                DB.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                                args.Player.SendSuccessMessage($"Successfully banned {plr.Name} from the {clans[clanindex].name} clan.");
                                TShock.Log.Info($"{args.Player.User.Name} banned {plr.Name} from the {clans[clanindex].name} clan.");
                            }
                            else
                            {
                                args.Player.SendErrorMessage($"{plr.Name} is not in the {clans[clanindex].name} clan!");
                            } //end if (plr.ID == clans[clanindex].owner)
                        } //end if (plr == null)
                    } //end if (clanindexlist.Count == 0)
                } //end if (type == "ban" && args.Parameters.Count == 3)
                
                else if (type == "unban" && args.Parameters.Count == 3)
                {
                    var clanindexlist = findClanByName(clan);
                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name \"{clan}\".");
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> matches = new List<string>();

                        foreach (int index in clanindexlist)
                            matches.Add(clans[index].name);

                        TShock.Utils.SendMultipleMatchError(args.Player, matches);
                    }
                    else
                    {
                        string name = args.Parameters[2];
                        var plr = TShock.Users.GetUserByName(name);

                        if (plr == null)
                        {
                            args.Player.SendErrorMessage("No players found by the name \"{name}\"");
                        }
                        else
                        {
                            int clanindex = clanindexlist[0];
                            if (!clans[clanindex].banned.Contains(plr.ID))
                            {
                                args.Player.SendErrorMessage($"{name} is not banned from the {clans[clanindex].name} clan!");
                            }
                            else
                            {
                                clans[clanindex].banned.Remove(plr.ID);
                                clans[clanindex].members.Remove(plr.ID);
                                DB.changeMembers(clans[clanindex].owner, clans[clanindex]);
                                DB.changeBanned(clans[clanindex].owner, clans[clanindex].banned);
                                args.Player.SendSuccessMessage($"Successfully unbanned {plr.Name} from the {clans[clanindex].name} clan.");
                                TShock.Log.Info($"{args.Player.User.Name} unbanned {plr.Name} from the {clans[clanindex].name} clan.");
                            } //end if (!clans[clanindex].banned.Contains(plr.ID))
                        } //end if (plr == null)
                    } //end if (clanindexlist.Count == 0)
                } //end if (type == "unban" && args.Parameters.Count == 3)
                #endregion
                #region cs prefix
                else if (type == "prefix" && args.Parameters.Count == 3)
                {
                    var clanindexlist = findClanByName(clan);
                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name \"{clan}\".");
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> matches = new List<string>();

                        foreach (int index in clanindexlist)
                            matches.Add(clans[index].name);

                        TShock.Utils.SendMultipleMatchError(args.Player, matches);
                    }
                    else
                    {
                        string prefix = args.Parameters[2];

                        if (prefix.Contains("[i") || prefix.Contains("[c"))
                        {
                            args.Player.SendErrorMessage("You cannot add item/color tags in clan prefixes!");
                        }
                        else if (prefix.Length > 20)
                        {
                            args.Player.SendErrorMessage("Prefix length too long!");
                        }
                        else
                        {
                            clans[clanindexlist[0]].prefix = prefix;
                            DB.clanPrefix(clans[clanindexlist[0]].owner, prefix);
                            args.Player.SendSuccessMessage($"Successfully changed the clan prefix of the {clans[clanindexlist[0]].name} clan to \"{prefix}\".");
                            TShock.Log.Info($"{args.Player.User.Name} changed the {clans[clanindexlist[0]].name} clan prefix to \"{prefix}\".");
                        } //end if (prefix.Contains("[i") || prefix.Contains("[c"))
                    } //end if (clanindexlist.Count == 0)
                } //end if (type == "prefix" && args.Parameters.Count == 3)
                #endregion
                #region cs delete
                else if (type == "delete" && args.Player.Group.HasPermission("clans.admin"))
                {
                    var clanindexlist = findClanByName(clan);
                    if (clanindexlist.Count == 0)
                    {
                        args.Player.SendErrorMessage($"No clans found by the name \"{clan}\".");
                    }
                    else if (clanindexlist.Count > 1)
                    {
                        List<string> matches = new List<string>();

                        foreach (int index in clanindexlist)
                            matches.Add(clans[index].name);

                        TShock.Utils.SendMultipleMatchError(args.Player, matches);
                    }
                    else
                    {
                        args.Player.SendSuccessMessage($"Successfully removed the {clans[clanindexlist[0]].name} clan.");
                        TShock.Log.Info($"{args.Player.User.Name} removed the {clans[clanindexlist[0]].name} clan.");
                        DB.removeClan(clans[clanindexlist[0]].owner);
                        clans.Remove(clans[clanindexlist[0]]);
                    }
                }
                #endregion
                else
                {
                    args.Player.SendErrorMessage("Unknown sub-command. Use /cs help for a list of valid sub-commands.");
                }
            }
            else
            {
                args.Player.SendErrorMessage("Unknown sub-command. Use /cs help for a list of valid sub-commands.");
            }
        }

        private void CChat(CommandArgs args)
        {
            int chatindex = findClan(args.Player.User.ID);
            if (chatindex == -1)
            {
                args.Player.SendErrorMessage("You are not in a clan!");
                return;
            }
            else if (args.Player.mute)
            {
                args.Player.SendErrorMessage("You are muted.");
                return;
            }
            foreach(TSPlayer plr in TShock.Players)
            {
                if (plr != null && plr.Active && plr.IsLoggedIn && findClan(plr.User.ID) == chatindex)
                    plr.SendMessage($"[Clanchat] {string.Join(" ", args.Parameters)}", Color.ForestGreen);
            }
        }
        #endregion

        #region Support
        private void CReload(CommandArgs args)
        {
            DB.loadClans();
            args.Player.SendSuccessMessage("Clans have been reloaded from the database.");
            TShock.Log.Info($"{args.Player.User.Name} reloaded Clans database.");
        }

        private int findClan(int userid)
        {
            if (userid == -1)
                return -1;

            for (int i = 0; i < clans.Count; i++)
            {
                if (clans[i].owner == userid || clans[i].admins.Contains(userid) || clans[i].members.Contains(userid))
                    return i;
            }

            return -1;
        }

        private List<int> findClanByName(string name)
        {
            List<int> clanslist = new List<int>();

            for (int i = 0; i < clans.Count; i++)
            {
                if (clans[i].name.Contains(name))
                    clanslist.Add(i);
            }

            return clanslist;
        }
        #endregion
    }
}
