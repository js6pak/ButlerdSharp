using ButlerdSharp.Protocol.Enums;
using Newtonsoft.Json;

namespace ButlerdSharp.Protocol.Structs
{
    public class InstallQueueResult
    {
        [JsonConstructor]
        public InstallQueueResult(string id, DownloadReason reason, string caveId, Game game, Upload upload, Build build, string installFolder, string stagingFolder, string installLocationId)
        {
            Id = id;
            Reason = reason;
            CaveId = caveId;
            Game = game;
            Upload = upload;
            Build = build;
            InstallFolder = installFolder;
            StagingFolder = stagingFolder;
            InstallLocationId = installLocationId;
        }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("reason")]
        public DownloadReason Reason { get; set; }

        [JsonProperty("caveId")]
        public string CaveId { get; set; }

        [JsonProperty("game")]
        public Game Game { get; set; }

        [JsonProperty("upload")]
        public Upload Upload { get; set; }

        [JsonProperty("build")]
        public Build Build { get; set; }

        [JsonProperty("installFolder")]
        public string InstallFolder { get; set; }

        [JsonProperty("stagingFolder")]
        public string StagingFolder { get; set; }

        [JsonProperty("installLocationId")]
        public string InstallLocationId { get; set; }
    }
}
