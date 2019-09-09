using Microsoft.Extensions.Configuration;

namespace Neo.Plugins
{
    internal class Settings
    {
        public string MongoHost { get; }
        public string MongoPort { get; }
        public string MongoUser { get; }
        public string MongoPass { get; }
        public ContractConfig[] ContractConfigs { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
     
              this.MongoHost = section.GetSection("MongoHost").Value;
              this.MongoPort = section.GetSection("MongoPort").Value;
              this.MongoUser = section.GetSection("MongoUser").Value;
              this.MongoPass = section.GetSection("MongoPass").Value;
              this.ContractConfigs = section.GetSection("Contracts").Get<ContractConfig[]>();
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
