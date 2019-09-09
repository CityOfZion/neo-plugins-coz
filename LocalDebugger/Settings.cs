using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Neo.Plugins
{
    internal class Settings
    {
        public bool ActivateOnStart { get; }
        public uint[] PreloadBlockBreakPoints { get; }
        public string[] PreloadTxBreakPoints { get; }
        public Dictionary<string, uint> PreloadScriptBreakPoints { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.ActivateOnStart = Convert.ToBoolean(section.GetSection("ActivateOnStart").Value);
            Console.WriteLine($"ActivateOnStart: {this.ActivateOnStart}");
            this.PreloadBlockBreakPoints = section.GetSection("PreloadBlockBreakPoints").GetChildren().Select(p => uint.Parse(p.Value)).ToArray();
            this.PreloadTxBreakPoints = section.GetSection("PreloadTxBreakPoints").GetChildren().Select(p => p.Value).ToArray();
            this.PreloadScriptBreakPoints = section.GetSection("PreloadScriptBreakPoints").GetChildren()
        .Select(item => new KeyValuePair<string, uint>(item.Key, uint.Parse(item.Value)))
        .ToDictionary(x => x.Key, x => x.Value);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
