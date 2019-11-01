using Neo.IO.Json;
using Neo.Ledger;
using Neo.VM;
using Neo.Wallets;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.Linq;
using Snapshot = Neo.Persistence.Snapshot;
using System.Net.Sockets;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System.Text;
using System.Numerics;
using AC.Components.Util;

namespace Neo.Plugins
{
    public class DynamoDBPublisher : Plugin, IPersistencePlugin
    {
        private AmazonDynamoDBClient client;
        private Table ApplicationLogsTable;
        private Table BlocksTable;
        private Table TransactionsTable;
        private Table TransfersTable;
        private Table AddressesTable;
        private Table AssetsTable;
        private Table ContractsTable;
        private bool databaseInitialized;
        private HashSet<string> Assets;
        private HashSet<string> Contracts;

        public override string Name => "DynamoDBPublisher";

        public DynamoDBPublisher()
        {
            Assets = new HashSet<string>();
            Contracts = new HashSet<string>();
        }

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        private void initDatabase()
        {
            if (databaseInitialized) return;
            createClient();
            AddressesTable = Table.LoadTable(client, "Addresses");
            ApplicationLogsTable = Table.LoadTable(client, "ApplicationLogs");
            AssetsTable = Table.LoadTable(client, "Assets");
            BlocksTable = Table.LoadTable(client, "Blocks");
            ContractsTable = Table.LoadTable(client, "Contracts");
            TransactionsTable = Table.LoadTable(client, "Transactions");
            TransfersTable = Table.LoadTable(client, "Transfers");
            databaseInitialized = true;
        }

	private void PreloadContracts()
	{
            Console.WriteLine("Scanning remote database for contracts to preload");
            ScanOperationConfig contractsconfig = new ScanOperationConfig();
            Search contractsSearch = ContractsTable.Scan(contractsconfig);

            List<Document> contractList = new List<Document>();
	    int idx = 0;
            do
            {
		contractList = AsyncUtil.RunSync(() => contractsSearch.GetNextSetAsync());
                //contractList = await contractsSearch.GetNextSetAsync();
                foreach (var document in contractList)
                {
                    string contract = document["hash"];
                    Console.WriteLine($"Preloading contract {contract}");
                    Contracts.Add(contract);
		    idx++;
                }
            } while (!contractsSearch.IsDone);
	}

	private void PreloadAssets()
	{
            Console.WriteLine("Scanning remote database for assets to preload");
            ScanOperationConfig assetsConfig = new ScanOperationConfig();
            Search assetsSearch = AssetsTable.Scan(assetsConfig);

            List<Document> assetList = new List<Document>();
            do
            {

		assetList = AsyncUtil.RunSync(() => assetsSearch.GetNextSetAsync());
                //assetList = await assetsSearch.GetNextSetAsync();
                foreach (var document in assetList)
                {
                    string asset = document["scripthash"];
                    Console.WriteLine($"Preloading asset {asset}");
                    Assets.Add(asset);
                }
            } while (!assetsSearch.IsDone);
	}

        private void AddTransaction(Transaction tx, ulong ts, ulong height)
        {
            if (tx != null)
            {
                Console.WriteLine($"Adding transaction {tx.Hash} to DynamoDB table");
                JObject j = tx.ToJson();
                j["time"] = ts;
                j["block"] = height;
                Document d = Document.FromJson(j.ToString());
                TransactionsTable.UpdateItemAsync(d);
            }
        }

        private void AddBlock(Snapshot snapshot)
        {
	    Block block = snapshot.PersistingBlock;
	    ulong idx = block.Index;
            Console.WriteLine($"Adding block {idx} to DynamoDB table");
	    ulong blocktime = 0;
	    if (block.Index > 0)
	    {
	      ulong timestamp = block.Timestamp;
	      Header header = snapshot.GetHeader((uint)idx - 1);
	      ulong lasttimestamp = header.Timestamp;
              blocktime = timestamp - lasttimestamp;  
	    }
	    JObject j = block.ToJson();
	    j["blocktime"] = blocktime;
            Document d = Document.FromJson(j.ToString());
            BlocksTable.UpdateItemAsync(d);
        }

