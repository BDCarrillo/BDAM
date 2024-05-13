using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRage.Utils;

namespace BDAM
{
    [ProtoContract]
    public class ListComp //Used for mod storage serialization
    {
        [ProtoMember(100)] public List<ListCompItem> compItems;
        [ProtoMember(101)] public bool auto = false;
    }
    [ProtoContract]
    public class ListCompItem
    {
        [ProtoMember(1)] public string bpBase;
        [ProtoMember(2)] public int buildAmount = -1;
        [ProtoMember(3)] public int grindAmount = -1;
        [ProtoMember(4)] public int priority = 3;
        [ProtoMember(5)] public string label;

        public bool missingMats;
        public int missingMatsTime;
    }

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
        internal Dictionary<MyBlueprintDefinitionBase, int> missingMats = new Dictionary<MyBlueprintDefinitionBase, int>();

        //Stats tracking
        internal int unJamAttempts = 0;
        internal int countStart = 0;
        internal int countStop = 0;


        internal void Init(IMyAssembler Assembler, GridComp comp, Session session)
        {
            _session = session;
            gridComp = comp;
            assembler = Assembler;
            
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
                                ListComp load = new ListComp() { compItems = new List<ListCompItem>() };
                                var base64 = Convert.FromBase64String(rawData);
                                load = MyAPIGateway.Utilities.SerializeFromBinary<ListComp>(base64);

                                //Serialized string to BPs
                                foreach (var saved in load.compItems)
                                {
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
            //TODO client request data from server
        }

        public void AssemblerUpdate()
        {
            if (!assembler.IsQueueEmpty)
            {
                var queue = assembler.GetQueue();
                if (Session.logging)
                    MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + $"{queue[0].Blueprint.Id.SubtypeName} - {queue[0].Amount}  Last: {lastQueue.Blueprint.Id.SubtypeName} - {lastQueue.Amount}");
                if (lastQueue.Blueprint == queue[0].Blueprint && lastQueue.Amount == queue[0].Amount)
                {
                    //TODO check if this throws a false positive if update occurs right after the stoppage was detected and before a pull
                    assembler.RemoveQueueItem(0, queue[0].Amount);
                    if (buildList.ContainsKey((MyBlueprintDefinitionBase)queue[0].Blueprint))
                    {
                        missingMats.Add((MyBlueprintDefinitionBase)queue[0].Blueprint, Session.Tick + 101);
                        if (Session.logging)
                            MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + $" same item/qty found in queue, missing mats {queue[0].Blueprint.Id.SubtypeName}.  Progress: {assembler.CurrentProgress}"); //insufficient mats?
                    }
                    else if (Session.logging)
                        MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + $" manually added {queue[0].Blueprint.Id.SubtypeName} missing mats, removed from queue ");
                }
            }
            else
            {
                if (Session.logging)
                    MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " skipping check- items in queue");
                return;
            }

            //Missing mat checks
            //TODO don't like time based as _any_ new inv item can trip the cycle.  Unsure if it's worth iterating all ingredients though
            foreach (var item in buildList)
            {
                var lComp = item.Value;
                if (!lComp.missingMats || lComp.missingMatsTime > Session.Tick)
                    continue;
                if (gridComp.lastInvUpdate > lComp.missingMatsTime)
                {
                    lComp.missingMats = false;
                    if (Session.logging)
                        MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + $" removed {lComp.label} from missing mat list");
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
            if (Session.logging)
                MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " checking for buildable items");
            for (int i = 1; i < 4; i++)
                foreach (var listItem in buildList)
                {
                    var lComp = listItem.Value;
                    if (lComp.buildAmount == -1 || lComp.buildAmount <= lComp.grindAmount || lComp.priority != i || lComp.missingMats)
                        continue;
                    var itemName = listItem.Key.Results[0].Id.SubtypeName;
                    if (gridComp.inventoryList.ContainsKey(itemName))
                    {
                        var amountAvail = gridComp.inventoryList[itemName];
                        var amountNeeded = lComp.buildAmount - amountAvail;
                        if (amountNeeded > 0)
                        {
                            var queueAmount = amountNeeded > Session.maxQueueAmount ? Session.maxQueueAmount : amountNeeded;
                            if (assembler.Mode == Sandbox.ModAPI.Ingame.MyAssemblerMode.Disassembly)
                                assembler.Mode = Sandbox.ModAPI.Ingame.MyAssemblerMode.Assembly;
                            assembler.AddQueueItem(listItem.Key, queueAmount);
                            if (Session.logging)
                                MyLog.Default.WriteLineAndConsole($"{Session.modName}Queued build {queueAmount} of {itemName}.  On-hand {amountAvail}  Target {listItem.Value.buildAmount}");
                            return true;
                        }
                    }
                }
            return false;
        }
        public bool AssemblerTryGrind()
        {
            if (Session.logging)
                MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " checking for grindable items");
            for (int i = 1; i < 4; i++)
                foreach (var listItem in buildList)
                {
                    var lComp = listItem.Value;
                    if (lComp.grindAmount == -1 || lComp.grindAmount <= lComp.buildAmount || lComp.priority != i)
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
            return false;
        }

        public void Save()
        {
            if (Session.Server)
            {
                ListComp tempListComp = new ListComp() { compItems = new List<ListCompItem>() };
                foreach (var item in buildList.Values)
                    tempListComp.compItems.Add(item);
                tempListComp.auto = autoControl;
                var binary = MyAPIGateway.Utilities.SerializeToBinary(tempListComp);
                assembler.Storage[_session.storageGuid] = Convert.ToBase64String(binary);
                if (Session.logging)
                    MyLog.Default.WriteLineAndConsole($"{Session.modName} Saving storage for {assembler.DisplayNameText} {tempListComp.auto}");
            }
            else if (!Session.Server && Session.Client)
            {
                //TODO client network send of data update
            }
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
            buildList.Clear();
        }
    }
}
