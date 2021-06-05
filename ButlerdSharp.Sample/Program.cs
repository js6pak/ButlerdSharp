using System;
using System.Linq;
using System.Threading.Tasks;
using ButlerdSharp.Protocol.Requests;
using ButlerdSharp.Protocol.Structs;
using Serilog;
using Serilog.Extensions.Logging;
using StreamJsonRpc;

namespace ButlerdSharp.Sample
{
    /// <summary>
    /// Sample program that downloads butlerd, prompts for login and downloads the oldest build of Among Us
    /// </summary>
    internal class Program
    {
        public static async Task Main()
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var client = new ButlerdClient
            {
                Logger = new SerilogLoggerProvider().CreateLogger("ButlerdClient")
            };

            await client.UpgradeButlerAsync("./butler");
            await client.StartAsync("./butler.db");

            Log.Information("Butler version: {Version}", client.Version);

            if (client.JsonRpc == null)
                throw new NullReferenceException();

            Profile profile;
            var profiles = (await new Requests.Profile.List.Request().SendAsync(client.JsonRpc)).Profiles;

            if (profiles.Length <= 0)
            {
                Console.Write("Username: ");
                var username = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(username))
                {
                    Log.Fatal("Username can't be empty");
                    return;
                }

                Console.Write("Password: ");
                var password = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(password))
                {
                    Log.Fatal("Password can't be empty");
                    return;
                }

                Requests.Profile.LoginWithPassword.Response loginResponse;

                try
                {
                    loginResponse = await new Requests.Profile.LoginWithPassword.Request(username, password, false).SendAsync(client.JsonRpc);
                }
                catch (RemoteInvocationException e)
                {
                    Log.Fatal("Login failed: {Message}", e.Message);
                    return;
                }

                profile = loginResponse.Profile;
            }
            else if (profiles.Length == 1)
            {
                profile = profiles.Single();
            }
            else
            {
                throw new NotSupportedException("Multiple profiles are not implemented");
            }

            Log.Information("Logged in as {Username}", profile.User.Username);

            const int gameId = 257677; // Among Us

            await new Requests.Fetch.DownloadKeys.Request(profile.Id, fresh: true, filters: new FetchDownloadKeysFilter
            {
                GameId = gameId
            }).SendAsync(client.JsonRpc);

            var planResponse = await new Requests.Install.Plan.Request(gameId).SendAsync(client.JsonRpc);

            var upload = planResponse.Uploads.Single(x => x.Id == 1047908);
            var builds = (await new Requests.Fetch.UploadBuilds.Request(planResponse.Game, upload).SendAsync(client.JsonRpc)).Builds;

            var queueResponse = await new Requests.Install.Queue.Request(
                noCave: true,
                installFolder: "./game",
                stagingFolder: "./staging",
                ignoreInstallers: true,
                game: planResponse.Game,
                upload: upload,
                build: builds.Last()
            ).SendAsync(client.JsonRpc);

            await new Requests.Install.Perform.Request(Guid.NewGuid().ToString(), queueResponse.StagingFolder).SendAsync(client.JsonRpc);

            Log.Information("Done!");
        }
    }
}