        private void RecordTransferHistory(Snapshot snapshot, UInt160 scriptHash, UInt160 from, UInt160 to, BigInteger amount, UInt256 txHash, ref ushort transferIndex)
        {
            Header header = snapshot.GetHeader(snapshot.Height);

            JObject transfer = new JObject();
            transfer["from"] = from.ToAddress();
            transfer["to"] = to.ToAddress();
            transfer["time"] = header.Timestamp;
            transfer["scripthash"] = scriptHash.ToString();
            transfer["amount"] = amount.ToString();
            transfer["block"] = snapshot.Height;
            transfer["txid"] = txHash.ToString();
            transfer["transferindex"] = $"{snapshot.Height}.{transferIndex}";

            Document a = Document.FromJson(transfer.ToString());
            TransfersTable.UpdateItemAsync(a);

            transferIndex++;

            // store the asset metadata if this is the first time we're seeing this scripthash
	    if (Assets.Count == 0)
	    {
		    PreloadAssets();
	    }
            if (!Assets.Contains(scriptHash.ToString()))
            {
                AddAsset(snapshot, scriptHash);
            }
        }

        private void AddAsset(Snapshot snapshot, UInt160 scriptHash)
        {
   
            JObject asset = new JObject();
            string n = GetAssetString(snapshot, scriptHash, "name");
            string s = GetAssetString(snapshot, scriptHash, "symbol");
            BigInteger d = GetAssetInteger(snapshot, scriptHash, "decimals");
            if (n.Length > 0 && s.Length > 0)
            {
                ContractState contract = snapshot.Contracts.TryGet(scriptHash);
                asset["scripthash"] = scriptHash.ToString();
                asset["firstseen"] = snapshot.Height;
                asset["name"] = n;
                asset["symbol"] = s;
                asset["decimals"] = d.ToString();
                asset["state"] = contract.ToJson();
            }
            Document b = Document.FromJson(asset.ToString());
            AssetsTable.UpdateItemAsync(b);
            Assets.Add(scriptHash.ToString());
        }

        /*
        private void RefreshContractList(Snapshot snapshot)
        {
            Contracts = new HashSet<string>();
            int idx = 0;
            foreach (KeyValuePair<UInt160, ContractState> dc in snapshot.Contracts.Find())
            {
                Console.WriteLine($"Adding contract {idx}: {dc.Key.ToString()}");
                JObject c = dc.Value.ToJson();
                c["idx"] = idx;

                Document b = Document.FromJson(c.ToString());
                ContractsTable.UpdateItemAsync(b);
                Contracts.Add(dc.Key.ToString());

                idx++;
            }
            ContractCount = idx;
        }
        */
        private void AddNewContracts(Snapshot snapshot)
        {
            int idx = Contracts.Count;
            foreach (KeyValuePair<UInt160, ContractState> dc in snapshot.Contracts.Find())
            {
                string cs = dc.Key.ToString();
		if (!Contracts.Contains(cs))
                {
                    Console.WriteLine($"Adding contract {idx}: {cs}");
                    JObject c = dc.Value.ToJson();
                    c["idx"] = idx;
                    c["block"] = snapshot.Height;
                    c["time"] = snapshot.PersistingBlock.Timestamp;

                    Document b = Document.FromJson(c.ToString());
                    ContractsTable.UpdateItemAsync(b);
                    Contracts.Add(cs);
                    idx++;
                }
            }
        }

