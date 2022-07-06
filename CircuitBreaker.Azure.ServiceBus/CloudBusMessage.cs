using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CircuitBreaker.Azure.ServiceBus
{
    public class CloudBusMessage {
        public virtual string MessageType { get; set; }
        
        [JsonExtensionData]
        public virtual Dictionary<string, JsonElement> EventData { get; set; }
    }
}