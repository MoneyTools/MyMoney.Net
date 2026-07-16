using System;
using System.Threading.Tasks;
using Grpc.Core;
using Google.Protobuf;
using Walkabout.PerformanceProvider.Grpc;

namespace PerformanceViewer
{

    // gRPC server integration (Grpc.Core - lower level, no ASP.NET Core)
    internal class PerformanceServer
    {
        private readonly PerformanceViewer.MainWindow window;
        private Server server;

        public PerformanceServer(PerformanceViewer.MainWindow window)
        {
            this.window = window;
        }

        public void Start()
        {
            if (server != null) return;

            // Create protobuf marshallers using Serialization/Deserialization contexts (buffer-friendly)
            var performanceMarshaller = Marshallers.Create<Walkabout.PerformanceProvider.Grpc.PerformanceMessage>(
                (message, context) =>
                {
#if !GRPC_DISABLE_PROTOBUF_BUFFER_SERIALIZATION
                    if (message is IBufferMessage)
                    {
                        context.SetPayloadLength(message.CalculateSize());
                        MessageExtensions.WriteTo(message, context.GetBufferWriter());
                        context.Complete();
                        return;
                    }
#endif
                    context.Complete(MessageExtensions.ToByteArray(message));
                },
                ctx =>
                {
#if !GRPC_DISABLE_PROTOBUF_BUFFER_SERIALIZATION
                    return Walkabout.PerformanceProvider.Grpc.PerformanceMessage.Parser.ParseFrom(ctx.PayloadAsReadOnlySequence());
#else
                    return Walkabout.PerformanceProvider.Grpc.PerformanceMessage.Parser.ParseFrom(ctx.PayloadAsNewBuffer());
#endif
                });

            var ackMarshaller = Marshallers.Create<Walkabout.PerformanceProvider.Grpc.Ack>(
                (message, context) =>
                {
#if !GRPC_DISABLE_PROTOBUF_BUFFER_SERIALIZATION
                    if (message is IBufferMessage)
                    {
                        context.SetPayloadLength(message.CalculateSize());
                        MessageExtensions.WriteTo(message, context.GetBufferWriter());
                        context.Complete();
                        return;
                    }
#endif
                    context.Complete(MessageExtensions.ToByteArray(message));
                },
                ctx =>
                {
#if !GRPC_DISABLE_PROTOBUF_BUFFER_SERIALIZATION
                    return Walkabout.PerformanceProvider.Grpc.Ack.Parser.ParseFrom(ctx.PayloadAsReadOnlySequence());
#else
                    return Walkabout.PerformanceProvider.Grpc.Ack.Parser.ParseFrom(ctx.PayloadAsNewBuffer());
#endif
                });

            // Define the unary method
            var sendPerformanceMethod = new Method<Walkabout.PerformanceProvider.Grpc.PerformanceMessage, Walkabout.PerformanceProvider.Grpc.Ack>(
                MethodType.Unary,
                "PerformanceService",
                "SendPerformance",
                performanceMarshaller,
                ackMarshaller);

            // Build service definition and handler
            var serviceDef = ServerServiceDefinition.CreateBuilder()
                .AddMethod(sendPerformanceMethod, async (request, context) =>
                {
                    var ts = request.Timestamp?.ToDateTime() ?? DateTime.UtcNow;
                    _ = window.Dispatcher.BeginInvoke(new Action(() => window.OnEventCaptured(request.EventId, request.Component, request.Category, request.Measurement, request.Ticks, request.Size, request.Rate, ts)));
                    return await Task.FromResult(new Walkabout.PerformanceProvider.Grpc.Ack { Message = "ok" });
                })
                .Build();

            server = new Server
            {
                Services = { serviceDef },
                Ports = { new ServerPort("localhost", 50051, ServerCredentials.Insecure) }
            };

            server.Start();
            window.Dispatcher.BeginInvoke(new Action(() => window.UpdateServerStatus(true, 0)));
        }

        public void Stop()
        {
            try
            {
                server?.ShutdownAsync().Wait();
            }
            catch { }
            server = null;
            window.Dispatcher.BeginInvoke(new Action(() => window.UpdateServerStatus(false, 0)));
        }
    }


}
