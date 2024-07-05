using DevSyncLib;
using DevSyncLib.Command;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace DevSyncTest.Command;

public class PacketTest
{
    [Fact]
    public void TestError()
    {
        var errorResponse = new ErrorResponse(LoggerHelper.DummyLogger) { Message = "Error message", NeedToWait = true, Recoverable = true };
        var syncException = Assert.Throws<SyncException>(() => TestPacket(errorResponse));
        Assert.Contains(errorResponse.Message, syncException.Message);
        Assert.Equal(errorResponse.NeedToWait, syncException.NeedToWait);
        Assert.Equal(errorResponse.Recoverable, syncException.Recoverable);
    }

    [Fact]
    public void TestInit()
    {
        TestPacket(new InitRequest(LoggerHelper.DummyLogger)
            { AgentOptions = new AgentOptions("path", ["*.txt"]) });
        TestPacket(new InitResponse(LoggerHelper.DummyLogger));
    }

    [Fact]
    public void TestScan()
    {
        TestPacket(new ScanRequest(LoggerHelper.DummyLogger));
        TestPacket(new ScanResponse(LoggerHelper.DummyLogger) { FileList = [new() { Path = "path", LastWriteTime = DateTime.Now, Length = 1024 }] });
    }

    private static void TestPacket(Packet packet)
    {
        var memoryStream = new MemoryStream();
        var packetStream = new PacketStream(memoryStream, memoryStream, LoggerHelper.DummyLogger);

        memoryStream.Seek(0, SeekOrigin.Begin);
        packetStream.WritePacket(packet);

        memoryStream.Seek(0, SeekOrigin.Begin);
        var readPacket = packetStream.ReadPacket();
        packet.Should().BeEquivalentTo(readPacket);
    }
}