﻿using Sandbox.Game.Entities.Cube;

namespace SentisOptimisations
{
    public static class BlockUtils
    {
        public static int GetPCU(MySlimBlock block)
        {
            int num = 1;
            if (block.ComponentStack.IsFunctional)
                num = block.BlockDefinition.PCU;
            return num;
        }
    }
}