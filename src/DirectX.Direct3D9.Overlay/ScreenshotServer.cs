using System;
using System.Threading;
using System.Threading.Tasks;
using CoreHook.IPC.NamedPipes;
using CoreHook.IPC.Platform;
using CoreHook.IPC.Transport;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;
using JsonRpc.Streams;

namespace DirectX.Direct3D9.Overlay
{
    public class ScreenshotSessionFeature
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>
        /// Gets the <see cref="CancellationToken"/> that can stop the server.
        /// </summary>
        public CancellationToken CancellationToken => _cts.Token;

        /// <summary>
        /// Stops the server.
        /// </summary>
        public void StopServer()
        {
            _cts.Cancel();
        }
    }

    internal class ScreenshotServer
    {
        private readonly Type _service;
        private string _pipeName;
        private readonly Func<RequestContext, Func<Task>, Task> _handler;
        private static Thread _rpcServerThread;
        private readonly ScreenshotSessionFeature _session = new ScreenshotSessionFeature();
        private Direct3DHookModule _hookModule;

        internal ScreenshotServer(Type service, Func<RequestContext, Func<Task>, Task> handler, Direct3DHookModule hookModule)
        {
            _service = service;
            _handler = handler;
            _hookModule = hookModule;
        }

        private static readonly IJsonRpcContractResolver MyContractResolver = new JsonRpcContractResolver
        {
            // Use camelcase for RPC method names.
            NamingStrategy = new CamelCaseJsonRpcNamingStrategy(),
            // Use camelcase for the property names in parameter value objects
            ParameterValueConverter = new CamelCaseJsonValueConverter()
        };
        public IJsonRpcServiceHost BuildServiceHost(Type service)
        {
            var builder = new JsonRpcServiceHostBuilder
            {
                ContractResolver = MyContractResolver
            };

            builder.Register(service);

            builder.Intercept(_handler);

            return builder.Build();
        }
        public static void CreateRpcService(
            string namedPipeName,
            IPipePlatform pipePlatform,
            Type rpcService,
            Func<RequestContext, Func<Task>, Task> handler,
            Direct3DHookModule hookModule)
        {
            var service = new ScreenshotServer(rpcService, handler, hookModule);

            _rpcServerThread = new Thread(() => service.CreateServer(namedPipeName, pipePlatform))
            {
                IsBackground = true
            };
            _rpcServerThread.Start();
        }

        private INamedPipe CreateServer(string namedPipeName, IPipePlatform pipePlatform)
        {
            _pipeName = namedPipeName;
            return NamedPipeServer.StartNewServer(namedPipeName, pipePlatform, HandleTransportConnection);
        }

        public void HandleTransportConnection(ITransportChannel channel)
        {
            Console.WriteLine($"Connection received from pipe {_pipeName}.");

            var serverStream = channel.Connection.Stream;

            IJsonRpcServiceHost host = BuildServiceHost(_service);

            var serverHandler = new StreamRpcServerHandler(host);

            serverHandler.DefaultFeatures.Set(_session);

            using (var reader = new ByLineTextMessageReader(serverStream))
            using (var writer = new ByLineTextMessageWriter(serverStream))
            using (serverHandler.Attach(reader, writer))
            {
                _session.CancellationToken.WaitHandle.WaitOne();
            }
        }
    }
}
