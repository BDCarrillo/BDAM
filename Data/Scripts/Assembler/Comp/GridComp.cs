using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VRage;
using VRage.Game.Entity;
using VRage.Utils;

namespace BDAM
{
    public class GridComp
    {
        private Session _session;
        internal MyCubeGrid Grid;
        internal Dictionary<MyCubeBlock, AssemblerComp> assemblerList = new Dictionary<MyCubeBlock, AssemblerComp>();
        internal ConcurrentDictionary<string, MyFixedPoint> inventoryList = new ConcurrentDictionary<string, MyFixedPoint>();
        internal int invCount = 0;
        internal int updateCargos = 0;
        internal int countAStart = 0;
        internal int countAStop = 0;
        internal int countUnjamAttempts = 0;
        internal int lastInvUpdate = 0;
        internal int nextUpdate = 0;
        internal bool fatblocksDirty = false;
        internal long masterAssembler = 0;
        internal void Init(MyCubeGrid grid, Session session)
        {
            _session = session;
            Grid = grid;

            try
            {
                foreach (var fat in grid.GetFatBlocks())
                    FatBlockAdded(fat);
            }
            catch
            {
                fatblocksDirty = true;
            }

            Grid.OnFatBlockAdded += FatBlockAdded;
            Grid.OnFatBlockRemoved += FatBlockRemoved;
            Grid.OnGridMerge += OnGridMerge;
            nextUpdate = (int)(Session.Tick + Grid.EntityId % 100);
            //TODO look at grid merge/change events to preserve aComp data
        }
        private void OnGridMerge(MyCubeGrid retainedGrid, MyCubeGrid removedGrid)
        {

            Grid.OnGridMerge -= OnGridMerge;
        }
        internal void FatBlockAdded(MyCubeBlock block)
        {
            var assembler = block as IMyAssembler;
            if (assembler != null && !assemblerList.ContainsKey(block) && !block.CubeGrid.IsPreview)
            {
                var aComp = new AssemblerComp();
                aComp.Init(assembler, this, _session);
                assemblerList.Add(block, aComp);
                aComp.gridComp.masterAssembler = aComp.masterMode ? aComp.assembler.EntityId : 0;
                return;
            }
        }

        private void FatBlockRemoved(MyCubeBlock block)
        {
            var assembler = block as IMyAssembler;
            if (assembler != null)
            {
                if (assemblerList.ContainsKey(block))
                {
                    assemblerList[block].Clean(false);
                    assemblerList.Remove(block);
                    return;
                }
                else
                    Log.WriteLine($"{Session.modName} {Grid.DisplayName} Assembler type {block.DisplayNameText} was not in AssemblerList of the grid comp");
            }
        }
        internal void UpdateGrid()
        {
            try
            {
                //TODO look at dampening inv updates if they are unchanged repeatedly?
                var timer = new Stopwatch();
                timer.Start();
                inventoryList.Clear();
                MyInventoryBase inventory;
                foreach (var b in Grid.Inventories.ToArray())
                {
                    if (b is IMyAssembler || b is IMyRefinery)
                    {
                        var prodBlock = b as IMyProductionBlock;
                        var output = (MyInventoryBase)prodBlock.OutputInventory;
                        foreach (MyPhysicalInventoryItem item in output.GetItems())
                            inventoryList.AddOrUpdate(item.Content.SubtypeName, item.Amount, (key, current) => current += item.Amount);
                        invCount++;
                    }
                    else if ((b is IMyCargoContainer || b is IMyShipConnector) && b.TryGetInventory(out inventory))
                    {
                        foreach (MyPhysicalInventoryItem item in inventory.GetItems())
                            inventoryList.AddOrUpdate(item.Content.SubtypeName, item.Amount, (key, current) => current += item.Amount);
                        invCount++;
                    }
                }

                lastInvUpdate = Session.Tick;
                updateCargos++;
                timer.Stop();
                if (Session.logging) Log.WriteLine($"{Session.modName}{Grid.DisplayName} inventory update runtime: {timer.Elapsed.TotalMilliseconds}");

                //Assembler updates
                if (Session.Server)
                {
                    lock (assemblerList)
                        foreach (var aComp in assemblerList.Values)
                        {
                            if (aComp.autoControl && !aComp.assembler.CooperativeMode && aComp.buildList.Count > 0)
                                aComp.AssemblerUpdate();

                            if (aComp.inputJammed)
                            {
                                if (aComp.unJamAttempts < 5)
                                    aComp.UnJamAssembler(this, aComp);
                                else if (aComp.unJamAttempts < 6)
                                {
                                    aComp.unJamAttempts++;
                                    if (Session.logging) Log.WriteLine(Session.modName + aComp.gridComp.Grid.DisplayName + "Unable to unjam input for " + aComp.assembler.CustomName);
                                    if (aComp.notification < 2)
                                        aComp.SendNotification(aComp.gridComp.Grid.DisplayName + ": " + aComp.assembler.CustomName + $" Input inventory jammed");
                                }
                            }
                            if (aComp.outputJammed)
                            {
                                if (Session.logging) Log.WriteLine(Session.modName + aComp.gridComp.Grid.DisplayName + $"Assembler {aComp.assembler.CustomName} stopped - output full");
                                if (aComp.notification < 2)
                                    aComp.SendNotification(aComp.gridComp.Grid.DisplayName + ": " + aComp.assembler.CustomName + $" Output inventory jammed");
                                aComp.outputJammed = false;
                            }

                            //Helper stuck due to missing mats
                            if (aComp.helperMode && !(aComp.inputJammed || aComp.outputJammed) && !aComp.assembler.IsQueueEmpty)
                            {
                                var queue = aComp.assembler.GetQueue()[0];
                                if (aComp.lastQueue.Blueprint == queue.Blueprint && aComp.lastQueue.Amount == queue.Amount && aComp.assembler.CurrentProgress == 0)
                                {
                                    aComp.assembler.RemoveQueueItem(0, queue.Amount);
                                    if (Session.logging) Log.WriteLine(Session.modName + aComp.assembler.CustomName + $" in helper mode and stuck missing mats/components");
                                }
                            }
                        }
                    nextUpdate += Session.refreshTime;
                }
                if (fatblocksDirty)
                {
                    fatblocksDirty = false;
                    foreach (var fat in Grid.GetFatBlocks().ToArray())
                    {
                        if (fat is IMyAssembler)
                        {
                            if (assemblerList.ContainsKey(fat))
                                continue;
                            FatBlockAdded(fat);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine("BDAM crash in update grid" + "\n" + e);
                MyLog.Default.WriteLineAndConsole("BDAM crash in update grid" + "\n" + e);
            }
        }

        internal void Clean(bool sendUpdate)
        {
            foreach (var aComp in assemblerList.Values)
                aComp.Clean(sendUpdate);

            if (Session.logging && Session.Server)
                Log.WriteLine($"{Session.modName} Grid {Grid.DisplayName} closed \n" +
                    $"Grid assembler qty: {assemblerList.Count}  inventories checked: {invCount}  cargo updates: {updateCargos}\n" +
                    $"Assembler starts: {countAStart}  stops: {countAStop}  unjam attempts: {countUnjamAttempts}\n");

            assemblerList.Clear();
            inventoryList.Clear();
            Grid.OnFatBlockAdded -= FatBlockAdded;
            Grid.OnFatBlockRemoved -= FatBlockRemoved;
            Grid = null;
        }
    }
}
