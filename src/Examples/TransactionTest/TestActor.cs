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

public class TestActor: ReceivePersistentActor
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private int _currentIndex;
    private byte[]? _payload;
    
    public TestActor(string persistenceId, int payloadSize, IHostApplicationLifetime applicationLifetime)
    {
        PersistenceId = persistenceId;
        _applicationLifetime = applicationLifetime;

        var log = Context.GetLogger();
        
        Recover<SnapshotOffer>(offer => _payload = (byte[])offer.Snapshot);
        Recover<byte[]>(bytes => _payload = bytes);
        Recover<RecoveryCompleted>(
            _ =>
            {
                log.Info("Recovery Completed");
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
        Command<SaveEvent>(_ =>
            {
                Persist(_payload,
                    _ =>
                    {
                        _currentIndex++;
                        if (_currentIndex % 5 == 0)
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
        Command<DeleteMessagesSuccess>(
            _ =>
            {
                log.Info("Messages deleted");
            });
        Command<DeleteMessagesFailure>(
            fail =>
            {
                log.Error(fail.Cause, "Failed to delete messages");
                _applicationLifetime.StopApplication();
            });
    }
    
    public override string PersistenceId { get; }
}
