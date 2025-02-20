using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using DynamicPatcher;
using PatcherYRpp;
using Extension.Ext;
using Extension.INI;
using Extension.Utilities;

namespace Extension.Script
{

    public partial class AnimStatusScript
    {

        public bool OverrideExpireAnimOnWater()
        {
            ExpireAnimData expireAnimData = Ini.GetConfig<ExpireAnimData>(Ini.ArtDependency, section).Data;
            if (!expireAnimData.ExpireAnimOnWater.IsNullOrEmptyOrNone())
            {
                // Logger.Log($"{Game.CurrentFrame} 试图接管 落水动画 {animType}");
                Pointer<AnimTypeClass> pNewType = AnimTypeClass.ABSTRACTTYPE_ARRAY.Find(expireAnimData.ExpireAnimOnWater);
                if (!pNewType.IsNull)
                {
                    // Logger.Log($"{Game.CurrentFrame} 试图创建新的落水动画 {animType}");
                    Pointer<AnimClass> pNewAnim = YRMemory.Create<AnimClass>(pNewType, pAnim.Ref.Base.Base.GetCoords());
                    pNewAnim.Ref.Owner = pAnim.Ref.Owner;
                }
                return true; // skip create anim
            }
            return false;
        }

    }
}
