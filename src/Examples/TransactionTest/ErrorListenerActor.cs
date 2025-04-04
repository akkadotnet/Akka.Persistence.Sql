// -----------------------------------------------------------------------
//  <copyright file="ErrorListenerActor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Event;

namespace TransactionTest
{
    public class ErrorListenerActor: ReceiveActor
    {
        private static readonly DateTime Epoch = new (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly StreamWriter _streamWriter;

        public ErrorListenerActor()
        {
            var timestamp = (long)(DateTime.UtcNow - Epoch).TotalSeconds;
            _streamWriter = new StreamWriter(File.Open($"error-{timestamp}.log", FileMode.Create, FileAccess.Write, FileShare.Read));
            Context.System.EventStream.Subscribe(Self, typeof(Error));
            Context.System.EventStream.Subscribe(Self, typeof(Warning));
            ReceiveAsync<Error>(
                async err =>
                {
                    await _streamWriter.WriteLineAsync(err.ToString());
                });
            ReceiveAsync<Warning>(
                async warn =>
                {
                    await _streamWriter.WriteLineAsync(warn.ToString());
                });
        }

        protected override void PostStop()
        {
            base.PostStop();
            _streamWriter.Flush();
            _streamWriter.Close();
            _streamWriter.Dispose();
        }
    }
}
