using ButlerdSharp.Protocol.Enums;
using Newtonsoft.Json;

namespace ButlerdSharp.Protocol.Structs
{
    public class StrategyResult
    {
        [JsonConstructor]
        public StrategyResult(LaunchStrategy strategy, string fullTargetPath, Candidate candidate)
        {
            Strategy = strategy;
            FullTargetPath = fullTargetPath;
            Candidate = candidate;
        }

        [JsonProperty("strategy")]
        public LaunchStrategy Strategy { get; set; }

        [JsonProperty("fullTargetPath")]
        public string FullTargetPath { get; set; }

        [JsonProperty("candidate")]
        public Candidate Candidate { get; set; }
    }
}
