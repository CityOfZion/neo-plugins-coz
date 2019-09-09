using Neo.IO.Json;
using Neo.Ledger;
using Neo.VM;
using System;
using System.Collections.Generic;
using Snapshot = Neo.Persistence.Snapshot;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;
using System.Linq;
using Neo.Wallets;

namespace Neo.Plugins
{
    public class NotifyMongo : Plugin, IPersistencePlugin
    {
        private readonly MongoClient client;

        public override string Name => "NotifyMongo";

        public NotifyMongo()
        {
            var s = Settings.Default;
            if (s.MongoUser != "")
            {
                client = new MongoClient($"mongodb://{s.MongoUser}:{s.MongoPass}@{s.MongoHost}:{s.MongoPort}");
                Console.WriteLine($"connected to mongodb://xxx:xxx@{s.MongoHost}:{s.MongoPort}");
            }
            else
            {
                client = new MongoClient($"mongodb://{s.MongoHost}:{s.MongoPort}");
                Console.WriteLine($"connected to mongodb://{s.MongoHost}:{s.MongoPort}");
            }
        }

        public override void Configure()
        {
            Settings.Load(GetConfiguration());

            foreach (ContractConfig contract in Settings.Default.ContractConfigs)
            {
                foreach (MongoAction action in contract.Actions)
                {
                    Console.WriteLine($"Monitoring {contract.Scripthash} for {action.OnNotification} notification");
                }
            }
        }

        public void OnPersist(Snapshot snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            uint height = snapshot.PersistingBlock.Index;
            var db = client.GetDatabase("chaindata");
            var collection = db.GetCollection<BsonDocument>("chaindata");
            BsonDocument doc = BsonDocument.Parse("{\"chain\": 1, \"currentHeight\":" + height.ToString() + "}");
            var filter = Builders<BsonDocument>.Filter.Eq("chain", 1);
            UpdateOptions options = new UpdateOptions();
            options.IsUpsert = true;
            collection.ReplaceOne(filter, doc, options);

            foreach (var appExec in applicationExecutedList)
            {
                var txid = appExec.Transaction.Hash.ToString();
                foreach (ApplicationExecutionResult p in appExec.ExecutionResults)
                {
                    if (p.VMState.ToString().Contains("HALT"))
                    {
                        foreach (SmartContract.NotifyEventArgs q in p.Notifications)
                        {
                            var contract = q.ScriptHash.ToString();

                            foreach (ContractConfig h in Settings.Default.ContractConfigs)
                            {
                                if (contract.Contains(h.Scripthash))
                                {
                                    JObject notification = q.State.ToParameter().ToJson();
                                    HandleNotification(txid, h.Scripthash, notification["value"] as JArray, h.Actions);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void HandleNotification(string txid, string Scripthash, JArray notificationFields, MongoAction[] actions)
        {
            string notification_type = Encoding.UTF8.GetString(notificationFields[0]["value"].AsString().HexToBytes());
            foreach (MongoAction a in actions)
            {
                if (a.OnNotification == notification_type)
                {
                    JObject record = new JObject();
                    string idxKey = "";
                    int count = 1;
                    foreach (Dictionary<string, string> s in a.Schema)
                    {
                        if (s["Type"] == "string")
                        {
                            record[s["Name"]] = Encoding.UTF8.GetString(notificationFields[count]["value"].AsString().HexToBytes());
                        }
                        else if (s["Type"] == "integer")
                        {
                            if (notificationFields[count]["type"].AsString() == "Integer")
                            {
                                record[s["Name"]] = notificationFields[count]["value"].AsNumber();
                            }
                            else
                            {
                                record[s["Name"]] = HexToInt(notificationFields[count]["value"].AsString());
                            }
                        }
                        else if (s["Type"].Contains("uint"))
                        {
                            record[s["Name"]] = notificationFields[count]["value"].AsString().HexToBytes().Reverse().ToHexString();
                        }
                        else if (s["Type"] == "address")
                        {
                            record[s["Name"]] = UInt160.Parse(notificationFields[count]["value"].AsString().HexToBytes().Reverse().ToHexString()).ToAddress();
                        }
                        else
                        {
                            record[s["Name"]] = notificationFields[count]["value"].AsString();
                        }

                        if (count == a.Keyindex)
                        {
                            idxKey = record[s["Name"]].AsString();
                        }
                        count += 1;
                    }
                    if (a.Keyindex > 0)
                    {
                        record["refid"] = idxKey;
                    }
                    record["txid"] = txid;
                    BsonDocument doc = BsonDocument.Parse(record.ToString());
                    var db = client.GetDatabase(Scripthash);
                    var collection = db.GetCollection<BsonDocument>(a.Collection);
                    if (a.Action == "create")
                    {
                        collection.InsertOne(doc);
                    }
                    else if (a.Action == "delete")
                    {
                        var filter = Builders<BsonDocument>.Filter.Eq("refid", idxKey);
                        collection.DeleteOne(filter);
                    }
                }
            }
        }

        private ulong HexToInt(string hex)
        {
            return BitConverter.ToUInt64(hex.PadRight(16, '0').HexToBytes(), 0);
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
