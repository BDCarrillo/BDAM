using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Utils;

namespace BDAM
{
    public partial class Session
    {
        internal const ushort ServerPacketId = 65349;
        internal const ushort ClientPacketId = 65350;

        public static void SendPacketToServer(Packet packet)
        {
            var rawData = MyAPIGateway.Utilities.SerializeToBinary(packet);
            MyModAPIHelper.MyMultiplayer.Static.SendMessageToServer(ServerPacketId, rawData, true);
        }

        public static void SendPacketToClient(Packet packet, ulong client)
        {
            var rawData = MyAPIGateway.Utilities.SerializeToBinary(packet);
            MyModAPIHelper.MyMultiplayer.Static.SendMessageTo(ClientPacketId, rawData, client, true);
        }

        public static void SendPacketToClients(Packet packet, List<ulong> clients)
        {
            var rawData = MyAPIGateway.Utilities.SerializeToBinary(packet);
            foreach (var client in clients)
                MyModAPIHelper.MyMultiplayer.Static.SendMessageTo(ClientPacketId, rawData, client, true);
        }

        internal void ProcessPacket(ushort id, byte[] rawData, ulong sender, bool reliable)
        {
            try
            {
                var packet = MyAPIGateway.Utilities.SerializeFromBinary<Packet>(rawData);
                if (packet == null)
                {
                    MyLog.Default.WriteLineAndConsole(modName + $"Invalid packet - null:{packet == null}");
                    return;
                }

                //Get comps, unless it's just a notification packet
                AssemblerComp aComp = null;
                if (packet.Type != PacketType.Notification)
                {
                    if(!aCompMap.TryGetValue(packet.EntityId, out aComp)) 
                    {
                        MyLog.Default.WriteLineAndConsole(modName + $"Invalid packet - packet.EntityId {packet.EntityId} aComp null: {aComp == null}");
                        return;
                    }
                }
                if (netlogging)
                    MyLog.Default.WriteLineAndConsole(modName + $"Packet type received: {packet.Type}");

                switch (packet.Type)
                {
                    case PacketType.UpdateState:
                        var uPacket = packet as UpdateStatePacket;
                        aComp.autoControl = uPacket.AssemblerAuto;
                        if (Server)
                        {
                            var updateList = aComp.ReplicatedClients;
                            updateList.Remove(sender);
                            SendPacketToClients(new UpdateStatePacket { 
                                AssemblerAuto = aComp.autoControl, 
                                Type = PacketType.UpdateState, 
                                EntityId = aComp.assembler.EntityId }, updateList);
                        }
                        break;
                    case PacketType.Replication:
                        var rPacket = packet as ReplicationPacket;
                        if (rPacket.add)
                        {
                            aComp.ReplicatedClients.Add(sender);
                            if (netlogging)
                                MyLog.Default.WriteLineAndConsole(modName + $"Added client to replication data for aComp");

                            if (aComp.buildList.Count > 0)
                            {
                                var tempListComp = new ListComp();
                                foreach (var item in aComp.buildList.Values)
                                    tempListComp.compItems.Add(item);
                                tempListComp.auto = aComp.autoControl;
                                var data = Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(tempListComp));

                                if (netlogging)
                                    MyLog.Default.WriteLineAndConsole(modName + $"Sending initial aComp data to {sender}");

                                SendPacketToClient(new FullDataPacket
                                {
                                    EntityId = aComp.assembler.EntityId,
                                    Type = PacketType.FullData,
                                    rawData = data
                                }, sender);
                                if (aComp.missingMatAmount.Count > 0)
                                {
                                    SendPacketToClient(new MissingMatPacket
                                    {
                                        EntityId = aComp.assembler.EntityId,
                                        Type = PacketType.MissingMatData,
                                        data = aComp.missingMatAmount
                                    }, sender);
                                }
                            }
                        }
                        else
                        {
                            aComp.ReplicatedClients.Remove(sender);
                            if (netlogging)
                                MyLog.Default.WriteLineAndConsole(modName + $"Removed {sender} from aComp replication");
                        }
                        break;
                    case PacketType.Notification:
                        var nPacket = packet as NotificationPacket;
                        MyAPIGateway.Utilities.ShowMessage(modName, nPacket.Message);
                        break;
                    case PacketType.UpdateData:
                        var udPacket = packet as UpdateDataPacket;
                        var load = MyAPIGateway.Utilities.SerializeFromBinary<UpdateComp>(Convert.FromBase64String(udPacket.rawData));

                        if (Server)
                        {
                            if (netlogging)
                                MyLog.Default.WriteLineAndConsole(modName + $"Received aComp data from client - updates{load.compItemsUpdate.Count} - removals{load.compItemsRemove.Count}");
                            
                            //Actual acomp updates
                            foreach (var updated in load.compItemsUpdate)
                                aComp.buildList[BPLookup[updated.bpBase]] = new ListCompItem() { bpBase = updated.bpBase, buildAmount = updated.buildAmount, grindAmount = updated.grindAmount, priority = updated.priority, label = updated.label };
                            foreach (var removed in load.compItemsRemove)
                                aComp.buildList.Remove(BPLookup[removed]);
                            aComp.Save();

                            //Send updates out to clients
                            var updateList = aComp.ReplicatedClients;
                            updateList.Remove(sender);
                            SendPacketToClients(new UpdateDataPacket{
                                rawData = udPacket.rawData,
                                Type = PacketType.UpdateState,
                                EntityId = aComp.assembler.EntityId }, updateList);
                        }
                        else
                        {
                            if (netlogging)
                                MyLog.Default.WriteLineAndConsole(modName + $"Received aComp data from server");

                            foreach (var updated in load.compItemsUpdate)
                                aComp.buildList[BPLookup[updated.bpBase]] = new ListCompItem() { bpBase = updated.bpBase, buildAmount = updated.buildAmount, grindAmount = updated.grindAmount, priority = updated.priority, label = updated.label };
                            foreach (var removed in load.compItemsRemove)
                                aComp.buildList.Remove(BPLookup[removed]);
                        }
                        break;
                    case PacketType.FullData:
                        var fdPacket = packet as FullDataPacket;
                        if(Client)
                        {
                            var loadFD = MyAPIGateway.Utilities.SerializeFromBinary<ListComp>(Convert.FromBase64String(fdPacket.rawData));
                            if (netlogging)
                                MyLog.Default.WriteLineAndConsole(modName + $"Received initial aComp data from server");

                            if (logging && aComp.buildList.Count > 0)
                                MyLog.Default.WriteLineAndConsole(modName + $"Client received a full data set for an aComp but buildList.count > 0");

                            aComp.buildList.Clear();

                            foreach (var saved in loadFD.compItems)
                            {
                                aComp.buildList.Add(BPLookup[saved.bpBase], new ListCompItem() { bpBase = saved.bpBase, buildAmount = saved.buildAmount, grindAmount = saved.grindAmount, priority = saved.priority, label = saved.label });
                            }
                            aComp.autoControl = loadFD.auto;
                        }
                        break;
                    case PacketType.MissingMatData:
                        var mmPacket = packet as MissingMatPacket;
                        aComp.missingMatAmount = mmPacket.data;
                        if (netlogging)
                            MyLog.Default.WriteLineAndConsole(modName + $"Received missing mat data from server");
                        break;
                    default:
                        MyLog.Default.WriteLineAndConsole(modName + $"Invalid packet type - {packet.GetType()}");
                        break;
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole(modName + $"Exception in ProcessPacket: {ex}");
            }
        }
    }

