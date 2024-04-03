﻿using ProtoBuf;
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
        [ProtoMember(2)] public int buildAmount;
        [ProtoMember(3)] public int grindAmount;
        //TODO priorities?
        //TODO missing mats?
    }

    internal partial class AssemblerComp
    {
        private Session _session;
        internal IMyAssembler assembler;
        internal int runStartTick;
        internal int runStopTick;
        internal bool inputJammed;
        internal bool outputJammed;
        internal bool missingMats;
        internal int missingMatsInt = 0;
        internal int unJamAttempts = 0;
        internal int countStart = 0;
        internal int countStop = 0;
        internal bool autoControl = false;
        internal MyProductionQueueItem lastQueue;
        internal Dictionary<MyBlueprintDefinitionBase, ListCompItem> buildList = new Dictionary<MyBlueprintDefinitionBase, ListCompItem>();
        internal GridComp gridComp;


        internal void Init(IMyAssembler Assembler, GridComp comp, Session session)
        {
            _session = session;
            gridComp = comp;
            assembler = Assembler;
            
            if (Session.Server)
            {
                assembler.StoppedProducing += Assembler_StoppedProducing;
                assembler.StartedProducing += Assembler_StartedProducing;
                //Check/init storage
                if (assembler.Storage == null)
                {
                    assembler.Storage = new MyModStorageComponent { [_session.storageGuid] = "" };
                    MyLog.Default.WriteLineAndConsole($"{Session.modName} Storage was null, initting for {assembler.DisplayNameText}");
                }
                else if (!assembler.Storage.ContainsKey(_session.storageGuid))
                {
                    assembler.Storage[_session.storageGuid] = "";
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
                                    buildList.Add(Session.BPLookup[saved.bpBase], new ListCompItem() { bpBase = saved.bpBase, buildAmount = saved.buildAmount, grindAmount = saved.grindAmount });
                                }
                                autoControl = load.auto;
                                MyLog.Default.WriteLineAndConsole($"{Session.modName} Loaded storage for {assembler.DisplayNameText} items found: {load.compItems.Count}  auto: {load.auto}");
                            }
                            else
                                MyLog.Default.WriteLineAndConsole($"{Session.modName} Storage found but empty for {assembler.DisplayNameText}");
                        }
                        catch (Exception e)
                        {
                            MyLog.Default.WriteLineAndConsole($"{Session.modName} Error reading storage for {assembler.DisplayNameText} {e}");
                        }
                    }
                    else
                        MyLog.Default.WriteLineAndConsole($"{Session.modName} Storage found but empty for {assembler.DisplayNameText}");
                }
            }
            //TODO client request data from server
        }

        public void AssemblerUpdate()
        {
            //TODO eval this for cases of missing materials
            //TODO noting/skipping items that are not buildable due to a lack of materials

            if (!assembler.IsQueueEmpty)
            {
                var queue = assembler.GetQueue();

                MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + $"{queue[0].Blueprint.DisplayNameText} - {queue[0].Amount}  Last: {lastQueue.Blueprint.DisplayNameText} - {lastQueue.Amount}");

                if (lastQueue.Blueprint == queue[0].Blueprint && lastQueue.Amount == queue[0].Amount)
                {
                    if (Session.logging)
                        MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " SAME ITEM SITTING IN QUEUE"); //insufficient mats?

                }
                else if (Session.logging)
                    MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " skipping check- items in queue");
                return;
            }
            MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + $" check - mode: {assembler.Mode}");


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
            foreach (var listItem in buildList)
            {
                if (listItem.Value.buildAmount == -1 || listItem.Value.buildAmount <= listItem.Value.grindAmount)
                    continue;
                var itemName = listItem.Key.Results[0].Id.SubtypeName;
                if (gridComp.inventoryList.ContainsKey(itemName))
                {
                    var amountAvail = gridComp.inventoryList[itemName];
                    var amountNeeded = listItem.Value.buildAmount - amountAvail;
                    if (amountNeeded > 0)
                    {
                        var queueAmount = amountNeeded > Session.maxQueueAmount ? Session.maxQueueAmount : amountNeeded;
                        if (assembler.Mode == Sandbox.ModAPI.Ingame.MyAssemblerMode.Disassembly)
                            assembler.Mode = Sandbox.ModAPI.Ingame.MyAssemblerMode.Assembly;
                        assembler.AddQueueItem(listItem.Key, queueAmount);
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
            foreach (var listItem in buildList)
            {
                if (listItem.Value.grindAmount == -1 || listItem.Value.grindAmount <= listItem.Value.buildAmount)
                    continue;
                var itemName = listItem.Key.Results[0].Id.SubtypeName;
                if (gridComp.inventoryList.ContainsKey(itemName))
                {
                    var amountAvail = gridComp.inventoryList[itemName];
                    var amountExcess = amountAvail - listItem.Value.grindAmount;
                    if (amountExcess > 0)
                    {
                        var queueAmount = amountExcess > Session.maxQueueAmount ? Session.maxQueueAmount : amountExcess;
                        if (assembler.Mode == Sandbox.ModAPI.Ingame.MyAssemblerMode.Assembly)
                            assembler.Mode = Sandbox.ModAPI.Ingame.MyAssemblerMode.Disassembly;
                        assembler.AddQueueItem(listItem.Key, queueAmount);
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
