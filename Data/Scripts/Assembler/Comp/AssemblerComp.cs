using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage;

namespace BDAM
{
    public partial class AssemblerComp
    {
        private Session _session;
        internal IMyAssembler assembler;
        internal int runStartTick;
        internal int runStopTick;
        internal bool inputJammed;
        internal bool outputJammed;
        internal bool autoControl = false;
        internal bool masterMode = false;
        internal bool helperMode = false;
        internal int notification = 0; // 0 = Owner, 1 = faction, 2 = none
        internal MyProductionQueueItem lastQueue;
        internal Dictionary<MyBlueprintDefinitionBase, ListCompItem> buildList = new Dictionary<MyBlueprintDefinitionBase, ListCompItem>();
        internal GridComp gridComp;
        internal Dictionary<string, int> missingMatAmount = new Dictionary<string, int>();
        internal Dictionary<string, int> inaccessibleMatAmount = new Dictionary<string, int>();
        internal Dictionary<string, int> inaccessibleCompAmount = new Dictionary<string, int>();
        internal Dictionary<MyBlueprintDefinitionBase, Dictionary<string, MyFixedPoint>> missingMatQueue = new Dictionary<MyBlueprintDefinitionBase, Dictionary<string, MyFixedPoint>>();
        internal Dictionary<MyBlueprintDefinitionBase, Dictionary<string, MyFixedPoint>> inaccessibleMatQueue = new Dictionary<MyBlueprintDefinitionBase, Dictionary<string, MyFixedPoint>>();

        internal List<ulong> ReplicatedClients = new List<ulong>();
        internal int maxQueueAmount = 500;
        internal float baseSpeed = 0f;

        //Temps for networking updates/removals
        internal List<ListCompItem> tempUpdateList = new List<ListCompItem>();
        internal List<string> tempRemovalList = new List<string>();
        internal bool queueDirty = false;

        //Stats tracking
        internal int unJamAttempts = 0;
        internal int countStart = 0;
        internal int countStop = 0;

        internal void Init(IMyAssembler Assembler, GridComp gComp, Session session)
        {
            _session = session;
            gridComp = gComp;
            assembler = Assembler;
            Session.aCompMap[assembler.EntityId] = this;
            //TODO look at grid change events

            if (Session.Server)
            {
                if (!assembler.IsQueueEmpty)
                {
                    var queue = assembler.GetQueue();
                    lastQueue = queue[0];
                }
                baseSpeed = Session.speedMap[assembler.BlockDefinition.SubtypeId];
                assembler.StoppedProducing += Assembler_StoppedProducing;
                assembler.StartedProducing += Assembler_StartedProducing;
                //Check/init storage
                if (assembler.Storage == null)
                    assembler.Storage = new MyModStorageComponent { [_session.storageGuid] = "" };
                else if (!assembler.Storage.ContainsKey(_session.storageGuid))
                    assembler.Storage[_session.storageGuid] = "";
                else if (assembler.Storage.ContainsKey(_session.storageGuid))
                {
                    //Serialize storage
                    string rawData;
                    if (assembler.Storage.TryGetValue(_session.storageGuid, out rawData) && rawData != null && rawData.Length > 0)
                    {
                        var load = MyAPIGateway.Utilities.SerializeFromBinary<ListComp>(Convert.FromBase64String(rawData));

                        //Serialized string to BPs
                        foreach (var saved in load.compItems)
                            if (Session.BPLookup.ContainsKey(saved.bpBase))
                                buildList.Add(Session.BPLookup[saved.bpBase], new ListCompItem() { bpBase = saved.bpBase, buildAmount = saved.buildAmount, grindAmount = saved.grindAmount, priority = saved.priority, label = saved.label });
                        autoControl = load.auto;
                        notification = load.notif;
                        maxQueueAmount = load.queueAmt;
                        masterMode = load.master;
                        helperMode = load.helper;
                        if (masterMode)
                            gComp.masterAssembler = assembler.EntityId;
                    }
                }
            }
            else if (Session.Client)
                session.SendPacketToServer(new ReplicationPacket { EntityId = assembler.EntityId, add = true, Type = PacketType.Replication });
        }

