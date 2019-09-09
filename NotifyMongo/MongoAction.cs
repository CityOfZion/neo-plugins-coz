using System.Collections.Generic;

public class MongoAction {
    public string OnNotification { get; set; }
    public int Keyindex { get; set; }
    public string Action { get; set; }
    public string Collection { get; set; }
    public Dictionary<string, string>[] Schema { get; set; }
}

