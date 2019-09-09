using Microsoft.AspNetCore.Http;
using Neo.IO.Json;
using Neo.Ledger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Plugins
{
    public class RpcFindStorage : Plugin, IRpcPlugin
    {
        public override void Configure()
        {
        }

        public void PreProcess(HttpContext context, string method, JArray _params)
        {
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (method == "findstorage")
            {
                UInt160 script_hash = UInt160.Parse(_params[0].AsString());
                byte[] prefix = _params[1].AsString().HexToBytes();
                byte[] prefix_key;
                int toskip = 0;
                int totake = 0;

                using (MemoryStream ms = new MemoryStream())
                {
                    int index = 0;
                    int remain = prefix.Length;
                    while (remain >= 16)
                    {
                        ms.Write(prefix, index, 16);
                        ms.WriteByte(0);
                        index += 16;
                        remain -= 16;
                    }
                    if (remain > 0)
                        ms.Write(prefix, index, remain);
                    prefix_key = script_hash.ToArray()
                        .Concat(ms.ToArray()).ToArray();
                }

                if (_params.Count > 2)
                    toskip = int.Parse(_params[2].AsString());
                if (_params.Count > 3)
                    totake = int.Parse(_params[3].AsString());

                var iterator = Blockchain.Singleton.Store.GetStorages()
                    .Find(prefix_key)
                    .Where(p => p.Key.Key.Take(prefix.Length).SequenceEqual(prefix))
                    .Skip(toskip);
                if (totake > 0)
                    iterator = iterator.Take(totake);

                JArray array = new JArray();
                foreach (KeyValuePair<StorageKey, StorageItem> p in iterator)
                {
                    JObject item = new JObject();
                    item["key"] = p.Key.Key.ToHexString();
                    item["value"] = p.Value.Value.ToHexString();
                    array.Add(item);
                }

                return array;
            }
            return null;
        }
        
        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
        }
    }
}
