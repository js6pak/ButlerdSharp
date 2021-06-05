using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ButlerdSharp.Protocol.Notifications;
using ButlerdSharp.Protocol.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mono.Unix;
using Newtonsoft.Json;
using StreamJsonRpc;

namespace ButlerdSharp
{
    /// <summary>
    /// Low level client for butlerd that manages updates, launching the process and jsonrpc via tcp connection
    /// </summary>
    public class ButlerdClient : IDisposable
    {
        private Process? _process;

        public ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>
        /// Path to the butlerd executable
        /// </summary>
        public string? Executable { get; set; }

        public JsonRpc? JsonRpc { get; private set; }

        public string? Version { get; private set; }

        /// <summary>
        /// Installs or upgrades butlerd executable automatically
        /// </summary>
        public async Task UpgradeButlerAsync(string directory)
        {
            // TODO This will be implemented after js6pak/butler changes are merged upstream
            // using var httpClient = new HttpClient();
            // await using var stream = await httpClient.GetStreamAsync("https://broth.itch.ovh/butler/linux-amd64/LATEST/butler.zip");
            // using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
            // zipArchive.ExtractToDirectory(directory, true);

            string fileName;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                fileName = "butler";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                fileName = "butler.exe";
            }
            else
            {
                throw new PlatformNotSupportedException("MacOS is not supported yet, buy me a mac if you want it ;)");
            }

            Directory.CreateDirectory(directory);
            Executable = Path.Combine(directory, fileName);

            if (!File.Exists(Executable))
            {
                await new WebClient().DownloadFileTaskAsync("https://github.com/js6pak/butler/releases/download/v15.21.0/" + fileName, Executable);
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var unixFileInfo = new UnixFileInfo(Executable);
                unixFileInfo.FileAccessPermissions |= FileAccessPermissions.OtherExecute | FileAccessPermissions.GroupExecute | FileAccessPermissions.UserExecute;
            }
        }

        public async Task StartAsync(string databasePath)
        {
            if (string.IsNullOrEmpty(Executable) || !File.Exists(Executable))
            {
                throw new NullReferenceException("Executable is invalid, set it either manually or call UpgradeButlerAsync");
            }

            _process = Process.Start(new ProcessStartInfo(Executable, $"--json daemon --transport tcp --keep-alive --verbose --dbpath \"{databasePath}\" --user-agent ButlerdSharp --destiny-pid {Process.GetCurrentProcess().Id}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            if (_process == null || _process.HasExited)
            {
                throw new Exception("Failed to start butlerd process");
            }

            var taskCompletionSource = new TaskCompletionSource<ListenNotificationMessage>();

            _process.OutputDataReceived += (_, args) =>
            {
                if (args.Data == null)
                    return;

                var message = JsonConvert.DeserializeObject<JsonLineMessage>(args.Data, new JsonLogMessageConverter());
                if (message == null)
                    return;

                switch (message)
                {
                    case ListenNotificationMessage listenNotificationMessage:
                    {
                        taskCompletionSource.SetResult(listenNotificationMessage);
                        break;
                    }
                    case LogMessage logMessage:
                        Logger.Log(logMessage.Level switch
                        {
                            "info" => LogLevel.Information,
                            "debug" => LogLevel.Debug,
                            _ => throw new ArgumentOutOfRangeException()
                        }, logMessage.Message);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(message));
                }
            };

            _process.BeginOutputReadLine();

            await ConnectAsync(await taskCompletionSource.Task);
        }

        private async Task ConnectAsync(ListenNotificationMessage message)
        {
            Logger.LogInformation("Connecting to {Address}", message.Tcp.Address);

            var tcpClient = new TcpClient();
            tcpClient.Connect(message.Tcp.Address);
            var stream = tcpClient.GetStream();

            JsonRpc = new JsonRpc(new NewLineDelimitedMessageHandler(stream, stream, new JsonMessageFormatter())
            {
                NewLine = NewLineDelimitedMessageHandler.NewLineStyle.Lf
            });

            JsonRpc.AddLocalRpcMethodWithParameterObject(Log.Id, new Action<Log>(log =>
            {
                Logger.Log(log.Level switch
                {
                    Protocol.Enums.LogLevel.Debug => LogLevel.Debug,
                    Protocol.Enums.LogLevel.Info => LogLevel.Information,
                    Protocol.Enums.LogLevel.Warning => LogLevel.Warning,
                    Protocol.Enums.LogLevel.Error => LogLevel.Error,
                    _ => throw new ArgumentOutOfRangeException()
                }, log.Message);
            }));

            JsonRpc.StartListening();

            var authenticateResponse = await new Requests.Meta.Authenticate.Request(message.Secret).SendAsync(JsonRpc);

            if (!authenticateResponse.Ok)
            {
                throw new Exception("Failed to authenticate");
            }

            var version = await new Requests.Version.Get.Request().SendAsync(JsonRpc);
            Version = version.Version;
        }

        public void Dispose()
        {
            _process?.Dispose();
        }
    }
}
