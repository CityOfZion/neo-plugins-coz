// NeoConsensusMonitor by hal0x2328
// monitor consensus node reliability over time

using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Neo.Ledger;
using Neo.Persistence;
using Neo.Cryptography.ECC;
using Neo.IO.Json;

namespace Neo.Plugins
{
    public class NeoConsensusMonitor : Plugin, ILogPlugin, IRpcPlugin
    {
        private List<string> nodes; // list of validators in order as reported by the core 
        private Dictionary<uint,string> blocknodes; // which node validated a block
        private long lastblock; // unix timestamp of previous block
        public string magic;
        public Dictionary<string, ConsensusNode> stats;

        public List<string> GetConsensusNodes()
        {
            Snapshot snapshot = Blockchain.Singleton.GetSnapshot();
            var validators = snapshot.GetValidators();
            List<string> ret = new List<string>();

            foreach (ECPoint p in validators)
            {
                stats[p.ToString()].MonitoringStartedAt = snapshot.Height;
                ret.Add(p.ToString());
            }
            return ret;
        }

        void ILogPlugin.Log(string source, LogLevel level, string message)
        {
            DateTime foo = DateTime.UtcNow;
            long unixTime = ((DateTimeOffset)foo).ToUnixTimeSeconds();
            Console.WriteLine($"[{unixTime}] {source} {message}");

            if (source == "ConsensusService")
            {
                if (nodes == null)
                {
                    nodes = GetConsensusNodes();
                    lastblock = 0;
                }

                if (message.StartsWith("OnPrepareRequestReceived: ", StringComparison.CurrentCulture))
                {
                    Dictionary<string, string> prep = message.Substring(26).Split(' ')
                      .Select(value => value.Split('='))
                      .ToDictionary(pair => pair[0], pair => pair[1]);

                    int index = Int32.Parse(prep["index"]);
                    uint height = (uint)Int32.Parse(prep["height"]);
                    string node = nodes[index];
                    int view = Int32.Parse(prep["view"]);
                    if (view == 0)
                    {
                        blocknodes.Add(height, node);
                    }
                    else
                    {
                        // a view change happened - don't track this block for statistics
                        if (blocknodes.ContainsKey(height))
                            blocknodes.Remove(height);
                    }
                }
                else if (message.StartsWith("persist block:", StringComparison.CurrentCulture))
                {
                    if (lastblock > 0)
                    {
                        Dictionary<string, string> pers = message.Substring(15).Split(' ')
                        .Select(value => value.Split('='))
                        .ToDictionary(pair => pair[0], pair => pair[1]);
                        long elapsed = unixTime - lastblock;
                        uint block = (uint)Int32.Parse(pers["height"]);
                        if (blocknodes.ContainsKey(block))
                        {
                            // discard results less than 15 seconds
                            if (elapsed >= 15)
                            {
                                string speaker = blocknodes[block];
                                stats[speaker].BlocksProcessed = stats[speaker].BlocksProcessed + 1;
                                stats[speaker].TimeSpent = stats[speaker].TimeSpent + (uint) elapsed;
                                stats[speaker].LastBlock = block;

                                decimal a = (decimal) stats[speaker].TimeSpent / stats[speaker].BlocksProcessed;
                                string average = a.ToString("0.00");
                                Console.WriteLine($"{stats[speaker].Name} produced block {block} in {elapsed} seconds, averaging {average} second blocktimes");
                            }
                            blocknodes.Remove(block);
                        }
                    }
                    lastblock = unixTime;
                }
            }
        }

        private bool OnStats()
        {
            foreach (ConsensusNode c in stats.Values)
            {
                if ((magic == "00746E41" && c.MainNetNode) ||
                    (magic == "74746E41" && c.TestNetNode) ||
                    (magic != "00746E41" && magic != "74746E41" && !c.MainNetNode && !c.TestNetNode))
                {
                    PrintStats(c);
                }
            }
            return true;
        }

        private void PrintStats(ConsensusNode c)
        {
            if (c.BlocksProcessed > 0)
            {
                float average = c.TimeSpent / c.BlocksProcessed;
                Console.WriteLine($"{c.Name}: {c.BlocksProcessed} blocks processed with an average of {average} seconds per block");
            }
            else
            {
                Console.WriteLine($"{c.Name}: No blocks processed since neo-cli start");
            }
        }

        public override void Configure()
        {
            Console.WriteLine("Loading new list of ConsensusNodes for monitoring");

            stats = new Dictionary<string, ConsensusNode>();
            magic = ProtocolSettings.Default.Magic.ToString("X8");
            blocknodes = new Dictionary<uint, string>();

            Settings.Load(GetConfiguration());

            foreach (ConsensusNode c in Settings.Default.ConsensusNodes)
            {
                stats.Add(c.PublicKey, c);
                Console.WriteLine($"Added ConsensusNode {c.Name}:{c.PublicKey}");
            }
        }

        protected override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args.Length == 0) return false;
            switch (args[0].ToLower())
            {
                case "stats":
                    return OnStats();
            }
            return false;
        }
        public void PreProcess(HttpContext context, string method, JArray _params)
        {
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (string.Compare(method, "consensusstats", true) == 0)
            {
                JArray j = new JArray();
                foreach (ConsensusNode c in stats.Values)
                { 
                    if ((magic == "00746E41" && c.MainNetNode) ||
                        (magic == "74746E41" && c.TestNetNode) ||
                        (magic != "00746E41" && magic != "74746E41" && !c.MainNetNode && !c.TestNetNode))
                    {
                        j.Add(c.ToJson());
                    }
                }
                return j as JObject;
            }
            return null;
        }

        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
        }

    }
}
