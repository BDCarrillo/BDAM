﻿using RichHudFramework.Client;
using RichHudFramework.UI.Client;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;

namespace BDAM
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        public override void LoadData()
        {
            MPActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            Server = (MPActive && MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            Client = !MyAPIGateway.Utilities.IsDedicated;
            MyEntities.OnEntityCreate += OnEntityCreate;
            Log.InitLogs();
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            RichHudClient.Init("BDAM", HudInit, null);
        }
        private void HudInit()
        {
            aWindow = new AssemblerWindow(HudMain.Root, this)
            {
                Visible = false,
            };
        }
        public override void BeforeStart()
        {
            if (Client)
            {
                MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEnteredSender;
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ClientPacketId, ProcessPacket);
            }
            if (Server)
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ServerPacketId, ProcessPacket);
                MyVisualScriptLogicProvider.PlayerDisconnected += PlayerDisco;
                refreshTimeSeconds = refreshTime * 0.016666666f - 2;
                assemblerEfficiency = 1 / Session.AssemblerEfficiencyMultiplier;
                assemblerSpeed = Session.AssemblerSpeedMultiplier;
            }

            //Find assemblers and BP classes they can make
            foreach (var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                if (def is MyAssemblerDefinition)
                {
                    //Iterate BP classes an assembler can build
                    var aDef = def as MyAssemblerDefinition;
                    var bpClassSubtypeNames = new List<string>();
                    speedMap.Add(def.Id.SubtypeId.ToString(), aDef.AssemblySpeed);
                    foreach(var bpClass in aDef.BlueprintClasses)
                    {
                        if (bpClass.Id.SubtypeName == "LargeBlocks" || bpClass.Id.SubtypeName == "SmallBlocks" || bpClass.Id.SubtypeName == "BuildPlanner")
                            continue;

                        bpClassSubtypeNames.Add(bpClass.Id.SubtypeName); //Add BP class to assembler list

                        if (BPClasses.ContainsKey(bpClass.Id.SubtypeName))
                            continue;

                        var bpList = new List<MyBlueprintDefinitionBase>();
                        foreach (var bp in bpClass)
                        {
                            if (bp.Public == false)
                                continue;
                            bpList.Add(bp);

                            BPLookup[bp.Id.SubtypeName] = bp;
                            BPLookupFriendly[bp.Results[0].Id.SubtypeName] = bp;
                            NameLookupFriendly[bp.Results[0].Id.SubtypeName] = bp.DisplayNameText;
                        }
                        BPClasses.Add(bpClass.Id.SubtypeName, bpList);                           
                    }
                    assemblerBPs.Add(def.Id.SubtypeId.ToString(), bpClassSubtypeNames);//Pop assembler specific list
                }
                if (def is MyPhysicalItemDefinition)
                {
                    var physDef = def as MyPhysicalItemDefinition;
                    if (physDef.IsIngot || physDef.IsOre)
                        NameLookupFriendly[physDef.Id.SubtypeName] = physDef.DisplayNameText;
                }
            }
            foreach(var a in assemblerBPs)
            {
                assemblerBP2.Add(a.Key, new List<MyBlueprintDefinitionBase>());
                foreach (var bpClass in a.Value)
                    foreach (var bp in BPClasses[bpClass])
                        assemblerBP2[a.Key].Add(bp);
            }
        }


        public override void UpdateBeforeSimulation()
        {
            if (!_startGrids.IsEmpty && Tick % 30 == 0)
                StartComps();

            if(Server)
                foreach (var grid in GridMap.Values)
                    if (grid.assemblerList.Count > 0 && grid.nextUpdate <= Tick) 
                        grid.UpdateGrid();
            Tick++;
        }

        protected override void UnloadData()
        {
            foreach (var gridComp in GridMap.Values)
                gridComp.Clean(false);
            Clean();
            MyEntities.OnEntityCreate -= OnEntityCreate;

            if (Client)
            {
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ClientPacketId, ProcessPacket);
                MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
                if (aWindow != null)
                    aWindow.Unregister();
            }
            if (Server)
            {
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ServerPacketId, ProcessPacket);
                MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisco;
            }
            Log.Close();
        }
    }
}
