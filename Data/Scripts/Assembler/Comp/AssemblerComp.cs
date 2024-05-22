using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VRage;
using VRage.Utils;

namespace BDAM
{
    internal partial class AssemblerComp
    {
        private Session _session;
        internal IMyAssembler assembler;
        internal int runStartTick;
        internal int runStopTick;
        internal bool inputJammed;
        internal bool outputJammed;
        internal bool autoControl = false;
        internal MyProductionQueueItem lastQueue;
        internal Dictionary<MyBlueprintDefinitionBase, ListCompItem> buildList = new Dictionary<MyBlueprintDefinitionBase, ListCompItem>();
        internal GridComp gridComp;
        internal List<string> missingMatTypes = new List<string>();
        internal Dictionary<MyBlueprintDefinitionBase, Dictionary<string, MyFixedPoint>> missingMatQueue = new Dictionary<MyBlueprintDefinitionBase, Dictionary<string, MyFixedPoint>>();
        internal List<ulong> ReplicatedClients = new List<ulong>();

        //Temps for networking updates/removals
        internal List<ListCompItem> tempUpdateList = new List<ListCompItem>();
        internal List<string> tempRemovalList = new List<string>();

        //Stats tracking
        internal int unJamAttempts = 0;
        internal int countStart = 0;
        internal int countStop = 0;

        internal void Init(IMyAssembler Assembler, GridComp comp, Session session)
        {
            _session = session;
            gridComp = comp;
            assembler = Assembler;
            Session.aCompMap.Add(assembler.EntityId, this);
            //TODO look at grid change events
            
            if (Session.Server)
            {
                if(!assembler.IsQueueEmpty)
                {
                    var queue = assembler.GetQueue();
                    lastQueue = queue[0];
                }

                assembler.StoppedProducing += Assembler_StoppedProducing;
                assembler.StartedProducing += Assembler_StartedProducing;
                //Check/init storage
                if (assembler.Storage == null)
                {
                    assembler.Storage = new MyModStorageComponent { [_session.storageGuid] = "" };
                    if (Session.logging)
                        MyLog.Default.WriteLineAndConsole($"{Session.modName} Storage was null, initting for {assembler.DisplayNameText}");
                }
                else if (!assembler.Storage.ContainsKey(_session.storageGuid))
                {
                    assembler.Storage[_session.storageGuid] = "";
                    if (Session.logging)
                        MyLog.Default.WriteLineAndConsole($"{Session.modName} Storage not null, but no matching GUID for  {assembler.DisplayNameText}");
                }
                else if (assembler.Storage.ContainsKey(_session.storageGuid))
                {
                    //Serialize storage
                    string rawData;
                    if (assembler.Storage.TryGetValue(_session.storageGuid, out rawData))
                    {
                        try
                        {
                            if (rawData != null && rawData.Length > 0)
                            {
                                var load = MyAPIGateway.Utilities.SerializeFromBinary<ListComp>(Convert.FromBase64String(rawData));

                                //Serialized string to BPs
                                foreach (var saved in load.compItems)
                                {
                                    if(Session.BPLookup.ContainsKey(saved.bpBase))
                                        buildList.Add(Session.BPLookup[saved.bpBase], new ListCompItem() { bpBase = saved.bpBase, buildAmount = saved.buildAmount, grindAmount = saved.grindAmount, priority = saved.priority, label = saved.label });
                                }
                                autoControl = load.auto;
                                if (Session.logging)
                                    MyLog.Default.WriteLineAndConsole($"{Session.modName} Loaded storage for {assembler.DisplayNameText} items found: {load.compItems.Count}  auto: {load.auto}");
                            }
                            else if (Session.logging)
                                MyLog.Default.WriteLineAndConsole($"{Session.modName} Storage found but empty for {assembler.DisplayNameText}");
                        }
                        catch (Exception e)
                        {
                            if (Session.logging)
                                MyLog.Default.WriteLineAndConsole($"{Session.modName} Error reading storage for {assembler.DisplayNameText} {e}");
                        }
                    }
                    else if (Session.logging)
                        MyLog.Default.WriteLineAndConsole($"{Session.modName} Storage found but empty for {assembler.DisplayNameText}");
                }
            }
            else if (Session.Client && Session.MPActive)
            {
                if (Session.netlogging)
                    MyLog.Default.WriteLineAndConsole(Session.modName + $"Updating replication list on server - addition");
                Session.SendPacketToServer(new ReplicationPacket { EntityId = assembler.EntityId, add = true, Type = PacketType.Replication });
            }
        }

        public void AssemblerUpdate()
        {
            if (assembler.CooperativeMode)
            {
                if (Session.logging)
                    MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " mode switched to Cooperative, turning off auto control");
                autoControl = false;
                return;
            }

            if (!assembler.IsQueueEmpty)
            {
                var queue = assembler.GetQueue();
                if (Session.logging)
                    MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + $" Update check Queue: {queue[0].Blueprint.Id.SubtypeName} - {queue[0].Amount}  Last: {lastQueue.Blueprint.Id.SubtypeName} - {lastQueue.Amount}");
                
