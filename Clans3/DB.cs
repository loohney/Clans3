using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Data;
using System.IO;
using TShockAPI;
using TShockAPI.DB;

namespace Clans3
{
    public static class DB
    {
        public static IDbConnection db;

        public static void DBConnect()
        {
            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] dbHost = TShock.Config.MySqlHost.Split(':');
                    db = new MySqlConnection()
                    {
                        ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                            dbHost[0],
                            dbHost.Length == 1 ? "3306" : dbHost[1],
                            TShock.Config.MySqlDbName,
                            TShock.Config.MySqlUsername,
                            TShock.Config.MySqlPassword)

                    };
                    break;

                case "sqlite":
                    string sql = Path.Combine(TShock.SavePath, "Clans.sqlite");
                    db = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
                    break;

            }

            SqlTableCreator sqlcreator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            sqlcreator.EnsureTableStructure(new SqlTable("Clans",
                new SqlColumn("owner", MySqlDbType.Int32) { Primary = true, Unique = true, Length = 7 },
                new SqlColumn("name", MySqlDbType.Text) { Length = 30 },
                new SqlColumn("admins", MySqlDbType.Text) { Length = 100 },
                new SqlColumn("members", MySqlDbType.Text) { Length = 100 },
                new SqlColumn("prefix", MySqlDbType.Text) { Length = 30 },
                new SqlColumn("banned", MySqlDbType.Text) { Length = 100 },
                new SqlColumn("priv", MySqlDbType.Int32) { Length = 1 }));
        }

        public static void loadClans()
        {
            Clans3.clans.Clear();

            using (QueryResult reader = db.QueryReader(@"SELECT * FROM Clans"))
            {
                while (reader.Read())
                {
                    string adminstr = reader.Get<string>("admins");
                    List<int> adminlist = new List<int>();
                    if (adminstr != "")
                    {
                        adminstr = adminstr.Trim(',');
                        string[] adminsplit = adminstr.Split(',');
                        foreach (string str in adminsplit)
                            adminlist.Add(int.Parse(str));
                    }

                    string memberstr = reader.Get<string>("members");
                    List<int> memberlist = new List<int>();
                    if (memberstr != "")
                    {
                        memberstr = memberstr.Trim(',');
                        string[] membersplit = memberstr.Split(',');
                        foreach (string str in membersplit)
                            memberlist.Add(int.Parse(str));
                    }

                    string banstr = reader.Get<string>("banned");
                    List<int> banlist = new List<int>();
                    if (banstr != "")
                    {
                        banstr = banstr.Trim(',');
                        string[] bansplit = banstr.Split(',');
                        foreach (string str in bansplit)
                            banlist.Add(int.Parse(str));
                    }

                    bool ispriv = reader.Get<int>("priv") == 1 ? true : false;

                    Clans3.clans.Add(new Clan(reader.Get<string>("name"), reader.Get<int>("owner"))
                    {
                        admins = adminlist,
                        banned = banlist,
                        members = memberlist,
                        prefix = reader.Get<string>("prefix"),
                        cprivate = ispriv,
                        invited = new List<int>()
                    });
                }
            }
        }

        public static void removeClan(int owner)
        {
            int result = db.Query("DELETE FROM Clans WHERE owner=@0", owner);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to delete from Clans where owner = {owner}.");
        }

        public static void changeOwner(int oldowner, Clan newclan)
        {
            string admins = ",";
            string members = ",";
            admins += string.Join(",", newclan.admins);
            admins += ",";
            members += string.Join(",", newclan.members);
            members += ",";

            if (newclan.admins.Count == 0)
                admins = "";
            if (newclan.members.Count == 0)
                members = "";
            int result = db.Query("UPDATE Clans SET owner=@0,admins=@1,members=@2 WHERE owner=@3;", newclan.owner, admins, members, oldowner);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to change owner where oldowner = {oldowner} and newowner = {newclan.owner}.");
        }

        public static void changeMembers(int owner, Clan newclan)
        {
            string admins = ",";
            string members = ",";
            admins += string.Join(",", newclan.admins);
            admins += ",";
            members += string.Join(",", newclan.members);
            members += ",";
            if (newclan.admins.Count == 0)
                admins = "";
            if (newclan.members.Count == 0)
                members = "";
            int result = db.Query("UPDATE Clans SET admins=@0,members=@1 WHERE owner=@2;", admins, members, owner);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to update players where owner = {owner}.");
        }

        public static void newClan(string name, int owner)
        {
            int result = db.Query("INSERT INTO Clans (owner, name, admins, members, prefix, banned, priv) VALUES (@0, @1, '', '', '', '', @2);", owner, name, 0);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to create a new clan with owner = {owner}.");
        }

        public static void clanPrefix(int owner, string newprefix)
        {
            int result = db.Query("UPDATE Clans SET prefix=@0 WHERE owner=@1;", newprefix, owner);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to set new prefix where owner = {owner}.");
        }
        
        public static void changeBanned(int owner, List<int> bannedlist)
        {
            string banned = ",";
            banned += string.Join(",", bannedlist);
            banned += ",";
            if (bannedlist.Count == 0)
                banned = "";
            int result = db.Query("UPDATE Clans SET banned=@0 WHERE owner=@1;", banned, owner);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to update banned list where owner = {owner}.");
        }

        public static void changePrivate(int owner, bool isPrivate)
        {
            int newpriv = isPrivate ? 1 : 0;
            int result = db.Query("UPDATE Clans SET priv=@0 WHERE owner=@1;", newpriv, owner);
            if (result != 1)
                TShock.Log.Error($"Database error: Failed to update private setting where owner = {owner}.");
        }
        
    }
}