        private void HandleNotification(Snapshot snapshot, Transaction transaction, UInt160 scriptHash,
        VM.Types.Array stateItems,
        Dictionary<Nep5BalanceKey, Nep5Balance> nep5BalancesChanged, ref ushort transferIndex)
        {
            if (stateItems.Count == 0) return;
            // Event name should be encoded as a byte array.
            if (!(stateItems[0] is VM.Types.ByteArray)) return;
            var eventName = Encoding.UTF8.GetString(stateItems[0].GetByteArray());
            if (eventName != "Transfer") return;
            if (stateItems.Count < 4) return;

            if (!(stateItems[1] is null) && !(stateItems[1] is VM.Types.ByteArray))
                return;
            if (!(stateItems[2] is null) && !(stateItems[2] is VM.Types.ByteArray))
                return;
            var amountItem = stateItems[3];
            if (!(amountItem is VM.Types.ByteArray || amountItem is VM.Types.Integer))
                return;
            byte[] fromBytes = stateItems[1]?.GetByteArray();
            if (fromBytes?.Length != 20) fromBytes = null;
            byte[] toBytes = stateItems[2]?.GetByteArray();
            if (toBytes?.Length != 20) toBytes = null;
            if (fromBytes == null && toBytes == null) return;
            var from = new UInt160(fromBytes);
            var to = new UInt160(toBytes);

            if (fromBytes != null)
            {
                var fromKey = new Nep5BalanceKey(from, scriptHash);
                if (!nep5BalancesChanged.ContainsKey(fromKey)) nep5BalancesChanged.Add(fromKey, new Nep5Balance());
            }

            if (toBytes != null)
            {
                var toKey = new Nep5BalanceKey(to, scriptHash);
                if (!nep5BalancesChanged.ContainsKey(toKey)) nep5BalancesChanged.Add(toKey, new Nep5Balance());
            }
            RecordTransferHistory(snapshot, scriptHash, from, to, amountItem.GetBigInteger(), transaction.Hash, ref transferIndex);
        }

        public void OnPersist(Snapshot snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            initDatabase();
            Dictionary<Nep5BalanceKey, Nep5Balance> nep5BalancesChanged = new Dictionary<Nep5BalanceKey, Nep5Balance>();

            ushort transferIndex = 0;
            foreach (var appExec in applicationExecutedList)
            {
                // Add transaction to DynamoDB
                AddTransaction(appExec.Transaction, snapshot.PersistingBlock.Timestamp, snapshot.Height);

                // Extract transfer notifications and executed contracts
                if (!appExec.VMState.HasFlag(VMState.FAULT))
                {

                    foreach (var notifyEventArgs in appExec.Notifications)
                    {
                        if (!(notifyEventArgs?.State is VM.Types.Array stateItems) || stateItems.Count == 0
                            || !(notifyEventArgs.ScriptContainer is Transaction transaction))
                            continue;
                        HandleNotification(snapshot, transaction, notifyEventArgs.ScriptHash, stateItems,
                            nep5BalancesChanged, ref transferIndex);
                    }

                }

                // Add application log to DynamoDB
                JObject json = new JObject();
                json["txid"] = appExec.Transaction?.Hash.ToString();
                json["trigger"] = appExec.Trigger.ToString();
                json["vmstate"] = appExec.VMState.ToString();
                json["gas_consumed"] = appExec.GasConsumed.ToString();
                try
                {
                    json["stack"] = appExec.Stack.Select(q => q.ToParameter().ToJson()).ToArray();
                }
                catch (InvalidOperationException)
                {
                    json["stack"] = "error: recursive reference";
                }
                json["notifications"] = appExec.Notifications.Select(q =>
                {
                    JObject notification = new JObject();
                    notification["contract"] = q.ScriptHash.ToString();
                    try
                    {
                        notification["state"] = q.State.ToParameter().ToJson();
                    }
                    catch (InvalidOperationException)
                    {
                        notification["state"] = "error: recursive reference";
                    }
                    return notification;
                }).ToArray();
                string n = json.ToString();
                Document a = Document.FromJson(n);
                if (json["txid"] != null)
                {
                    ApplicationLogsTable.UpdateItemAsync(a);
                }

	    	if (Contracts.Count == 0)
	    	{
		    PreloadContracts();
	    	}
                int newCount = snapshot.Contracts.Find().Count();
                if (newCount != Contracts.Count)
                {
                    Console.WriteLine($"contract count: snapshot:{newCount} != memory:{Contracts.Count}");
                    AddNewContracts(snapshot);
                }
            }

            // process all balance changes
            foreach (var nep5BalancePair in nep5BalancesChanged)
            {
                // get guaranteed-accurate balances by calling balanceOf for keys that changed.
                byte[] script;
                using (ScriptBuilder sb = new ScriptBuilder())
                {
                    sb.EmitAppCall(nep5BalancePair.Key.AssetScriptHash, "balanceOf",
                        nep5BalancePair.Key.UserScriptHash.ToArray());
                    script = sb.ToArray();
                }

                ApplicationEngine engine = ApplicationEngine.Run(script, snapshot, extraGAS: 100000000);
                if (engine.State.HasFlag(VMState.FAULT)) continue;
                if (engine.ResultStack.Count <= 0) continue;
                nep5BalancePair.Value.Balance = engine.ResultStack.Pop().GetBigInteger();
                nep5BalancePair.Value.LastUpdatedBlock = snapshot.Height;

                JObject balance = new JObject();
                balance["address"] = nep5BalancePair.Key.UserScriptHash.ToAddress();
                balance["asset"] = nep5BalancePair.Key.AssetScriptHash.ToString();
                balance["balance"] = nep5BalancePair.Value.Balance.ToString();
                balance["lastupdatedblock"] = nep5BalancePair.Value.LastUpdatedBlock;

                Document a = Document.FromJson(balance.ToString());
                AddressesTable.UpdateItemAsync(a);

            }
        }

