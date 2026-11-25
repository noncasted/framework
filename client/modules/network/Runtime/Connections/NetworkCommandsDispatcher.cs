using System;
using Shared;

namespace Internal
{
    public class NetworkCommandsDispatcher
    {
        public NetworkCommandsDispatcher(
            INetworkCommandsCollection commands,
            INetworkConnection connection)
        {
            _commands = commands;
            _connection = connection;
        }

        private static readonly ILog _log = Logs.Of<NetworkCommandsDispatcher>();

        private readonly INetworkCommandsCollection _commands;
        private readonly INetworkConnection _connection;

        public void Run(IReadOnlyLifetime lifetime)
        {
            var reader = _connection.Reader;
            var writer = _connection.Writer;

            reader.OneWay.Advise(lifetime, response => {
                var context = response.Context;
                var command = _commands.Get(context);

                try
                {
                    command.Execute(lifetime, context);
                }
                catch (Exception e)
                {
                    _log.Exception(e);
                }
            });

            // As on the backend: every request of the server is answered, with ErrorResponse when there is no command
            // for it or the command throws - the server turns it into an exception instead of waiting.
            reader.Request.Advise(lifetime, request => {
                var context = request.Context;

                if (_commands.TryGet(context, out var command) == false)
                {
                    _log.Error("No command for the server request {Request}", context.GetType().FullName);
                    writer.WriteResponse(ErrorResponse.UnknownCommand(context), request.RequestId);
                    return;
                }

                INetworkContext response;

                try
                {
                    response = command.Execute(lifetime, context);
                }
                catch (Exception e)
                {
                    _log.Exception(e);
                    response = ErrorResponse.Create(ErrorCode.Internal, context, e);
                }

                writer.WriteResponse(response, request.RequestId);
            });

            reader.Response.Advise(lifetime, response => {
                var context = response.Context;
                writer.OnRequestHandled(context, response.RequestId);
            });
        }
    }
}