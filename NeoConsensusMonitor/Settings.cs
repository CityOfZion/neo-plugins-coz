using Microsoft.Extensions.Configuration;

namespace Neo.Plugins
{
    internal class Settings
    {
        public ConsensusNode[] ConsensusNodes { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.ConsensusNodes = section.GetSection("ConsensusNodes").Get<ConsensusNode[]>();
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
