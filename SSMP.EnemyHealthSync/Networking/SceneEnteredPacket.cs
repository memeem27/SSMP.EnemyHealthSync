using SSMP.Networking.Packet;

namespace SSMPEnemyHealthSync.Networking;

public sealed class SceneEnteredPacket : IPacketData
{
    public bool IsReliable => true;

    public bool DropReliableDataIfNewerExists => true;

    public string SceneName { get; set; } = "";

    public void WriteData(IPacket packet)
    {
        packet.Write(SceneName ?? "");
    }

    public void ReadData(IPacket packet)
    {
        SceneName = packet.ReadString();
    }
}
