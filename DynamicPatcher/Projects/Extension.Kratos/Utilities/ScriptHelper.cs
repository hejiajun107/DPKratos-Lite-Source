using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DynamicPatcher;
using PatcherYRpp;
using Extension.Components;
using Extension.Ext;
using Extension.INI;
using Extension.Script;
using Extension.Utilities;

namespace Extension.Utilities
{

    public static class ScriptHelper
    {
        #region AnimClass
        // 泛型
        public static T GetComponent<T>(this Pointer<AnimClass> pAnim) where T : Component
        {
            if (!pAnim.IsNull)
            {
                AnimExt ext = AnimExt.ExtMap.Find(pAnim);
                if (null != ext)
                {
                    return ext.GameObject.GetComponent<T>();
                }
            }
            return null;
        }
        public static bool TryGetComponent<T>(this Pointer<AnimClass> pAnim, out T script) where T : Component
        {
            return null != (script = pAnim.GetComponent<T>());
        }

        // 便利
        public static AnimStatusScript GetStatus(this Pointer<AnimClass> pAnim)
        {
            return pAnim.GetComponent<AnimStatusScript>();
        }

        public static bool TryGetStatus(this Pointer<AnimClass> pAnim, out AnimStatusScript bulletStatus)
        {
            return pAnim.TryGetComponent<AnimStatusScript>(out bulletStatus);
        }
        #endregion

        #region TechnoClass
        // 泛型
        public static T GetComponent<T>(this Pointer<TechnoClass> pTechno) where T : Component
        {
            if (!pTechno.IsNull)
            {
                TechnoExt ext = TechnoExt.ExtMap.Find(pTechno);
                if (null != ext)
                {
                    return ext.GameObject.GetComponent<T>();
                }
            }
            return null;
        }
        public static bool TryGetComponent<T>(this Pointer<TechnoClass> pTechno, out T script) where T : Component
        {
            return null != (script = pTechno.GetComponent<T>());
        }

        // 便利
        public static TechnoStatusScript GetStatus(this Pointer<TechnoClass> pTechno)
        {
            return pTechno.GetComponent<TechnoStatusScript>();
        }

        public static bool TryGetStatus(this Pointer<TechnoClass> pTechno, out TechnoStatusScript technoStatus)
        {
            return pTechno.TryGetComponent<TechnoStatusScript>(out technoStatus);
        }

        public static TechnoStatusScript GetTechnoStatus(this Pointer<AbstractClass> pTarget)
        {
            if (pTarget.CastToTechno(out Pointer<TechnoClass> pTechno))
            {
                return pTechno.GetStatus();
            }
            return null;
        }

        public static TechnoStatusScript GetTechnoStatus(this Pointer<ObjectClass> pObject)
        {
            if (pObject.CastToTechno(out Pointer<TechnoClass> pTechno))
            {
                return pTechno.GetStatus();
            }
            return null;
        }

        public static bool TryGetTechnoStatus(this Pointer<AbstractClass> pTarget, out TechnoStatusScript technoStatus)
        {
            technoStatus = null;
            return pTarget.CastToTechno(out Pointer<TechnoClass> pTechno) && pTechno.TryGetStatus(out technoStatus);
        }

        public static bool TryGetTechnoStatus(this Pointer<ObjectClass> pObject, out TechnoStatusScript technoStatus)
        {
            technoStatus = null;
            return pObject.CastToTechno(out Pointer<TechnoClass> pTechno) && pTechno.TryGetStatus(out technoStatus);
        }
        #endregion

        #region BulletClass
        // 泛型
        public static T GetComponent<T>(this Pointer<BulletClass> pBullet) where T : Component
        {
            if (!pBullet.IsNull)
            {
                BulletExt ext = BulletExt.ExtMap.Find(pBullet);
                if (null != ext)
                {
                    return ext.GameObject.GetComponent<T>();
                }
            }
            return null;
        }
        public static bool TryGetComponent<T>(this Pointer<BulletClass> pBullet, out T script) where T : Component
        {
            return null != (script = pBullet.GetComponent<T>());
        }

        // 便利
        public static BulletStatusScript GetStatus(this Pointer<BulletClass> pBullet)
        {
            return pBullet.GetComponent<BulletStatusScript>();
        }

        public static bool TryGetStatus(this Pointer<BulletClass> pBullet, out BulletStatusScript bulletStatus)
        {
            return pBullet.TryGetComponent<BulletStatusScript>(out bulletStatus);
        }

        public static BulletStatusScript GetBulletStatus(this Pointer<AbstractClass> pTarget)
        {
            if (pTarget.CastIf(AbstractType.Bullet, out Pointer<BulletClass> pBullet))
            {
                return pBullet.GetStatus();
            }
            return null;
        }

        public static BulletStatusScript GetBulletStatus(this Pointer<ObjectClass> pObject)
        {
            if (pObject.CastToBullet(out Pointer<BulletClass> pBullet))
            {
                return pBullet.GetStatus();
            }
            return null;
        }

        public static bool TryGetBulletStatus(this Pointer<AbstractClass> pTarget, out BulletStatusScript bulletStatus)
        {
            bulletStatus = null;
            return pTarget.CastIf(AbstractType.Bullet, out Pointer<BulletClass> pBullet) && pBullet.TryGetStatus(out bulletStatus);
        }

        public static bool TryGetBulletStatus(this Pointer<ObjectClass> pObject, out BulletStatusScript bulletStatus)
        {
            bulletStatus = null;
            return pObject.CastToBullet(out Pointer<BulletClass> pBullet) && pBullet.TryGetStatus(out bulletStatus);
        }
        #endregion

        #region 替身
        public static bool AmIStand(this Pointer<TechnoClass> pStand)
        {
            return pStand.AmIStand(out TechnoStatusScript status, out StandData data);
        }

        public static bool AmIStand(this Pointer<TechnoClass> pStand, out StandData data)
        {
            return pStand.AmIStand(out TechnoStatusScript status, out data);
        }

        public static bool AmIStand(this Pointer<TechnoClass> pStand, out TechnoStatusScript standStatus, out StandData standData)
        {
            standStatus = null;
            standData = null;
            if (pStand.TryGetStatus(out TechnoStatusScript technoStatus)
                    && !technoStatus.MyMaster.IsNull)
            {
                standData = technoStatus.StandData;
                return true;
            }
            return false;
        }

        #endregion
        #region 黑洞
        public static bool TryGetBlackHoleState(this Pointer<ObjectClass> pObject, out BlackHoleState blackHoleState)
        {
            blackHoleState = null;
            if (!pObject.IsDeadOrInvisible())
            {
                if (pObject.TryGetTechnoStatus(out TechnoStatusScript technoStatus))
                {
                    blackHoleState = technoStatus.BlackHoleState;
                    return true;
                }
                else if (pObject.TryGetBulletStatus(out BulletStatusScript bulletStatus))
                {
                    blackHoleState = bulletStatus.BlackHoleState;
                    return true;
                }
            }
            return false;
        }
        #endregion

    }

}