        public void AssemblerUpdate()
        {
            bool sendMatUpdates = false;
            bool sendInacUpdates = false;

            if (!assembler.IsQueueEmpty)
            {
                var queue = assembler.GetQueue();
                if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + $" Update check Queue: {queue[0].Blueprint.Id.SubtypeName} - {queue[0].Amount}  Last: {lastQueue.Blueprint.Id.SubtypeName} - {lastQueue.Amount}");

                //Jam check due to missing mats
                if (lastQueue.Blueprint == queue[0].Blueprint && lastQueue.Amount == queue[0].Amount && assembler.CurrentProgress == 0)
                {
                    if (assembler.Mode == Sandbox.ModAPI.Ingame.MyAssemblerMode.Assembly)
                    {
                        ListCompItem lComp;
                        if (buildList.TryGetValue((MyBlueprintDefinitionBase)queue[0].Blueprint, out lComp))
                        {
                            var bp = (MyBlueprintDefinitionBase)queue[0].Blueprint;
                            foreach (var item in bp.Prerequisites)
                            {
                                var adjustedAmount = item.Amount * Session.assemblerEfficiency;
                                //Insufficient mats
                                if ((!gridComp.inventoryList.ContainsKey(item.Id.SubtypeName) || gridComp.inventoryList[item.Id.SubtypeName] < item.Amount)) 
                                {
                                    MyFixedPoint qty = 0;
                                    gridComp.inventoryList.TryGetValue(lComp.label, out qty);
                                    var subTotalNeeded = adjustedAmount * (lComp.buildAmount - qty);

                                    if (!missingMatQueue.ContainsKey(bp))
                                        missingMatQueue.Add(bp, new Dictionary<string, MyFixedPoint>());

                                    missingMatQueue[bp][item.Id.SubtypeName] = subTotalNeeded;

                                    if (notification < 2)
                                        SendNotification(gridComp.Grid.DisplayName + ": " + assembler.CustomName + $" missing {Session.FriendlyNameLookup(item.Id.SubtypeName)} for {lComp.label}");
                                    sendMatUpdates = true;
                                    lComp.missingMats = true;
                                    if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + $" Missing {item.Amount} ({adjustedAmount}) {item.Id.SubtypeName} for {lComp.label}");
                                }
                                else if (!assembler.InputInventory.ContainItems(adjustedAmount, item.Id) && !missingMatQueue.ContainsKey(bp) && !inaccessibleMatQueue.ContainsKey(bp)) //Inaccessible mats.  Looks at what the Keen pull has already done
                                {
                                    MyFixedPoint qty = 0;
                                    gridComp.inventoryList.TryGetValue(item.Id.SubtypeName, out qty);
                                    if (!inaccessibleMatQueue.ContainsKey(bp))
                                        inaccessibleMatQueue.Add(bp, new Dictionary<string, MyFixedPoint>());
                                    inaccessibleMatQueue[bp][item.Id.SubtypeName] = qty;
                                    if (notification < 2)
                                        SendNotification(gridComp.Grid.DisplayName + ": " + assembler.CustomName + $" can't access {(item.Id.SubtypeName == "Stone" ? "Gravel" : Session.FriendlyNameLookup(item.Id.SubtypeName))} for {lComp.label}");
                                    lComp.inaccessibleMats = true;
                                    sendInacUpdates = true;
                                }
                            }
                            //If it was indeed missing materials, remove item from queue
                            if (lComp.missingMats || lComp.inaccessibleMats)
                                assembler.RemoveQueueItem(0, queue[0].Amount);

                            if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + $" same item/qty found in queue, missing mats checked for {lComp.label}.  Progress: {assembler.CurrentProgress}  Actually missing: {lComp.missingMats} Inaccessible: {lComp.inaccessibleComps}");
                        }
                        else
                        {
                            assembler.RemoveQueueItem(0, queue[0].Amount);
                            if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + $" manually added {queue[0].Blueprint.Id.SubtypeName} missing mats/stuck, removed from queue");
                        }
                    }
                    else //Disassembly stuck
                    {
                        ListCompItem lComp;
                        if (buildList.TryGetValue((MyBlueprintDefinitionBase)queue[0].Blueprint, out lComp))
                        {
                            var bp = (MyBlueprintDefinitionBase)queue[0].Blueprint;
                            if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + $" same item/qty found in disassembly queue, removing. Progress: {assembler.CurrentProgress}");
                            if (notification < 2)
                                SendNotification(gridComp.Grid.DisplayName + ": " + assembler.CustomName + $" cannot access items to be disassembled: {Session.FriendlyNameLookup(lComp.label)}");

                            MyFixedPoint qty = 0;
                            gridComp.inventoryList.TryGetValue(lComp.label, out qty);
                            if (!inaccessibleMatQueue.ContainsKey(bp))
                                inaccessibleMatQueue.Add(bp, new Dictionary<string, MyFixedPoint>());
                            inaccessibleMatQueue[bp][lComp.label] = qty.ToIntSafe();
                            lComp.inaccessibleComps = true;
                            assembler.RemoveQueueItem(0, queue[0].Amount);
                            sendInacUpdates = true;
                        }                        
                    }
                }
                if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + " quick check - items in queue");
            }

            if (assembler.IsQueueEmpty) //Second check, since it might have been cleared
            {
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

            //Missing mat list checks
            if (missingMatQueue.Count > 0)
                foreach (var matQueue in missingMatQueue.Keys.ToArray())
                {
                    foreach (var item in matQueue.Prerequisites)
                    {
                        if (gridComp.inventoryList.ContainsKey(item.Id.SubtypeName) && gridComp.inventoryList[item.Id.SubtypeName] >= item.Amount * Session.assemblerEfficiency)
                        {
                            missingMatQueue[matQueue].Remove(item.Id.SubtypeName);
                            sendMatUpdates = true;
                        }
                    }
                    if (missingMatQueue[matQueue].Count == 0)
                    {
                        ListCompItem lComp;
                        if (buildList.TryGetValue(matQueue, out lComp))
                        {
                            lComp.missingMats = false;
                            if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + $" removed {lComp.label} from missing mat list");
                        }
                        missingMatQueue.Remove(matQueue);
                    }
                }

            //Mats/Comps that are inaccessible
            if (inaccessibleMatQueue.Count > 0)
                foreach (var matQueue in inaccessibleMatQueue.ToArray())
                {
                    foreach (var item in matQueue.Value.ToArray())
                    {
                        if (gridComp.inventoryList.ContainsKey(item.Key) && gridComp.inventoryList[item.Key] != item.Value)
                        {
                            inaccessibleMatQueue[matQueue.Key].Remove(item.Key);
                            sendInacUpdates = true;
                        }
                    }
                    if (inaccessibleMatQueue[matQueue.Key].Count == 0)
                    {
                        ListCompItem lComp;
                        if (buildList.TryGetValue(matQueue.Key, out lComp))
                        {
                            lComp.inaccessibleMats = false;
                            if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + $" removed {lComp.label} from inaccessible mat list");
                        }
                        inaccessibleMatQueue.Remove(matQueue.Key);
                    }
                }

            //Send updates
            if (sendInacUpdates)
                SendInaccessibleUpdates();
            if (sendMatUpdates)
                SendMissingMatUpdates();
        }
        internal void SendNotification(string message)
        {
            var steamID = MyAPIGateway.Multiplayer.Players.TryGetSteamId(assembler.OwnerId);
            if (steamID > 0)
            {
                if (notification == 1)
                {
                    var playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(assembler.OwnerId);
                    if (playerFaction != null && playerFaction.AcceptHumans && !playerFaction.IsEveryoneNpc())
                    {
                        foreach (var member in playerFaction.Members)
                        {
                            var facMbrID = MyAPIGateway.Multiplayer.Players.TryGetSteamId(member.Value.PlayerId);
                            if (facMbrID > 0 && Session.PlayerMap.ContainsKey(facMbrID))
                                _session.SendPacketToClient(new NotificationPacket { Message = message, Type = PacketType.Notification }, facMbrID);
                        }
                    }
                    else
                        _session.SendPacketToClient(new NotificationPacket { Message = message, Type = PacketType.Notification }, steamID);
                }
                else
                    _session.SendPacketToClient(new NotificationPacket { Message = message, Type = PacketType.Notification }, steamID);
            }
        }
        internal void SendMissingMatUpdates()
        {
            //Regenerate missing mat list
            missingMatAmount.Clear();
            if (missingMatQueue.Count > 0)
                foreach (var matDict in missingMatQueue.Values)
                    foreach (var missingMat in matDict)
                    {
                        var name = Session.FriendlyNameLookup(missingMat.Key);
                        if (missingMatAmount.ContainsKey(name))
                            missingMatAmount[name] += (int)missingMat.Value;
                        else
                            missingMatAmount.Add(name, (int)missingMat.Value);
                    }

            if (Session.netlogging) Log.WriteLine($"{Session.modName} Sending missing mat updates");
            _session.SendPacketToClients(new MissingMatPacket
            {
                data = missingMatAmount,
                Type = PacketType.MissingMatData,
                EntityId = assembler.EntityId
            }, ReplicatedClients);
        }
        internal void SendInaccessibleUpdates()
        {
            //Regenerate inaccessible mat list
            inaccessibleMatAmount.Clear();
            if (inaccessibleMatQueue.Count > 0)
                foreach (var matDict in inaccessibleMatQueue.Values)
                    foreach (var missingMat in matDict)
                    {
                        var name = Session.FriendlyNameLookup(missingMat.Key);
                        if (inaccessibleMatAmount.ContainsKey(name))
                            inaccessibleMatAmount[name] += (int)missingMat.Value;
                        else
                            inaccessibleMatAmount.Add(name, (int)missingMat.Value);
                    }
            if (Session.netlogging) Log.WriteLine($"{Session.modName} Sending Inaccessible item updates");
            _session.SendPacketToClients(new InaccessibleCompPacket
            {
                data = inaccessibleMatAmount,
                Type = PacketType.InaccessibleData,
                EntityId = assembler.EntityId
            }, ReplicatedClients);
        }
        public bool AssemblerTryBuild()
        {
            if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + " checking for buildable items");
            for (int i = 1; i < 4; i++)
                foreach (var listItem in buildList)
                {
                    var lComp = listItem.Value;
                    if (lComp.missingMats && lComp.buildAmount == -1)
                        lComp.missingMats = false;
                    if (lComp.priority != i || lComp.buildAmount == -1 || lComp.missingMats || lComp.inaccessibleMats || (lComp.grindAmount > -1 && lComp.buildAmount > lComp.grindAmount))
                        continue;
                    var itemName = listItem.Key.Results[0].Id.SubtypeName;
                    MyFixedPoint amountAvail = 0;
                    gridComp.inventoryList.TryGetValue(itemName, out amountAvail);
                    var amountNeeded = lComp.buildAmount - amountAvail;
                    if (amountNeeded > 0)
                    {
                        var queueAmount = amountNeeded > maxQueueAmount ? maxQueueAmount : amountNeeded;
                        if (assembler.Mode == Sandbox.ModAPI.Ingame.MyAssemblerMode.Disassembly)
                            assembler.Mode = Sandbox.ModAPI.Ingame.MyAssemblerMode.Assembly;
                        var processingTime = (float)(listItem.Key.BaseProductionTimeInSeconds / (Session.assemblerSpeed * (baseSpeed + assembler.UpgradeValues["Productivity"])) * queueAmount);
                        if (!masterMode || (masterMode && processingTime < Session.refreshTimeSeconds))
                        {
                            lastQueue = new MyProductionQueueItem() { Blueprint = listItem.Key, Amount = queueAmount };
                            if (Session.logging) Log.WriteLine($"{Session.modName} {assembler.CustomName} Queued build {queueAmount} of {itemName}.  On-hand {amountAvail}  Target {listItem.Value.buildAmount}");
                            assembler.AddQueueItem(listItem.Key, queueAmount);
                        }
                        else
                            AssemblerMaster(listItem.Key, queueAmount, true);
                        return true;
                    }
                }
            if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + $" no buildable items found");
            return false;
        }
        public bool AssemblerTryGrind()
        {
            if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + " checking for grindable items");
            for (int i = 1; i < 4; i++)
                foreach (var listItem in buildList)
                {
                    var lComp = listItem.Value;
                    if (lComp.priority != i || lComp.grindAmount == -1 || lComp.inaccessibleComps || (lComp.buildAmount > -1 && lComp.buildAmount > lComp.grindAmount))
                        continue;
                    var itemName = listItem.Key.Results[0].Id.SubtypeName;
                    if (gridComp.inventoryList.ContainsKey(itemName))
                    {
                        var amountAvail = gridComp.inventoryList[itemName];
                        var amountExcess = amountAvail - lComp.grindAmount;
                        if (amountExcess > 0)
                        {
                            var queueAmount = amountExcess > maxQueueAmount ? maxQueueAmount : amountExcess;
                            if (assembler.Mode == Sandbox.ModAPI.Ingame.MyAssemblerMode.Assembly)
                                assembler.Mode = Sandbox.ModAPI.Ingame.MyAssemblerMode.Disassembly;
                            var processingTime = (float)(listItem.Key.BaseProductionTimeInSeconds / (Session.assemblerSpeed * (baseSpeed + assembler.UpgradeValues["Productivity"])) * queueAmount);
                            if (!masterMode || (masterMode && processingTime < Session.refreshTimeSeconds))
                            {
                                assembler.AddQueueItem(listItem.Key, queueAmount);
                                if (Session.logging) Log.WriteLine($"{Session.modName} {assembler.CustomName} Queued grind {queueAmount} of {itemName}.  On-hand {amountAvail}  Target {listItem.Value.grindAmount}");
                            }
                            else
                                AssemblerMaster(listItem.Key, queueAmount, false);
                            return true;
                        }
                    }
                }
            if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + $" no grindable items found");
            return false;
        }

        public void AssemblerMaster(MyBlueprintDefinitionBase item, MyFixedPoint queueAmount, bool build)
        {
            if (Session.logging) Log.WriteLine($"{Session.modName} {assembler.CustomName} Running work distribution for {queueAmount} of {item.Results[0].Id.SubtypeName}.");
            //List of available helpers
            var availableList = new List<AssemblerComp>();
            foreach (var aComp in gridComp.assemblerList.Values)
            {
                if (!aComp.helperMode || !aComp.assembler.Enabled || !aComp.assembler.UseConveyorSystem || !aComp.assembler.IsQueueEmpty || aComp.assembler == assembler || !aComp.assembler.CanUseBlueprint(item))
                    continue;
                availableList.Add(aComp);
            }
            if (availableList.Count == 0)
            {
                assembler.AddQueueItem(item, queueAmount);
                if (Session.logging) Log.WriteLine($"{Session.modName} No available helpers found {assembler.CustomName} (master) assigned {queueAmount} of {item.Results[0].Id.SubtypeName}");
                return;
            }

            var masterProcessingAmount = (int)(Session.refreshTimeSeconds / (item.BaseProductionTimeInSeconds / (Session.assemblerSpeed * (baseSpeed + assembler.UpgradeValues["Productivity"]))));
            if (masterProcessingAmount == 0)
                masterProcessingAmount = 1;            
            if (masterProcessingAmount > queueAmount)
                masterProcessingAmount = (int)queueAmount;
            queueAmount -= masterProcessingAmount;

            //Add to master
            assembler.AddQueueItem(item, masterProcessingAmount);
            if (Session.logging) Log.WriteLine($"{Session.modName} {assembler.CustomName} (master) assigned {masterProcessingAmount} of {item.Results[0].Id.SubtypeName}");

            //Iterate and assign to helpers
            foreach (var helper in availableList)
            {
                if (build && helper.assembler.Mode != Sandbox.ModAPI.Ingame.MyAssemblerMode.Assembly)
                    helper.assembler.Mode = Sandbox.ModAPI.Ingame.MyAssemblerMode.Assembly;
                else if (!build && helper.assembler.Mode != Sandbox.ModAPI.Ingame.MyAssemblerMode.Disassembly)
                    helper.assembler.Mode = Sandbox.ModAPI.Ingame.MyAssemblerMode.Disassembly;

                var helperProcessingAmount = (int)(Session.refreshTimeSeconds / (item.BaseProductionTimeInSeconds / (Session.assemblerSpeed * (helper.baseSpeed + helper.assembler.UpgradeValues["Productivity"]))));
                if (helperProcessingAmount == 0)
                    helperProcessingAmount = 1;
                if (helperProcessingAmount > queueAmount)
                    helperProcessingAmount = (int)queueAmount;
                queueAmount -= helperProcessingAmount;

                helper.assembler.AddQueueItem(item, helperProcessingAmount);
                if (Session.logging) Log.WriteLine($"{Session.modName} {helper.assembler.CustomName} assigned {helperProcessingAmount} of {item.Results[0].Id.SubtypeName}");
                if (queueAmount <= 0)
                    return;
            }
        }

        public void SaveServer()
        {
            var tempListComp = new ListComp();
            foreach (var item in buildList.Values)
                tempListComp.compItems.Add(item);
            tempListComp.auto = autoControl;
            tempListComp.notif = notification;
            tempListComp.queueAmt = maxQueueAmount;
            tempListComp.master = masterMode;
            tempListComp.helper = helperMode;
            var binary = MyAPIGateway.Utilities.SerializeToBinary(tempListComp);
            assembler.Storage[_session.storageGuid] = Convert.ToBase64String(binary);
            if (Session.logging) Log.WriteLine($"{Session.modName} Saving storage for {assembler.DisplayNameText} {tempListComp.auto}");
        }

        public void SaveClient()
        {
            //Roll up dirty list items
            foreach (var lComp in buildList.Values)
            {
                if (lComp.dirty)
                {
                    lComp.dirty = false;
                    tempUpdateList.Add(lComp);
                }
            }

            if (tempRemovalList.Count + tempUpdateList.Count > 0)
            {
                var updates = new UpdateComp() { compItemsRemove = tempRemovalList, compItemsUpdate = tempUpdateList };
                var data = Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(updates));
                _session.SendPacketToServer(new UpdateDataPacket { EntityId = assembler.EntityId, Type = PacketType.UpdateData, rawData = data });
                if (Session.netlogging) Log.WriteLine($"{Session.modName} Sent updates to server");
            }

            if (queueDirty)
            {
                if (Session.netlogging) Log.WriteLine(Session.modName + $"Sending updated max queue amount {maxQueueAmount} to server");
                var packet = new UpdateStatePacket { Var = UpdateType.maxQueueAmount, Value = maxQueueAmount, Type = PacketType.UpdateState, EntityId = assembler.EntityId };
                _session.SendPacketToServer(packet);
                queueDirty = false;
            }
            tempRemovalList.Clear();
            tempUpdateList.Clear();
        }
        public void Clean(bool sendUpdate)
        {
            if (Session.Server)
            {
                gridComp.countAStart += countStart;
                gridComp.countAStop += countStop;
                assembler.StoppedProducing -= Assembler_StoppedProducing;
                assembler.StartedProducing -= Assembler_StartedProducing;
            }
            else if (sendUpdate && Session.Client)
            {
                if (Session.netlogging) Log.WriteLine(Session.modName + $"Updating replication list on server - removal");
                _session.SendPacketToServer(new ReplicationPacket { EntityId = assembler.EntityId, add = false, Type = PacketType.Replication });
            }
            Session.aCompMap.Remove(assembler.EntityId);

            buildList.Clear();
            missingMatQueue.Clear();
            missingMatAmount.Clear();
            inaccessibleMatAmount.Clear();
            inaccessibleMatQueue.Clear();
            inaccessibleCompAmount.Clear();
        }
    }
}
