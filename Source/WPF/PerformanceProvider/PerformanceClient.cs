using System.Diagnostics;
using Grpc.Net.Client;
using Walkabout.PerformanceProvider.Grpc;
using System.Collections.Concurrent;
//using Google.Protobuf.WellKnownTypes;

namespace Walkabout.PerformanceProvider
{
    public class PerformanceClient : IDisposable
    {
        private GrpcChannel channel;
        private PerformanceService.PerformanceServiceClient client;
        private CancellationTokenSource source = new CancellationTokenSource();
        private bool disposedValue;
        private const string name = "PerformanceProvider";
        private ConcurrentQueue<PerformanceMessage> queue = new ConcurrentQueue<PerformanceMessage>();
        private bool connecting;
        private bool sending;
        public static string ServerAddress = "http://localhost:50051";

        internal static PerformanceClient Instance;

        public PerformanceClient()
        {
            Instance = this;
        }

        public async Task Start()
        {
            // Connect to the PerformanceViewer, and cache any messages until we are connected
            // so we don't lose any critical startup performance data.
            try
            {
                await Task.CompletedTask;
                this.connecting = true;
                // connect to local gRPC server hosted by PerformanceViewer
                this.channel = GrpcChannel.ForAddress(ServerAddress);
                this.client = new PerformanceService.PerformanceServiceClient(this.channel);
                Debug.WriteLine("PerformanceClient: Connected to PerformanceViewer (gRPC)");
                this.connecting = false;
            } 
            catch (Exception ex)
            {
                this.connecting = false;
                Debug.WriteLine("PerformanceClient: Could not connect to PerformanceViewer: " + ex.Message);
            }
        }

        public async void Send(PerformanceMessage message)
        {
            if (this.connecting || this.sending)
            {
                this.queue.Enqueue(message);
                return;
            }
            if (this.client != null)
            {
                try
                {
                    sending = true;
                    while (this.queue.TryDequeue(out PerformanceMessage qm))
                    {
                        await this.client.SendPerformanceAsync(qm);
                    }
                    var response = await this.client.SendPerformanceAsync(message);
                    Debug.WriteLine(response.Message);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("PerformanceClient: Could not send message to PerformanceViewer (gRPC): " + ex.Message);
                    this.Close();
                }
                finally
                {
                    sending = false;
                }
            }
        }

        private void Close()
        {
            if (this.source != null)
            {
                this.source.Dispose();
                this.source = null;
            }
            if (this.channel != null)
            {
                this.channel.Dispose();
                this.channel = null;
            }
            this.client = null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    this.Close();
                }

                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        ~PerformanceClient()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