        public string GetAssetString(Snapshot snapshot, UInt160 scripthash, string operation)
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitAppCall(scripthash, operation,
                    scripthash.ToArray());
                script = sb.ToArray();
            }

            ApplicationEngine engine = ApplicationEngine.Run(script, snapshot, extraGAS: 100000000);
            if (engine.State.HasFlag(VMState.FAULT)) return "";
            if (engine.ResultStack.Count <= 0) return "";
            return engine.ResultStack.Pop().GetString();
        }

        public BigInteger GetAssetInteger(Snapshot snapshot, UInt160 scripthash, string operation)
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitAppCall(scripthash, operation,
                    scripthash.ToArray());
                script = sb.ToArray();
            }

            ApplicationEngine engine = ApplicationEngine.Run(script, snapshot, extraGAS: 100000000);
            if (engine.State.HasFlag(VMState.FAULT)) return 0;
            if (engine.ResultStack.Count <= 0) return 0;
            return engine.ResultStack.Pop().GetBigInteger();
        }

        public void OnCommit(Snapshot snapshot)
        {
            AddBlock(snapshot);
        }

        public bool ShouldThrowExceptionFromCommit(Exception ex)
        {
            return true;
        }

        public bool createClient()
        {
            string Host = Settings.Default.DynamoDBLocalHost;
            string Port = Settings.Default.DynamoDBLocalPort;

            if ((Host.Length > 0) && (Port.Length > 0))
            {
                bool localFound = false;
                try
                {
                    using (var tcp_client = new TcpClient())
                    {
                        var result = tcp_client.BeginConnect(Host, Int16.Parse(Port), null, null);
                        localFound = result.AsyncWaitHandle.WaitOne(3000); // Wait 3 seconds
                        tcp_client.EndConnect(result);
                    }
                }
                catch
                {
                    localFound = false;
                }
                if (!localFound)
                {
                    Console.WriteLine($"DynamoDB Local not found on port {Port}");
                    return (false);
                }

                Console.WriteLine($"Connecting to local DynamoDB instance on {Host}:{Port}");
                AmazonDynamoDBConfig ddbConfig = new AmazonDynamoDBConfig();
                ddbConfig.ServiceURL = $"http://{Host}:{Port}";
                try { client = new AmazonDynamoDBClient(ddbConfig); }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to connect to DynamoDB: " + ex.Message);
                    return false;
                }
            }
            else
            {
                try { client = new AmazonDynamoDBClient(); }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to connect to DynamoDB: " + ex.Message);
                }
            }
            return true;
        }
    }
}
