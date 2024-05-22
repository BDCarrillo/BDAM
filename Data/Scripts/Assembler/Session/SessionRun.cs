using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
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
            Client = (MPActive && !MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            MyEntities.OnEntityCreate += OnEntityCreate;
            if (Client)
            {
                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
            }
        }

        public override void BeforeStart()
        {
            if(Client)
                AssemblerHud.Init();

            if(MPActive)
            {
                if (Client)
                    MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ClientPacketId, ProcessPacket);
                else if (Server)
                {
                    MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ServerPacketId, ProcessPacket);
                    MyVisualScriptLogicProvider.PlayerDisconnected += PlayerDisco;
                }
            }

            if (Server)
                assemblerEfficiency =  1 / Session.AssemblerEfficiencyMultiplier;

            //Find assemblers and BP classes they can make
            foreach (var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                if(def is MyAssemblerDefinition)
                {
                    //Iterate BP classes an assembler can build
                    var aDef = def as MyAssemblerDefinition;
                    var bpClassSubtypeNames = new List<string>();

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
                            if(!BPLookup.ContainsKey(bp.Id.SubtypeName))
                                BPLookup.Add(bp.Id.SubtypeName, bp);
                        }
                        BPClasses.Add(bpClass.Id.SubtypeName, bpList);                           
                    }
                    assemblerBPs.Add(def.Id.SubtypeId.ToString(), bpClassSubtypeNames);//Pop assembler specific list
                }
            }
            foreach(var a in assemblerBPs)
            {
                var assemblerName = a.Key;
                var assemblerBPList = a.Value;
                assemblerBP2.Add(assemblerName, new List<MyBlueprintDefinitionBase>());
                foreach (var bpClass in assemblerBPList)
                    foreach (var bp in BPClasses[bpClass])
                        assemblerBP2[assemblerName].Add(bp);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (!_startGrids.IsEmpty && Tick % 30 == 0)
                StartComps();

            if(Server)
                foreach (var grid in GridMap.Values)
                {
                    if (grid.assemblerList.Count > 0 && grid.nextUpdate <= Tick) 
                    {
                        grid.UpdateGrid();
                    }
                }
            Tick++;
        }

        protected override void UnloadData()
        {
            foreach (var gridComp in GridMap.Values)
                gridComp.Clean();
            Clean();
            MyEntities.OnEntityCreate -= OnEntityCreate;

            if (Client)
            {
                MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;
            }

            if (MPActive)
            {
                if (Client)
                    MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ClientPacketId, ProcessPacket);
                else if (Server)
                {
                    MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ServerPacketId, ProcessPacket);
                    MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisco;
                }
            }
        }
    }
}
