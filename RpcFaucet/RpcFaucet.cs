using Microsoft.AspNetCore.Http;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Wallets;
using Neo.Network.RPC;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Neo.Plugins
{
    public class RpcFaucet : Plugin, IRpcPlugin
    {
        private List<string> _deniedMethods;
        private NeoSystem system;

        public override void Configure()
        {
            Console.WriteLine("Loading configuration for RpcFaucet plugin...");
            Settings.Load(GetConfiguration());
            _deniedMethods = new List<string> {
                "dumpprivkey",
                "getbalance",
                "getnewaddress",
                "getwalletheight",
                "listaddress",
                "sendfrom",
                "sendmany",
                "sendtoaddress"
            };
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (_deniedMethods.Contains(method))
                throw new RpcException(-400, "Access denied");
 
            if (method == "faucet")
            {
                system = Plugin.System;
                RpcServer rpcserver = system.RpcServer;
                Wallet Wallet = rpcserver.Wallet;
                UInt160 script_hash = UInt160.Parse(_params[0].AsString());
                UInt160 asset_id = UInt160.Parse(Settings.Default.TokenHash);
                UInt160 from_addr = UInt160.Parse(Settings.Default.FromAddr);
                BigDecimal value = BigDecimal.Parse(Settings.Default.AmountPerDay, (byte)0);

                DateTime now = DateTime.Now;
                string today = now.ToString("yyyyMMdd");

                List<TransactionAttribute> attrs = new List<TransactionAttribute>               { 
                    new TransactionAttribute
                    { 
                        Usage = TransactionAttributeUsage.Remark,
                        Data = Encoding.UTF8.GetBytes(today)
		    }
                };

                Transaction tx = Wallet.MakeTransaction(attrs, new[]
                {
                    new TransferOutput
                    {
                        AssetId = asset_id,
                        Value = value,
                        ScriptHash = script_hash
                    }
                }, from_addr, null, Fixed8.Zero);

                if (tx == null)
                    throw new RpcException(-300, "MakeTransaction failed");

                ContractParametersContext ctx = new ContractParametersContext(tx);
                Wallet.Sign(ctx);
                if (ctx.Completed)
                {
                    tx.Witnesses = ctx.GetWitnesses();
                    Wallet.ApplyTransaction(tx);
                    system.LocalNode.Tell(new LocalNode.Relay { Inventory = tx }, null);
                    return tx.ToJson();
                }
                else
                {
                    return ctx.ToJson();
                }
            }
            return null;
        }

        public void PreProcess(HttpContext context, string method, JArray _params)
        {
        }

        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
        }
    }
}
