﻿using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI.Ingame;
using VRage.Utils;

namespace BDAM
{
    internal partial class AssemblerComp
    {
        private void Assembler_StartedProducing()
        {
            if (runStartTick == Session.Tick)
                return;
            countStart++;
            inputJammed = false;
            outputJammed = false;
            unJamAttempts = 0;
            if (Session.logging) MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " started");
            runStartTick = Session.Tick; //Dumb... action runs twice/assembler
        }
        private void Assembler_StoppedProducing()
        {
            if (runStopTick == Session.Tick)
                return;
            countStop++;

            if (assembler.IsQueueEmpty)
            {
                if (Session.logging)
                    MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " finished queue");
            }
            //Production stopped
            if (assembler.IsFunctional && assembler.Enabled && !assembler.IsQueueEmpty)
            {
                if (assembler.InputInventory.VolumeFillFactor > 0.95f) //Input jammed up (less than 5% space remaining)
                {
                    if (Session.logging) MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " Input jammed" + assembler.InputInventory.VolumeFillFactor * 100 + " % full");
                    inputJammed = true;
                }

                if (assembler.OutputInventory.VolumeFillFactor > 0.95f) //Output jammed up (less than 5% space remaining)
                {
                    if (Session.logging) MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " Output jammed" + assembler.OutputInventory.VolumeFillFactor * 100 + " % full");
                    outputJammed = true;
                }

                if(!(inputJammed || outputJammed)) //Missing materials.  Will be eval'd on next update, as it may still pull resources
                {
                    lastQueue = assembler.GetQueue()[0];
                    if (Session.logging) MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " stopped - missing materials");
                }
            }
            runStopTick = Session.Tick; //Dumb... action runs twice/assembler
        }
        public void UnJamAssembler(GridComp gComp, AssemblerComp aComp)
        {
            var aCube = aComp.assembler as MyCubeBlock;
            var aInput = aComp.assembler.InputInventory;

            MyFixedPoint max = 0;
            MyInventoryItem? largestStack = null;
            for (int i = 0; i < aInput.ItemCount - 1; i++)
            {
                var curItem = aInput.GetItemAt(i);
                if (curItem.Value.Amount > max)
                {
                    max = curItem.Value.Amount;
                    largestStack = (MyInventoryItem)curItem;
                }
            }
            var transferAmount = MyFixedPoint.MultiplySafe(largestStack.Value.Amount, 0.5f);
            foreach (var block in gComp.Grid.Inventories)
            {
                if (block == aCube || !(block is IMyCargoContainer))
                    continue;

                if (aInput.TransferItemTo(block.GetInventory(), largestStack.Value, transferAmount))
                    break;
            }
            aComp.unJamAttempts++;
            gComp.countUnjamAttempts++;
        }
    }
}