    [ProtoContract]
    [ProtoInclude(100, typeof(UpdateStatePacket))]
    [ProtoInclude(200, typeof(NotificationPacket))]
    [ProtoInclude(300, typeof(ReplicationPacket))]
    [ProtoInclude(400, typeof(UpdateDataPacket))]
    [ProtoInclude(500, typeof(FullDataPacket))]
    [ProtoInclude(600, typeof(MissingMatPacket))]


    public class Packet
    {
        [ProtoMember(1)] internal long EntityId;
        [ProtoMember(2)] internal PacketType Type;
    }

    [ProtoContract]
    public class UpdateStatePacket : Packet
    {
        [ProtoMember(4)] internal bool AssemblerAuto;
    }

    [ProtoContract]
    public class NotificationPacket : Packet
    {
        [ProtoMember(4)] internal string Message;
    }

    [ProtoContract]
    public class ReplicationPacket : Packet
    {
        [ProtoMember(4)] internal bool add;
    }

    [ProtoContract]
    public class UpdateDataPacket : Packet
    {
        [ProtoMember(4)] internal string rawData;
    }

    [ProtoContract]
    public class FullDataPacket : Packet
    {
        [ProtoMember(4)] internal string rawData;
    }

    [ProtoContract]
    public class MissingMatPacket : Packet
    {
        [ProtoMember(4)] internal Dictionary<string, int> data;
    }

    public enum PacketType
    {
        UpdateState,
        Notification,
        Replication,
        UpdateData,
        FullData,
        MissingMatData,
    }
}
