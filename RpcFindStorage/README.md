# RpcFindStorage
neo-cli plugin to implement a `findstorage` extension RPC method

### Author
hal0x2328

## Notes
Not intended for public RPC nodes, this is mainly intended to provide 
a faster method to supply your dApp with information from your smart contract 
in situations where there might be hundreds or thousands of results in a 
query and pagination is required, e.g. retrieving a batch of IDs of NFT tokens 
owned by a particular address. This removes complexity from the smart contract
and overhead required if the same query was answered by an operation in the
smart contract running inside the Neo VM.

## Compile
```
git clone https://github.com/neo-project/neo
git clone https://github.com/neo-plugins-coz/RpcFindStorage
cd RpcFindStorage
dotnet restore
dotnet publish -c Release
```
## Install
See [Neo Plug-ins page](http://docs.neo.org/en-us/node/plugin.html)

## Method parameters
```
findstorage <contract script hash> <key prefix> [start index] [number of results]
```

## Example request
```
{
  "jsonrpc": "2.0",
  "method": "findstorage",
  "params": [
    "ba06a313a2046fe1d00a3a3253dee90be9c4acac",
    "68656c6c6f",
    0,
    3
  ],
  "id": 1
}
```

## Example result
```
{
    "jsonrpc": "2.0",
    "id": 1,
    "result": [
        {
            "key": "68656c6c6f5f68656c6c6f31",
            "value": "6d7976616c756531"
        },
        {
            "key": "68656c6c6f5f68656c6c6f32",
            "value": "6d7976616c756532"
        },
        {
            "key": "68656c6c6f5f68656c6c6f33",
            "value": "6d7976616c756533"
        }
    ]
}
```
