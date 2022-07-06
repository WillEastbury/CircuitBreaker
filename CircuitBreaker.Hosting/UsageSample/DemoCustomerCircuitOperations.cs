using CircuitBreaker.Azure.ServiceBus;
using CircuitBreaker.Http;
using CircuitBreaker.Core.Enums;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using CircuitBreaker.Custom.DTO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;

namespace CircuitBreaker.Custom.CircuitOperations
{
    public class QuotedIntConverter : JsonConverter<Int32>
    {
        public override Int32 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return Convert.ToInt32(reader.GetString());
            }
            else
            {
                return reader.GetInt32();
            }
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
    public class QuotedDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return Convert.ToDecimal(reader.GetString());
            }
            else
            {
                return reader.GetDecimal();
            }
        }

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
    public class DemoCustomerCircuitOperations : HttpCircuitOperations{
        private JsonSerializerOptions GetOptions(){
            var _d = new JsonSerializerOptions(){Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)}};
            _d.Converters.Add(new QuotedIntConverter());
            _d.Converters.Add(new QuotedDecimalConverter());
            _d.PropertyNameCaseInsensitive = true;
            return _d;
        }
        public DemoCustomerCircuitOperations(ILogger<HttpCircuitOperations> logger, IHttpClientFactory clientfactory) : base(logger, clientfactory){}
        public override async Task<RequestStatusType> ProcessStandardOperationalMessageAsync(string message, string ExtraHeaderInfo){
            // Decode the payload from the ingress service here and do something with it -- Deserialize the CloudMessage
            CloudBusMessage cloudMessage = JsonSerializer.Deserialize<CloudBusMessage>(message);
            switch (cloudMessage.MessageType) 
            {
                case "StockUpdate":
                    StockUpdate stockUpdate = JsonSerializer.Deserialize<StockUpdate>((cloudMessage.EventData["EventData"].GetRawText()), GetOptions());
                    return await FireRequestBehindBreakerAsync(
                        "OperationalClient", 
                        $"/Stock/{stockUpdate.StockLocation}-{stockUpdate.StockCode}", 
                        HttpMethod.Post,JsonSerializer.Serialize(stockUpdate), ExtraHeaderInfo: ExtraHeaderInfo
                    );

                case "PriceUpdate":
                    PriceUpdate priceUpdate = JsonSerializer.Deserialize<PriceUpdate>((cloudMessage.EventData["EventData"].GetRawText()), GetOptions());
                    return await FireRequestBehindBreakerAsync(
                        "OperationalClient",  $"/Price/{priceUpdate.StockCode}", HttpMethod.Post, 
                        JsonSerializer.Serialize(priceUpdate), ExtraHeaderInfo: ExtraHeaderInfo
                    );
   
            }
            return RequestStatusType.Failure; // We shouldn't get to here, but if we do, return a failure
        }
    }
}