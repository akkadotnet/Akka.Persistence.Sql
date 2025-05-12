// -----------------------------------------------------------------------
//  <copyright file="TestActor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Event;
using Akka.Persistence;
using Microsoft.Extensions.Hosting;

namespace TransactionTest;

public sealed class SaveEvent
{
    public static readonly SaveEvent Instance = new ();
    private SaveEvent() { }
}

public sealed class Initialize
{
    public static readonly Initialize Instance = new ();
    private Initialize() { }
}

public sealed class Initialized
{
    public static readonly Initialized Instance = new ();
    private Initialized() { }
}

public class TestActor: ReceivePersistentActor
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILoggingAdapter _log;
    private int _currentIndex;
    private byte[]? _payload;
    
    public TestActor(string persistenceId, int payloadSize, IHostApplicationLifetime applicationLifetime)
    {
        PersistenceId = persistenceId;
        _applicationLifetime = applicationLifetime;

        _log = Context.GetLogger();
        
        Recover<SnapshotOffer>(offer => _payload = (byte[])offer.Snapshot);
        Recover<byte[]>(
            bytes =>
            {
                _payload = bytes;
                _currentIndex++;
            });
        Recover<RecoveryCompleted>(_ =>
            {
                _log.Info("Recovery Completed");
                if (_payload == null)
                {
                    var rnd = new Random();
                    _payload = new byte[payloadSize];
                    for (var i = 0; i < payloadSize; i++)
                    {
                        _payload[i] = (byte)rnd.Next(0, 255);
                    }
                }
            });
        Command<Initialize>(_ => Sender.Tell(Initialized.Instance, Self));
        Command<SaveEvent>(_ =>
            {
                Persist(_payload,
                    _ =>
                    {
                        _currentIndex++;
                        if (_currentIndex % 10 == 0 || _currentIndex > 10)
                        {
                            SaveSnapshot(_payload);
                        }
                    });
            });
        Command<SaveSnapshotSuccess>(
            evt =>
            {
                DeleteMessages(evt.Metadata.SequenceNr - 1);
            });
        Command<SaveSnapshotFailure>(
            fail =>
            {
                _log.Error(fail.Cause, "Failed to save snapshot");
                // _applicationLifetime.StopApplication();
            });
        Command<DeleteMessagesSuccess>(
            _ =>
            {
                // no-op
                // log.Info("Messages deleted");
            });
        Command<DeleteMessagesFailure>(
            fail =>
            {
                _log.Error(fail.Cause, "Failed to delete messages");
                // _applicationLifetime.StopApplication();
            });
    }
    
    public override string PersistenceId { get; }

    /*
    protected override void OnPersistFailure(Exception cause, object @event, long sequenceNr)
    {
        base.OnPersistFailure(cause, @event, sequenceNr);
        if(cause is not TimeoutException)
            _applicationLifetime.StopApplication();
    }

    protected override void OnPersistRejected(Exception cause, object @event, long sequenceNr)
    {
        base.OnPersistRejected(cause, @event, sequenceNr);
        _applicationLifetime.StopApplication();
    }
    */
}
