using Microsoft.Extensions.Configuration;
using Neo.Network.P2P;
using System.Linq;
using System.Reflection;

namespace Neo.Plugins
{
    internal class Settings
    {
        public string TokenHash { get; }
        public string FromAddr { get; }
        public string AmountPerDay { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.TokenHash = section.GetSection("TokenHash").Value;
            this.FromAddr = section.GetSection("FromAddr").Value;
            this.AmountPerDay = section.GetSection("AmountPerDay").Value;
        }
        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
