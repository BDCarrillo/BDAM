using Sandbox.Game.Entities;
using Sandbox.ModAPI;
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
            if (Session.logging)
                MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " started");
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
                var inputInventory = assembler.InputInventory;
                var inputMaxVolume = (float)inputInventory.MaxVolume * 1000;
                var inputVolume = (float)inputInventory.CurrentVolume * 1000;
                if (inputVolume/inputMaxVolume > 0.95f) //Input jammed up (less than 5% space remaining)
                {
                    if (Session.logging)
                        MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " Input jammed" + inputVolume + "/" + inputMaxVolume);
                    inputJammed = true;
                }

                var outputInventory = assembler.OutputInventory;
                var outputMaxVolume = (float)outputInventory.MaxVolume * 1000;
                var outputVolume = (float)outputInventory.CurrentVolume * 1000;
                if (outputVolume/outputMaxVolume > 0.95f) //Output jammed up (less than 5% space remaining)
                {
                    if (Session.logging)
                        MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " Output jammed" + outputVolume + "/" + outputMaxVolume);
                    outputJammed = true;
                }

                if(!(inputJammed || outputJammed))
                {
                    var queue = assembler.GetQueue();
                    lastQueue = queue[0];
                    missingMatsInt++;
                    if (Session.logging)
                        MyLog.Default.WriteLineAndConsole(Session.modName + assembler.CustomName + " stopped - missing materials");
                }
            }
            runStopTick = Session.Tick; //Dumb... action runs twice/assembler
        }
        public void UnJamAssembler(GridComp gComp, AssemblerComp aComp)
        {
            var grid = gComp.Grid;
            var aCube = aComp.assembler as MyCubeBlock;
            var aInput = aComp.assembler.InputInventory;
            var aFirstItem = aInput.GetItemAt(0);

            if (aFirstItem == null)
                return;

            foreach (var block in grid.Inventories)
            {
                if (block == aCube || !(block is IMyCargoContainer))
                    continue;

                var blockInv = block.GetInventory();
                if (blockInv == null)
                    continue;

                bool canTransfer = aInput.CanTransferItemTo(blockInv, aFirstItem.Value.Type);
                var transferAmount = VRage.MyFixedPoint.MultiplySafe(aFirstItem.Value.Amount, 0.5f);
                if (canTransfer)
                {
                    if (aInput.TransferItemTo(blockInv, aFirstItem.Value, transferAmount))
                        break;
                }
            }
            aComp.unJamAttempts++;
            gComp.countUnjamAttempts++;
        }
    }
}
