using System;
using StackExchange.Redis;
using Neo.Ledger;
using Neo.VM;
using Neo.SmartContract;
using System.Collections.Generic;

using Snapshot = Neo.Persistence.Snapshot;

namespace Neo.Plugins
{
    public class NeoPubSub : Plugin, ILogPlugin, IPersistencePlugin
    {
        private readonly ConnectionMultiplexer connection;

        public NeoPubSub()
        {
            Console.WriteLine($"Connecting to PubSub server at {Settings.Default.RedisHost}:{Settings.Default.RedisPort}");
            this.connection = ConnectionMultiplexer.Connect($"{Settings.Default.RedisHost}:{Settings.Default.RedisPort}");
            if (this.connection == null) {
                Console.WriteLine("Connection failed!");
            } else {
                Console.WriteLine("Connected.");
            }
        }

        void ILogPlugin.Log(string source, LogLevel level, string message)
        {
            connection.GetSubscriber().Publish(source, message);
        }

        public override void Configure()
        {
 
            Settings.Load(GetConfiguration());

            foreach (string c in Settings.Default.WatchContracts)
            {
                Console.WriteLine($"Watching contract {c}");
            }
        }

        public void OnPersist(Snapshot snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            foreach (var appExec in applicationExecutedList)
            {
                var txid = appExec.Transaction.Hash.ToString();
                foreach (ApplicationExecutionResult p in appExec.ExecutionResults)
                {

                    if (p.VMState.ToString() == "HALT, BREAK")
                    {
                        foreach (NotifyEventArgs q in p.Notifications)
                        {
                            string contract = q.ScriptHash.ToString();
                            if (Array.IndexOf(Settings.Default.WatchContracts, contract) >= 0 ||
                                Array.IndexOf(Settings.Default.WatchContracts, "*") >= 0)
                            {
                                string r = q.State.ToParameter().ToJson().ToString();
                                connection.GetSubscriber().Publish(contract, $"{txid} {r}");
                            }
                        }
                    }
                }
            }
        }

        public void OnCommit(Snapshot snapshot)
        {
        }

        public bool ShouldThrowExceptionFromCommit(Exception ex)
        {
            return false;
        }

    }
}