                //Jam check due to missing mats
                if (lastQueue.Blueprint == queue[0].Blueprint && lastQueue.Amount == queue[0].Amount && assembler.CurrentProgress == 0)
                {
                    ListCompItem lComp;
                    if (buildList.TryGetValue((MyBlueprintDefinitionBase)queue[0].Blueprint, out lComp))
                    {
                        lComp.missingMats = true;
                        var bp = (MyBlueprintDefinitionBase)queue[0].Blueprint;
                        foreach (var item in bp.Prerequisites)
                        {
                            if ((!gridComp.inventoryList.ContainsKey(item.Id.SubtypeName) || gridComp.inventoryList[item.Id.SubtypeName] < item.Amount) && !missingMatTypes.Contains(item.Id.SubtypeName))
                            {
                                missingMatTypes.Add(item.Id.SubtypeName);

                                var adjustedAmount = item.Amount * Session.assemblerEfficiency;
                                if (!missingMatQueue.ContainsKey(bp))
                                    missingMatQueue.Add(bp, new Dictionary<string, MyFixedPoint>() { { item.Id.SubtypeName, adjustedAmount } });
                                else
                                    missingMatQueue[bp].Add(item.Id.SubtypeName, adjustedAmount);
                                var message = gridComp.Grid.DisplayName + ": " + assembler.CustomName + $" missing {item.Id.SubtypeName} for {queue[0].Blueprint.Id.SubtypeName}";
                                if (Session.MPActive) //Send notification
                                {
                                    var steamID = MyAPIGateway.Multiplayer.Players.TryGetSteamId(assembler.OwnerId);
                                    if (steamID > 0)
                                        Session.SendPacketToClient(new NotificationPacket { Message = message, Type = PacketType.Notification }, steamID);
                                    MyLog.Default.WriteLineAndConsole($"Missing mats: ownerID{assembler.OwnerId} steam {steamID}");
                                }
                                else
                                    MyAPIGateway.Utilities.ShowMessage(Session.modName, message);

                                if (Session.logging)
                                    MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName +$" Missing {item.Amount} ({adjustedAmount}) {item.Id.SubtypeName} for {queue[0].Blueprint.Id.SubtypeName}");
                            }
                        }
                        assembler.RemoveQueueItem(0, queue[0].Amount);

                        if (Session.logging)
                            MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + $" same item/qty found in queue, missing mats for {queue[0].Blueprint.Id.SubtypeName}.  Progress: {assembler.CurrentProgress}");
                    }
                    else if (Session.logging)
                        MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + $" manually added {queue[0].Blueprint.Id.SubtypeName} missing mats, removed from queue ");
                }

                if (Session.logging)
                    MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " quick check - items in queue");
                return;
            }


            //Missing mat list checks
            //TODO look at dampening update cadence of this?
            if(missingMatTypes.Count > 0)
                foreach (var type in missingMatTypes.ToArray())
                {
                    //Check inv list for missing mat type
                    if(gridComp.inventoryList.ContainsKey(type))
                    {
                        var amount = gridComp.inventoryList[type];
                        bool remove = true;
                        foreach (var queue in missingMatQueue.ToArray())
                        {
                            if (queue.Value.ContainsKey(type))
                            {
                                remove = false;
                                if (queue.Value[type] <= amount)
                                {
                                    queue.Value.Remove(type);
                                    if (queue.Value.Count == 0)
                                    {
                                        ListCompItem lComp;
                                        if (buildList.TryGetValue(queue.Key, out lComp))
                                        {
                                            lComp.missingMats = false;
                                        }
                                        missingMatQueue.Remove(queue.Key);
                                        if (Session.logging)
                                            MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + $" removed {lComp.label} from missing mat list");
                                    }
                                }
                            }
                        }
                        if (remove)
                            missingMatTypes.Remove(type);
                    }
                }

            //Assemble/disassemble mode and iterations to see if something can be queued
            if (assembler.Mode == Sandbox.ModAPI.Ingame.MyAssemblerMode.Assembly)
            {
                if (!AssemblerTryBuild())
                    AssemblerTryGrind();
            }
            else
            {
                if (!AssemblerTryGrind())
                    AssemblerTryBuild();
            }
        }

        public bool AssemblerTryBuild()
        {
            var timer = new Stopwatch();
            timer.Start();
            if (Session.logging)
                MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " checking for buildable items");
            for (int i = 1; i < 4; i++)
                foreach (var listItem in buildList)
                {
                    var lComp = listItem.Value;
                    if (lComp.missingMats && lComp.buildAmount == -1)
                        lComp.missingMats = false;
                    if (lComp.priority != i || lComp.buildAmount == -1 || lComp.buildAmount <= lComp.grindAmount || lComp.missingMats)
                        continue;
                    var itemName = listItem.Key.Results[0].Id.SubtypeName;
                    MyFixedPoint amountAvail = 0;
                    gridComp.inventoryList.TryGetValue(itemName, out amountAvail);
                    var amountNeeded = lComp.buildAmount - amountAvail;
                    if (amountNeeded > 0)
                    {
                        var queueAmount = amountNeeded > Session.maxQueueAmount ? Session.maxQueueAmount : amountNeeded;
                        if (assembler.Mode == Sandbox.ModAPI.Ingame.MyAssemblerMode.Disassembly)
                            assembler.Mode = Sandbox.ModAPI.Ingame.MyAssemblerMode.Assembly;
                        assembler.AddQueueItem(listItem.Key, queueAmount);
                        lastQueue = new MyProductionQueueItem() { Blueprint = listItem.Key, Amount = queueAmount };
                        if (Session.logging)
                            MyLog.Default.WriteLineAndConsole($"{Session.modName}Queued build {queueAmount} of {itemName}.  On-hand {amountAvail}  Target {listItem.Value.buildAmount}");
                        return true;
                    }
                }
            timer.Stop();
            if (Session.logging)
                MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + $" no buildable items found runtime: {timer.Elapsed.TotalMilliseconds}");
            return false;
        }
        public bool AssemblerTryGrind()
        {
            var timer = new Stopwatch();
            timer.Start();
            if (Session.logging)
                MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " checking for grindable items");
            for (int i = 1; i < 4; i++)
                foreach (var listItem in buildList)
                {
                    var lComp = listItem.Value;
                    if (lComp.priority != i || lComp.grindAmount == -1 || lComp.grindAmount <= lComp.buildAmount )
                        continue;
                    var itemName = listItem.Key.Results[0].Id.SubtypeName;
                    if (gridComp.inventoryList.ContainsKey(itemName))
                    {
                        var amountAvail = gridComp.inventoryList[itemName];
                        var amountExcess = amountAvail - lComp.grindAmount;
                        if (amountExcess > 0)
                        {
                            var queueAmount = amountExcess > Session.maxQueueAmount ? Session.maxQueueAmount : amountExcess;
                            if (assembler.Mode == Sandbox.ModAPI.Ingame.MyAssemblerMode.Assembly)
                                assembler.Mode = Sandbox.ModAPI.Ingame.MyAssemblerMode.Disassembly;
                            assembler.AddQueueItem(listItem.Key, queueAmount);
                            if (Session.logging)
                                MyLog.Default.WriteLineAndConsole($"{Session.modName}Queued grind {queueAmount} of {itemName}.  On-hand {amountAvail}  Target {listItem.Value.grindAmount}");
                            return true;
                        }
                    }
                }
            timer.Stop();
            if (Session.logging)
                MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + $" no grindable items found runtime: {timer.Elapsed.TotalMilliseconds}");
            return false;
        }

        public void Save()
        {
            if (Session.Server)
            {
                var tempListComp = new ListComp();
                foreach (var item in buildList.Values)
                    tempListComp.compItems.Add(item);
                tempListComp.auto = autoControl;
                var binary = MyAPIGateway.Utilities.SerializeToBinary(tempListComp);
                assembler.Storage[_session.storageGuid] = Convert.ToBase64String(binary);
                if (Session.logging)
                    MyLog.Default.WriteLineAndConsole($"{Session.modName} Saving storage for {assembler.DisplayNameText} {tempListComp.auto}");
            }
            else if (!Session.Server && Session.Client && Session.MPActive)
            {
                //Roll up dirty list items
                foreach (var item in buildList)
                {
                    if (item.Value.dirty)
                    {
                        item.Value.dirty = false;
                        tempUpdateList.Add(item.Value);
                    }
                }

                if (tempRemovalList.Count + tempUpdateList.Count > 0)
                {
                    var updates = new UpdateComp() { compItemsRemove = tempRemovalList, compItemsUpdate = tempUpdateList };
                    var data = Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(updates));
                    Session.SendPacketToServer(new UpdateDataPacket { EntityId = assembler.EntityId, Type = PacketType.UpdateData, rawData = data });

                    if (Session.netlogging)
                        MyLog.Default.WriteLineAndConsole($"{Session.modName} Sent updates to server");
                }
            }
            tempRemovalList.Clear();
            tempUpdateList.Clear();
        }
        public void Clean()
        {
            if (Session.Server)
            {
                gridComp.countAStart += countStart;
                gridComp.countAStop += countStop;
                assembler.StoppedProducing -= Assembler_StoppedProducing;
                assembler.StartedProducing -= Assembler_StartedProducing;
            }
            else if (Session.Client && Session.MPActive)
            {
                if (Session.netlogging)
                    MyLog.Default.WriteLineAndConsole(Session.modName + $"Updating replication list on server - removal");
                Session.SendPacketToServer(new ReplicationPacket { EntityId = assembler.EntityId, add = false, Type = PacketType.Replication });
            }
            Session.aCompMap.Remove(assembler.EntityId);

            buildList.Clear();
            missingMatQueue.Clear();
            missingMatTypes.Clear();
        }
    }
}
