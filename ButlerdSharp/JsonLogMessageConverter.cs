using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ButlerdSharp
{
    public class JsonLogMessageConverter : JsonConverter<JsonLineMessage>
    {
        public override void WriteJson(JsonWriter writer, JsonLineMessage value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }

        public override JsonLineMessage ReadJson(JsonReader reader, Type objectType, JsonLineMessage existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);

            if (!jObject.TryGetValue("type", out var typeProperty))
            {
                throw new JsonException();
            }

            var type = typeProperty.Value<string>() switch
            {
                "butlerd/listen-notification" => typeof(ListenNotificationMessage),
                "log" => typeof(LogMessage),
                _ => throw new ArgumentOutOfRangeException()
            };

            return (JsonLineMessage) jObject.ToObject(type);
        }
    }
}
