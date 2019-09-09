# NeoConsensusMonitor

A plugin for neo-cli to monitor consensus statistics

### Author
hal0x2328

### Installation
git clone https://github.com/neo-plugins-coz/NeoConsensusMonitor
cd NeoConsensusMonitor
dotnet publish -c Release
cp -r ./NeoConsensusMonitor {neo-cli folder}/Plugins
cp ./bin/Release/netstandard2.0/publish/NeoConsensusMonitor.dll {neo-cli folder}/Plugins

### Usage
Start neo-cli, open a wallet and run
```
start consensus
```

The `stats` command will give a current summary for all consensus nodes


## TODO 
Store the statistics in Redis for long-term analysis
