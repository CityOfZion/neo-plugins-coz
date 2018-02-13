using Neo.IO.Json;
using Neo.Plugins;

namespace RpcSampleNeoPlugin
{
    public class RpcSamplePlugin : NeoRpcPlugin
    {
        public override JObject RpcCall(NeoRpcPluginArgs args)
        {
            if (string.Compare(args.Method, "dummy", true) == 0)
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