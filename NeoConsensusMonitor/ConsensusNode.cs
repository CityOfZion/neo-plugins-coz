using System;
using Neo.IO.Json;

public class ConsensusNode
{
    public string Name { get; set; }
    public string PublicKey { get; set; }
    public bool MainNetNode { get; set; }
    public bool TestNetNode { get; set; }
    public uint MonitoringStartedAt { get; set; }
    public uint LastBlock { get; set; }
    public uint TimeSpent { get; set; }
    public uint BlocksProcessed { get; set; }

    public JObject ToJson()
    {
        JObject res = new JObject();
        res["Name"] = Name;
        res["PublicKey"] = PublicKey;
        res["MonitoringStartedAt"] = MonitoringStartedAt;
        res["LastBlock"] = LastBlock;
        res["TimeSpent"] = TimeSpent;
        res["BlocksProcessed"] = BlocksProcessed;
        res["MainNetNode"] = MainNetNode;
        res["TestNetNode"] = TestNetNode;
        string average = "0.00";
        if (BlocksProcessed > 0)
        {
            decimal a = (decimal) TimeSpent / BlocksProcessed;
            average = a.ToString("0.00");
        }
        res["AverageTimeSpent"] = average;
        return res;
    }

}