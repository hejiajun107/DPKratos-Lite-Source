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

    public partial class TechnoStatusScript : TechnoScriptable
    {
        public static Dictionary<TechnoExt, StandData> StandArray = new Dictionary<TechnoExt, StandData>();

        public SwizzleablePointer<TechnoClass> MyMaster = new SwizzleablePointer<TechnoClass>(IntPtr.Zero);
        public StandData StandData;

        public bool VirtualUnit;

        public void OnPut_StandUnit(Pointer<CoordStruct> pCoord, DirType dirType)
        {
            // Logger.Log($"{Game.CurrentFrame}, [{section}]{pTechno} put on the map");
            if (!MyMaster.IsNull)
            {
                // Logger.Log($"{Game.CurrentFrame}, [{section}]{pTechno} is stand, add to list.");
                StandArray.Add(Owner, StandData);
            }
        }

        public void OnReceiveDamageDestroy_StandUnit()
        {
            // Logger.Log($"{Game.CurrentFrame}, [{section}]{pTechno} take damage to dead");
            if (!MyMaster.IsNull)
            {
                // Logger.Log($"{Game.CurrentFrame}, [{section}]{pTechno} is stand, remove form list.");
                StandArray.Remove(Owner);
            }
        }

        public void OnRemove_StandUnit()
        {
            // Logger.Log($"{Game.CurrentFrame}, [{section}]{pTechno} remove on the map");
            if (!MyMaster.IsNull)
            {
                // Logger.Log($"{Game.CurrentFrame}, [{section}]{pTechno} is stand, remove form list.");
                StandArray.Remove(Owner);
            }
        }

        public bool OnSelect_VirtualUnit()
        {
            return !VirtualUnit;
        }

        public unsafe void OnReceiveDamage_Stand(Pointer<int> pDamage, int distanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
                    Pointer<ObjectClass> pAttacker, bool ignoreDefenses, bool preventPassengerEscape, Pointer<HouseClass> pAttackingHouse)
        {
            // 无视防御的真实伤害不做任何分摊
            if (!ignoreDefenses)
            {
                if (null != StandData)
                {
                    if (pDamage.Ref >= 0 || StandData.AllowShareRepair)
                    {
                        // I'm stand
                        if (StandData.Immune)
                        {
                            // 消除伤害会让替身无法被销毁，如果是无视防御的伤害不应被消去
                            pDamage.Ref = 0;
                        }
                        else if (StandData.DamageToMaster > 0 && !MyMaster.Pointer.IsDeadOrInvisible())
                        {
                            int damage = pDamage.Ref;
                            // 分摊伤害给使者
                            double to = damage * StandData.DamageToMaster;
                            pDamage.Ref = (int)(damage - to);
                            MyMaster.Ref.Base.ReceiveDamage((int)to, distanceFromEpicenter, pWH, pAttacker, ignoreDefenses, preventPassengerEscape, pAttackingHouse);
                        }
                    }
                }
                else
                {
                    int damage = pDamage.Ref;
                    // I'm JoJO
                    if (pTechno.TryGetComponent<AttachEffectScript>(out AttachEffectScript AEM))
                    {
                        foreach (AttachEffect ae in AEM.AttachEffects)
                        {
                            Stand stand = ae.Stand;
                            if (null != stand && stand.IsAlive() && !stand.Data.IsTrain && !stand.Data.Immune
                                && (damage >= 0 || stand.Data.AllowShareRepair) && stand.Data.DamageFromMaster > 0)
                            {
                                // 找到一个可以分摊伤害的替身
                                double to = damage * stand.Data.DamageFromMaster;
                                damage -= (int)to;
                                stand.pStand.Ref.Base.ReceiveDamage((int)to, distanceFromEpicenter, pWH, pAttacker, ignoreDefenses, preventPassengerEscape, pAttackingHouse);
                            }
                        }
                    }
                    pDamage.Ref = damage;
                }
            }
        }

        public unsafe bool OnRegisterDestruction_StandUnit(Pointer<TechnoClass> pKiller, int cost)
        {
            if (cost != 0 && !pKiller.IsDead())
            {
                // Logger.Log("{0} 被 {1} 杀死了，价值 {2}，杀手{3}，等级{4}", pTechno.Ref.Type.Ref.Base.Base.ID, pKiller.Ref.Type.Ref.Base.Base.ID, cost, pKiller.Ref.Type.Ref.Trainable ? "可以升级" : "不可训练", pKiller.Ref.Veterancy.Veterancy);
                // Killer是Stand，而且Master可训练
                if (pKiller.AmIStand(out TechnoStatusScript standStatus, out StandData standData) && standStatus.MyMaster.Ref.Type.Ref.Trainable)
                {
                    Pointer<TechnoClass> pMaster = standStatus.MyMaster;
                    int transExp = 0;
                    if (pKiller.Ref.Type.Ref.Trainable)
                    {
                        // 替身可以训练，经验部分转给使者
                        int exp = cost;
                        // 替身已经满级
                        if (!pKiller.Ref.Veterancy.IsElite())
                        {
                            transExp = cost;
                            exp = 0;
                            // Logger.Log("替身{0}已经满级，全部经验{1}转给使者{2}", pKiller.Ref.Type.Ref.Base.Base.ID, transExp, pMaster.Ref.Type.Ref.Base.Base.ID);
                        }
                        if (!pMaster.Ref.Veterancy.IsElite())
                        {
                            // 使者还能获得经验，转移部分给使者
                            transExp = (int)(cost * standData.ExperienceToMaster);
                            exp -= transExp;
                            // Logger.Log("使者{0}没有满级，经验{1}转给使者，替身{2}享用{3}", pMaster.Ref.Type.Ref.Base.Base.ID, transExp, pKiller.Ref.Type.Ref.Base.Base.ID, exp);
                        }
                        // 剩余部分自己享用
                        if (exp != 0)
                        {
                            int technoCost = pKiller.Ref.Type.Ref.Base.GetActualCost(pKiller.Ref.Owner);
                            pKiller.Ref.Veterancy.Add(technoCost, exp);
                            // Logger.Log("替身{0}享用剩余经验{1}", pKiller.Ref.Type.Ref.Base.Base.ID, exp);
                        }
                    }
                    else
                    {
                        // 替身不能训练，经验全部转给使者
                        transExp = cost;
                        // Logger.Log("替身{0}不能训练，全部经验{1}转给使者{2}", pKiller.Ref.Type.Ref.Base.Base.ID, transExp, pMaster.Ref.Type.Ref.Base.Base.ID);
                    }
                    if (transExp != 0)
                    {
                        int technoCost = pMaster.Ref.Type.Ref.Base.GetActualCost(pMaster.Ref.Owner);
                        pMaster.Ref.Veterancy.Add(technoCost, transExp);
                        // Logger.Log("使者{0}享用分享经验{1}", pMaster.Ref.Type.Ref.Base.Base.ID, transExp);
                    }

                    return true;
                }
            }
            return false;
        }

    }
}