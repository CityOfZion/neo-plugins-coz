using Microsoft.Extensions.Configuration;

namespace Neo.Plugins
{
    internal class Settings
    {
        public string DynamoDBLocalHost { get; }
        public string DynamoDBLocalPort { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.DynamoDBLocalHost = section.GetSection("DynamoDBLocalHost").Value;
            this.DynamoDBLocalPort = section.GetSection("DynamoDBLocalPort").Value;
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
