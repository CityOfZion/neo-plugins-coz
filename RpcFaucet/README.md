# RpcFaucet
neo-cli plugin to implement a faucet over RPC

## Notes

## Compile
```
git clone https://github.com/neo-project/neo
git clone https://github.com/neo-plugins-coz/RpcFaucet
cd RpcFaucet
dotnet restore
dotnet publish -c Release
```
## Install
See [Neo Plug-ins page](http://docs.neo.org/en-us/node/plugin.html)

## Method parameters
```
faucet <address>
```

## Example request
```
{
  "jsonrpc": "2.0",
  "method": "faucet",
  "params": [
    "ba06a313a2046fe1d00a3a3253dee90be9c4acac"
  ],
  "id": 1
}
```

## Example result
