using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clans3
{
    public class Clan
    {
        public string name;
        public int owner;
        public List<int> admins;
        public List<int> members;
        public string prefix;
        public List<int> banned;

        public Clan(string _name, int _owner)
        {
            name = _name;
            owner = _owner;
            admins = new List<int>();
            members = new List<int>();
            prefix = "";
            banned = new List<int>();
        }
    }
}
