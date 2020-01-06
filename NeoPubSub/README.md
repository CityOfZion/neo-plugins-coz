# NeoPubSub

A plugin for neo-cli to publish smart contract events to a Redis PubSub queue

### Author
hal0x2328

### Installation
```bash
git clone https://github.com/neo-plugins-coz/NeoPubSub
cd NeoPubSub
dotnet publish -c Release
cp -r ./NeoPubSub {neo-cli folder}/Plugins
cp ./bin/Release/netstandard2.0/publish/NeoPubSub.dll {neo-cli folder}/Plugins
cp ./bin/Release/netstandard2.0/publish/Redis*.dll {neo-cli folder}
```

### Example PubSub session:
```
redis-cli
127.0.0.1:6379> SUBSCRIBE 0x0947ff380d2022110b1e53bbce5eee6a6a701c4f
1) "subscribe"
2) "0x0947ff380d2022110b1e53bbce5eee6a6a701c4f"
3) (integer) 1
1) "message"
2) "0x0947ff380d2022110b1e53bbce5eee6a6a701c4f"
3) "0x9aa3f1b15aba276cd5798630ba5ed46680583c0532450a24e15b7a4fd8a1f946 {\"type\":\"Array\",\"value\":[{\"type\":\"ByteArray\",\"value\":\"7472616e73666572\"},{\"type\":\"ByteArray\",\"value\":\"796f75\"},{\"type\":\"ByteArray\",\"value\":\"6d65\"},{\"type\":\"ByteArray\",\"value\":\"31206d696c6c696f6e204e454f\"}]}"
```
