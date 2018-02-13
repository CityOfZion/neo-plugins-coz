using Neo.IO.Json;
using Neo.Plugins;

namespace RpcSampleNeoPlugin
{
    public class NeoRpcSample : NeoRpcPlugin
    {
        public override JObject RpcCall(NeoRpcPluginArgs args)
        {
            if (string.Compare(args.Method, "dummy") == 0)
            {
                args.Handle = true;

                JObject ret = new JObject();
                ret["dummyResult"] = true;
                return ret;
            }

            return null;
        }
    }
}