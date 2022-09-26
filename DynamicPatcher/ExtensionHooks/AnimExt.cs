﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicPatcher;
using PatcherYRpp;
using PatcherYRpp.Utilities;
using Extension.Ext;
using Extension.Utilities;
using Extension.Script;

namespace ExtensionHooks
{
    public class AnimExtHooks
    {
#if USE_ANIM_EXT
        [Hook(HookType.AresHook, Address = 0x422126, Size = 5)]
        [Hook(HookType.AresHook, Address = 0x4228D2, Size = 5)]
        [Hook(HookType.AresHook, Address = 0x422707, Size = 5)]
        public static unsafe UInt32 AnimClass_CTOR(REGISTERS* R)
        {
            return AnimExt.AnimClass_CTOR(R);
        }

        [Hook(HookType.AresHook, Address = 0x4228E0, Size = 5)]
        public static unsafe UInt32 AnimClass_DTOR(REGISTERS* R)
        {
            return AnimExt.AnimClass_DTOR(R);
        }

        [Hook(HookType.AresHook, Address = 0x425280, Size = 5)]
        [Hook(HookType.AresHook, Address = 0x4253B0, Size = 5)]
        public static unsafe UInt32 AnimClass_SaveLoad_Prefix(REGISTERS* R)
        {
            return AnimExt.AnimClass_SaveLoad_Prefix(R);
        }

        [Hook(HookType.AresHook, Address = 0x4253A2, Size = 7)]
        [Hook(HookType.AresHook, Address = 0x425358, Size = 7)]
        [Hook(HookType.AresHook, Address = 0x425391, Size = 7)]
        public static unsafe UInt32 AnimClass_Load_Suffix(REGISTERS* R)
        {
            return AnimExt.AnimClass_Load_Suffix(R);
        }

        [Hook(HookType.AresHook, Address = 0x4253FF, Size = 5)]
        public static unsafe UInt32 AnimClass_Save_Suffix(REGISTERS* R)
        {
            return AnimExt.AnimClass_Save_Suffix(R);
        }

#endif
        
        [Hook(HookType.AresHook, Address = 0x423630, Size = 6)]
        public static unsafe UInt32 AnimClass_Draw_It(REGISTERS* R)
        {
            //Logger.Log("Hook 0x00423130 calling...");
            Pointer<AnimClass> pAnim = (IntPtr)R->ESI;
            // R->EBP = ExHelper.ColorAdd2RGB565(new ColorStruct(255, 0, 0));
            // R->Stack<uint>(0x38, 500);
            // Logger.Log($"{Game.CurrentFrame} {R->Stack<uint>(0xFC)} {R->Stack<uint>(0x14 + 0x20 + 0x1C)} {R->Stack<uint>(0x14 - 0x20 - 0x1C)}");
            // Logger.Log($"{Game.CurrentFrame} - {pAnim.Ref.Type.Ref.Base.Base.ID} is Drawing. color = {R->EBP}, bright = {R->Stack<int>(0x38)}, pTechno= {R->Stack<uint>(0x14)}");
            if (!pAnim.IsNull && pAnim.Ref.IsBuildingAnim)
            {
                CoordStruct location = pAnim.Ref.Base.Base.GetCoords();
                if (MapClass.Instance.TryGetCellAt(location, out Pointer<CellClass> pCell))
                {
                    Pointer<BuildingClass> pBuilding = pCell.Ref.GetBuilding();
                    if (!pBuilding.IsNull && pBuilding.Convert<TechnoClass>().TryGetStatus(out TechnoStatusScript status))
                    {
                        status.TechnoClass_DrawSHP_Paintball_BuildAnim(R);
                        // TechnoExt ext = TechnoExt.ExtMap.Find(pBuilding.Convert<TechnoClass>());
                        // ext?.TechnoClass_DrawSHP_Paintball_BuildAnim(R);
                    }
                }
            }
            return 0;
        }

    }
}
