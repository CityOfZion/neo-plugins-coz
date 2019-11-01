## DynamoDBPublisher
A neo-cli plugin to index all Neo3 blocks/transactions/contracts/assets to DynamoDB

### Author
hal0x2328

### Introduction
This plugin is the basis for the Neo3 preview explorer - it is not compatible with Neo2 (lacks UTXO tracking ability). It can be used with the AWS DynamoDB cloud service or a local DynamoDB instance. The plugin is intended for use with the neo3-explorer-api which will create the necessary tables/indexes to publish to.

### Installation
```
git clone https://github.com/neo-plugins-coz/DynamoDBPublisher
cd DynamoDBPublisher
dotnet publish -c Release
cp -r ./DynamoDBPublisher {neo-cli folder}/Plugins
cp ./bin/Release/netcoreapp2.1/publish/DynamoDBPublisher.dll {neo-cli folder}/Plugins
cp ./bin/Release/netcoreapp2.1/publish/AWSSDK*.dll {neo-cli folder}
```

### Configuration
Define a local DynamoDB server instance (or leave values empty to use with the AWS cloud):

* `DynamoDBLocalHost` - host or IP address of your DynamoDBLocalDB server
* `DynamoDBLocalPort` - TCP port of your DynamoDBLocalDB server

#### Example config.json settings for local use
```
{
  "PluginConfiguration": {
    "DynamoDBLocalHost": "localhost",
    "DynamoDBLocalPort": "8000",
  }
}
```
