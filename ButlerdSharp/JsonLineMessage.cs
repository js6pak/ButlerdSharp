using System;
using System.Net;
using Newtonsoft.Json;

namespace ButlerdSharp
{
    public abstract class JsonLineMessage
    {
        [JsonConstructor]
        protected JsonLineMessage(string type)
        {
            Type = type;
        }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class LogMessage : JsonLineMessage
    {
        [JsonConstructor]
        public LogMessage(string type, string level, string message) : base(type)
        {
            Level = level;
            Message = message;
        }

        [JsonProperty("level")]
        public string Level { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class ListenNotificationMessage : JsonLineMessage
    {
        [JsonConstructor]
        public ListenNotificationMessage(string type, string secret, Endpoint tcp) : base(type)
        {
            Secret = secret;
            Tcp = tcp;
        }

        [JsonProperty("secret")]
        public string Secret { get; set; }

        [JsonProperty("tcp")]
        public Endpoint Tcp { get; set; }

        public class Endpoint
        {
            [JsonConstructor]
            public Endpoint(IPEndPoint address)
            {
                Address = address;
            }

            [JsonProperty("address")]
            [JsonConverter(typeof(IpEndPointConverter))]
            public IPEndPoint Address { get; set; }
        }
    }

    public class IpEndPointConverter : JsonConverter<IPEndPoint>
    {
        public override void WriteJson(JsonWriter writer, IPEndPoint value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override IPEndPoint ReadJson(JsonReader reader, Type objectType, IPEndPoint existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var address = ((string) reader.Value).Split(':');
            return new IPEndPoint(IPAddress.Parse(address[0]), int.Parse(address[1]));
        }
    }
}
