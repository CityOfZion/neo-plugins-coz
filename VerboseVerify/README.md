## VerboseVerify
A neo-cli plugin to provide extended information about transaction verification failures during the `sendrawtransaction` RPC method

### Author
hal0x2328

### Introduction
There are 20+ reasons a Neo transaction may fail verification, but the Neo RPC server by default only gives generic
error messages, such as the dreaded `Block or transaction validation failed`, leaving the developer on their own to 
figure out exactly what is wrong with their transaction syntax. The VerboseVerify plugin intercepts `sendrawtransaction`
requests and runs them through the same transaction verification process used by the Neo core, except it provides 
specific information about the exact failure condition. If the pre-verification step passes, the transaction continues
to be verified and processed as normal by the core code.

### Installation
```
git clone https://github.com/neo-plugins-coz/VerboseVerify
cd VerboseVerify
dotnet publish -c Release
cp ./bin/Release/netstandard2.0/publish/VerboseVerify.dll {neo-cli folder}/Plugins
```

### Configuration
No configuration required.

### Example failure message
```
{
    "jsonrpc": "2.0",
    "id": 1,
    "error": {
        "code": -500,
        "message": "Input 0x4fd449222de147a30bd408bf58588f091429aca58624a570e763eae9ddb7baaa:1 is marked as spent",
        "data": "   at Neo.Plugins.VerboseVerify.CheckDoubleSpend(IPersistence persistence, Transaction tx)
                    at Neo.Plugins.VerboseVerify.PreVerify(Transaction tx, Snapshot snapshot, IEnumerable 1 mempool)   
                    at Neo.Network.RPC.RpcServer.ProcessRequest(HttpContext context, JObject request)"
    }
}
```
