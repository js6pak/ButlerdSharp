using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace ButlerdSharp.Generator
{
    public class Spec
    {
        [JsonProperty("requests")]
        public RequestSpec[] Requests { get; set; }

        [JsonProperty("notifications")]
        public NotificationSpec[] Notifications { get; set; }

        [JsonProperty("structTypes")]
        public StructTypeSpec[] StructTypes { get; set; }

        [JsonProperty("enumTypes")]
        public EnumTypeSpec[] EnumTypes { get; set; }
    }

    public class RequestSpec
    {
        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("doc")]
        public string Doc { get; set; }

        [JsonProperty("caller")]
        public Caller Caller { get; set; }

        [JsonProperty("params")]
        public StructSpec Params { get; set; }

        [JsonProperty("result")]
        public StructSpec Result { get; set; }
    }

    public class StructTypeSpec
    {
        [JsonProperty("doc")]
        public string Doc { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("fields")]
        public FieldSpec[] Fields { get; set; }
    }

    public class EnumTypeSpec
    {
        [JsonProperty("doc")]
        public string Doc { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("values")]
        public EnumValueSpec[] Values { get; set; }
    }

    public class EnumValueSpec
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("doc")]
        public string Doc { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }

    public class StructSpec
    {
        [JsonProperty("fields")]
        public FieldSpec[] Fields { get; set; }
    }

    public class FieldSpec
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("doc")]
        public string Doc { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("optional")]
        public bool Optional { get; set; }
    }

    public class NotificationSpec
    {
        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("doc")]
        public string Doc { get; set; }

        [JsonProperty("params")]
        public StructSpec Params { get; set; }
    }

    public enum Caller
    {
        [EnumMember(Value = "client")]
        Client,

        [EnumMember(Value = "server")]
        Server
    }
}
