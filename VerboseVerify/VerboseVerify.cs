using Microsoft.AspNetCore.Http;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.Network.RPC;
using Neo.VM;
using System;
using System.Linq;
using System.Collections.Generic;
using Snapshot = Neo.Persistence.Snapshot;

// VerboseVerify by hal0x2328
// Get more diagnostic info about transaction verification failures during sendrawtransaction

namespace Neo.Plugins
{
    public class VerboseVerify : Plugin, IRpcPlugin
    {
        public override void Configure()
        {
        }

        public void PreProcess(HttpContext context, string method, JArray _params)
        {
            if (method == "sendrawtransaction")
            {
                JObject res = new JObject();
                 
                Transaction tx = Transaction.DeserializeFrom(_params[0].AsString().HexToBytes());
                Snapshot snapshot = Blockchain.Singleton.GetSnapshot();
                MemoryPool MemPool = Blockchain.Singleton.MemPool;

                PreVerify(tx, snapshot, MemPool.GetVerifiedTransactions());
            }
        }

        private void PreVerify(Transaction tx, Snapshot snapshot, IEnumerable<Transaction> mempool)
        {
            if (tx.Size > Transaction.MaxTransactionSize) throw new RpcException(-500, "Size exceeds maximum transaction size");
            for (int i = 1; i < tx.Inputs.Length; i++)
                for (int j = 0; j < i; j++)
                    if (tx.Inputs[i].PrevHash == tx.Inputs[j].PrevHash && tx.Inputs[i].PrevIndex == tx.Inputs[j].PrevIndex)
                        throw new RpcException(-500, "Duplicate inputs");
            if (mempool.Where(p => p != tx).SelectMany(p => p.Inputs).Intersect(tx.Inputs).Count() > 0)
                throw new RpcException(-500, "Transaction already in mempool");
            CheckDoubleSpend(snapshot, tx);
            foreach (var group in tx.Outputs.GroupBy(p => p.AssetId))
            {
                AssetState asset = snapshot.Assets.TryGet(group.Key);
                if (asset == null) throw new RpcException(-500, "Asset value is null");
                if (asset.Expiration <= snapshot.Height + 1 && asset.AssetType != AssetType.GoverningToken && asset.AssetType != AssetType.UtilityToken)
                    throw new RpcException(-500, "Asset registration has expired");
                foreach (TransactionOutput output in group)
                    if (output.Value.GetData() % (long)Math.Pow(10, 8 - asset.Precision) != 0)
                        throw new RpcException(-500, "Invalid precision for asset output"); 
            }
            TransactionResult[] results = tx.GetTransactionResults()?.ToArray();
            if (results == null) throw new RpcException(-500, "No TransactionResults found");
            TransactionResult[] results_destroy = results.Where(p => p.Amount > Fixed8.Zero).ToArray();
            if (results_destroy.Length > 1) throw new RpcException(-500, "TransactionResults destroy length > 1");
            if (results_destroy.Length == 1 && results_destroy[0].AssetId != Blockchain.UtilityToken.Hash)
                throw new RpcException(-500, "TransactionResults destroy length equals 1 but asset is not GAS");
            if (tx.SystemFee > Fixed8.Zero && (results_destroy.Length == 0 || results_destroy[0].Amount < tx.SystemFee))
                throw new RpcException(-500, "System fee > 0 but TransactionResults destroy length is zero or amount is less than fee");
            TransactionResult[] results_issue = results.Where(p => p.Amount < Fixed8.Zero).ToArray();
            switch (tx.Type)
            {
                case TransactionType.MinerTransaction:
                case TransactionType.ClaimTransaction:
                    if (results_issue.Any(p => p.AssetId != Blockchain.UtilityToken.Hash))
                        throw new RpcException(-500, "ClaimTransaction but asset is not GAS");
                    break;
                case TransactionType.IssueTransaction:
                    if (results_issue.Any(p => p.AssetId == Blockchain.UtilityToken.Hash))
                        throw new RpcException(-500, "IssueTransaction but asset hash matches GAS");
                    break;
                case TransactionType.InvocationTransaction:
                    InvocationTransaction it = (InvocationTransaction) tx;
                    if (it.Gas.GetData() % 100000000 != 0)
                        throw new RpcException(-500, "Gas parameter is non-integer");
                    break;
                default:
                    if (results_issue.Length > 0)
                        throw new RpcException(-500, "No assets issued");
                    break;
            }
            if (tx.Attributes.Count(p => p.Usage == TransactionAttributeUsage.ECDH02 || p.Usage == TransactionAttributeUsage.ECDH03) > 1)
                throw new RpcException(-500, "Multiple occurances of ECDH02/ECDH03 transaction attribute usages");
            VerifyWitnesses(tx, snapshot);
        }

        private void VerifyWitnesses(IVerifiable verifiable, Snapshot snapshot)
        {
            UInt160[] hashes;
            try
            {
                hashes = verifiable.GetScriptHashesForVerifying(snapshot);
            }
            catch (InvalidOperationException)
            {
                throw new RpcException(-500, "InvalidOperationException getting script hashes for verifying");
            }
            if (hashes.Length != verifiable.Witnesses.Length) throw new RpcException(-500, "Number of script hashes doesn't match number of witnesses");
            for (int i = 0; i < hashes.Length; i++)
            {
                byte[] verification = verifiable.Witnesses[i].VerificationScript;
                if (verification.Length == 0)
                {
                    using (ScriptBuilder sb = new ScriptBuilder())
                    {
                        sb.EmitAppCall(hashes[i].ToArray());
                        verification = sb.ToArray();
                    }
                }
                else
                {
                    if (hashes[i] != verifiable.Witnesses[i].ScriptHash) throw new RpcException(-500, $"Script hash {i} didn't match witness hash {i}");
                }
                using (ApplicationEngine engine = new ApplicationEngine(TriggerType.Verification, verifiable, snapshot, Fixed8.Zero))
                {
                    engine.LoadScript(verification);
                    engine.LoadScript(verifiable.Witnesses[i].InvocationScript);
                    engine.Execute();
                    if (engine.State.HasFlag(VMState.FAULT)) throw new RpcException(-500, "VM returned FAULT during verification trigger (invalid signature data)");
                    if (engine.ResultStack.Count != 1 || !engine.ResultStack.Pop().GetBoolean()) throw new RpcException(-500, "VM returned false from verification trigger (bad signature)");
                }
            }
        }

        private void CheckDoubleSpend(IPersistence persistence, Transaction tx)
        {
            if (tx.Inputs.Length == 0) return;
            foreach (var group in tx.Inputs.GroupBy(p => p.PrevHash))
            {
                UnspentCoinState state = persistence.UnspentCoins.TryGet(group.Key);
                if (state == null) throw new RpcException(-500, $"Unspent coin state for ${group.Key} is null");
                if (group.Any(p => p.PrevIndex >= state.Items.Length || state.Items[p.PrevIndex].HasFlag(CoinState.Spent)))
                {
                    int idx = 0;
                    foreach (var p in state.Items)
                    {
                        if (p.HasFlag(CoinState.Spent))
                        {
                           throw new RpcException(-500, $"Input {group.Key.ToString()}:{idx} is marked as spent");
                        }
                        idx += 1;
                    }
                    throw new RpcException(-500, "At least one input is marked as spent");
                }
            }
        }


        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            return null;
        }

        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
        }
    }
}
