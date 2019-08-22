using Microsoft.AspNetCore.Http;
using Neo.IO.Json;

namespace Neo.Plugins
{
    public class RpcSamplePlugin : Plugin, IRpcPlugin
    {
        public override void Configure()
        {
        }

        public void PreProcess(HttpContext context, string method, JArray _params)
        {
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (string.Compare(method, "dummy", true) == 0)
            {
                JObject ret = new JObject();
                ret["dummyResult"] = true;
                return ret;
            }
            return null;
        }

        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
        }
    }
}
