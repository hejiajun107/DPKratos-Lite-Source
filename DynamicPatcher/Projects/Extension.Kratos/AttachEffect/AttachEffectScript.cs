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

    [Serializable]
    public class LocationMark
    {
        public CoordStruct Location;
        public DirStruct Direction;

        public LocationMark(CoordStruct location, DirStruct direction)
        {
            this.Location = location;
            this.Direction = direction;
        }
    }

    /// <summary>
    /// AEManager
    /// </summary>
    [Serializable]
    [GlobalScriptable(typeof(TechnoExt), typeof(BulletExt))]
    [UpdateAfter(typeof(TechnoStatusScript), typeof(BulletStatusScript))]
    public class AttachEffectScript : ObjectScriptable
    {
        public AttachEffectScript(IExtension owner) : base(owner) { }

        public Pointer<ObjectClass> pOwner => pObject;

        public List<AttachEffect> AttachEffects; // 所有有效的AE
        public Dictionary<string, TimerStruct> DisableDelayTimers; // 同名AE失效后再赋予的计时器

        private List<LocationMark> locationMarks;
        private CoordStruct lastLocation; // 使者的上一次位置
        private int locationMarkDistance; // 多少格记录一个位置
        private double totleMileage; // 总里程

        private bool attachEffectOnceFlag = false; // 已经在Update事件中附加过一次section上写的AE
        private bool renderFlag = false; // Render比Update先执行，在附着对象Render时先调整替身位置，Update就不用调整
        private bool isDead = false;

        private int locationSpace; // 替身火车的车厢间距

        // 将AE转移给其他对象
        public void InheritedTo(AttachEffectScript heir)
        {
            heir.AttachEffects = this.AttachEffects;
            heir.DisableDelayTimers = this.DisableDelayTimers;

            heir.locationMarks = this.locationMarks;
            heir.locationMarkDistance = this.locationMarkDistance;
            heir.totleMileage = this.totleMileage;

            heir.attachEffectOnceFlag = this.attachEffectOnceFlag;

            heir.locationSpace = this.locationSpace;

            // 转移完成后，重置
            Awake();
        }

        public override void Awake()
        {
            this.AttachEffects = new List<AttachEffect>();
            this.DisableDelayTimers = new Dictionary<string, TimerStruct>();

            this.locationMarks = new List<LocationMark>();
            this.locationMarkDistance = 16;
            this.totleMileage = 0;

            this.locationSpace = 512;
        }

        public int Count()
        {
            return AttachEffects.Count;
        }

        public void SetLocationSpace(int cabinLenght)
        {
            this.locationSpace = cabinLenght;

            if (cabinLenght < locationMarkDistance)
            {
                this.locationMarkDistance = cabinLenght;
            }
        }

        /// <summary>
        /// 从单位自身的section中获取AE清单并附加
        /// </summary>
        /// <param name="typeData">section的AE清单</param>
        /// <param name="pHouse">强制所属</param>
        public void Attach(AttachEffectTypeData typeData)
        {
            // 清单中有AE类型
            if (null != typeData.AttachEffectTypes && typeData.AttachEffectTypes.Length > 0)
            {
                // 写在type上附加的AE，所属是自己，攻击者是自己
                Pointer<HouseClass> pHouse = IntPtr.Zero;
                Pointer<TechnoClass> pAttacker = IntPtr.Zero;
                if (pOwner.CastToTechno(out Pointer<TechnoClass> pTechno))
                {
                    pHouse = pTechno.Ref.Owner;
                    pAttacker = pTechno;
                }
                else if (pOwner.CastToBullet(out Pointer<BulletClass> pBullet))
                {
                    pHouse = pBullet.GetSourceHouse();
                    pAttacker = pBullet.Ref.Owner;
                }
                Attach(typeData.AttachEffectTypes, pHouse, pAttacker, attachEffectOnceFlag);
            }

            if (typeData.StandTrainCabinLength > 0)
            {
                SetLocationSpace(typeData.StandTrainCabinLength);
            }
        }

        /// <summary>
        /// 遍历AE清单并逐个附加
        /// </summary>
        /// <param name="aeTypes"></param>
        /// <param name="pHouse"></param>
        /// <param name="attachOnceFlag"></param>
        public void Attach(string[] aeTypes, Pointer<HouseClass> pHouse, Pointer<TechnoClass> pAttacker, bool attachOnceFlag)
        {
            if (null != aeTypes && aeTypes.Length > 0)
            {
                foreach (string type in aeTypes)
                {
                    // Logger.Log("事件{0}添加AE类型{1}", onUpdate ? "OnUpdate" : "OnInit", type);
                    Attach(type, pHouse, pAttacker, attachOnceFlag);
                }
            }
        }

        /// <summary>
        /// 按照AE的section来添加AE
        /// </summary>
        /// <param name="type">AE的section</param>
        /// <param name="pHouse">强制所属</param>
        /// <param name="pAttacker">AE来源</param>
        /// <param name="attachOnceFlag"></param>
        public void Attach(string type, Pointer<HouseClass> pHouse, Pointer<TechnoClass> pAttacker, bool attachOnceFlag)
        {
            IConfigWrapper<AttachEffectData> aeDate = Ini.GetConfig<AttachEffectData>(Ini.RulesDependency, type);
            if (attachOnceFlag && aeDate.Data.AttachOnceInTechnoType)
            {
                return;
            }
            // Logger.Log("AE {0} AttachOnceInTechnoType = {1}, AttachOnceFlag = {2}", aeType.Name, aeType.AttachOnceInTechnoType, attachOnceFlag);
            Attach(aeDate, pHouse, pAttacker);
        }

        /// <summary>
        /// 附加AE
        /// </summary>
        /// <param name="aeData">要附加的AE类型</param>
        /// <param name="pHouse">指定的所属</param>
        /// <param name="pAttacker">来源</param>
        public void Attach(IConfigWrapper<AttachEffectData> aeData, Pointer<HouseClass> pHouse, Pointer<TechnoClass> pAttacker)
        {
            if (!aeData.Data.Enable)
            {
                Logger.LogWarning($"Attempt to attach an invalid AE [{aeData.Data.Name}] to [{section}]");
                return;
            }
            // 是否在名单上
            if (!aeData.Data.CanAffectType(pOwner))
            {
                // Logger.Log($"{Game.CurrentFrame} 单位 [{section}] 不在AE [{aeData.Data.Name}] 的名单内，不能赋予");
                return;
            }
            AttachEffectData data = aeData.Data;
            // 检查叠加
            bool add = data.Cumulative == CumulativeMode.YES;
            if (!add)
            {
                // 不同攻击者是否叠加
                bool isAttackMark = data.Cumulative == CumulativeMode.ATTACKER && !pAttacker.IsNull && pAttacker.Ref.Base.IsAlive;
                // 攻击者标记AE名称相同，但可以来自不同的攻击者，可以叠加，不检查Delay
                // 检查冷却计时器
                if (!isAttackMark && DisableDelayTimers.TryGetValue(data.Name, out TimerStruct delayTimer) && delayTimer.InProgress())
                {
                    // Logger.Log($"{Game.CurrentFrame} 单位 [{section}]{pObject} 添加AE类型[{data.Name}]，该类型尚在冷却中，无法添加");
                    return;
                }
                bool find = false;
                // 检查持续时间，增减Duration
                for (int i = Count() - 1; i >= 0; i--)
                {
                    AttachEffect temp = AttachEffects[i];
                    if (data.Group < 0)
                    {
                        // 找同名
                        if (temp.AEData == data)
                        {
                            // 找到了
                            find = true;
                            if (isAttackMark)
                            {
                                if (temp.pSource.Pointer == pAttacker)
                                {
                                    // 是攻击者标记，且相同的攻击者，重置持续时间
                                    if (temp.AEData.ResetDurationOnReapply)
                                    {
                                        temp.ResetDuration();
                                        AttachEffects[i] = temp;
                                    }
                                    break;
                                }
                            }
                            else
                            {
                                // 不是攻击者标记，重置持续时间
                                if (temp.AEData.ResetDurationOnReapply)
                                {
                                    temp.ResetDuration();
                                    AttachEffects[i] = temp;
                                }
                            }
                        }
                    }
                    else
                    {
                        // 找同组
                        if (temp.IsSameGroup(data))
                        {
                            // 找到了同组
                            find = true;
                            if (data.OverrideSameGroup)
                            {
                                // 替换
                                // Logger.Log($"{Game.CurrentFrame} 单位 [{section}]{pObject} 添加AE类型[{data.Name}]，发现同组{data.Group}已存在有AE类型[{temp.AEData.Name}]，执行替换");
                                // 关闭发现的同组
                                CoordStruct location = pOwner.Ref.Base.GetCoords();
                                temp.Disable(location);
                                add = true;
                                continue; // 全部替换
                            }
                            else
                            {
                                // Logger.Log($"{Game.CurrentFrame} 单位 [{section}]{pObject} 添加AE类型[{data.Name}]，发现同组{data.Group}已存在有AE类型[{temp.AEData.Name}]，调整持续时间{data.Duration}");
                                // 调整持续时间
                                temp.MergeDuation(data.Duration);
                                AttachEffects[i] = temp;
                            }
                        }
                    }
                }
                // 没找到同类或同组，可以添加新的实例
                add = add || !find;
            }
            // 可以添加AE
            if (add && data.GetDuration() != 0)
            {
                AttachEffect ae = aeData.CreateAE();
                // 入队
                int index = FindInsertIndex(ae);
                // Logger.Log($"{Game.CurrentFrame} 单位 [{section}]{pObject} 添加AE类型[{data.Name}]，加入队列，插入位置{index}");
                AttachEffects.Insert(index, ae);
                // 激活
                ae.Enable(pOwner, pHouse, pAttacker);
            }
        }

        /// <summary>
        /// 根据火车的位置插入AE列表中
        /// </summary>
        /// <param name="ae"></param>
        /// <returns></returns>
        public int FindInsertIndex(AttachEffect ae)
        {
            StandData standData = null;
            if (null != ae.Stand && (standData = ae.Stand.Data).IsTrain)
            {
                int index = -1;
                // 插入头还是尾
                if (standData.CabinHead)
                {
                    // 插入队列末位
                    // 检查是否有分组
                    if (standData.CabinGroup > -1)
                    {
                        // 倒着找自己的分组
                        for (int j = Count() - 1; j >= 0; j--)
                        {
                            AttachEffect temp = AttachEffects[j];
                            Stand tempStand = null;
                            if (null != (tempStand = temp.Stand))
                            {
                                if (standData.CabinGroup == tempStand.Data.CabinGroup)
                                {
                                    // 找到组员
                                    index = j;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // 插入队列首位
                    index = 0;
                    // 检查是否有分组
                    if (standData.CabinGroup > -1)
                    {
                        // 顺着找自己的分组
                        for (int j = 0; j < Count(); j++)
                        {
                            AttachEffect temp = AttachEffects[j];
                            Stand tempStand = null;
                            if (null != (tempStand = temp.Stand))
                            {
                                if (standData.CabinGroup == tempStand.Data.CabinGroup)
                                {
                                    // 找到组员
                                    index = j;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (index > -1)
                {
                    return index;
                }
            }
            return 0;
        }

        private CoordStruct MarkLocation()
        {
            CoordStruct location = pOwner.Ref.Base.GetCoords();
            if (default == lastLocation)
            {
                lastLocation = location;
            }
            double mileage = location.DistanceFrom(lastLocation);
            if (mileage > locationMarkDistance)
            {
                lastLocation = location;
                double tempMileage = totleMileage + mileage;
                // 记录下当前的位置信息
                LocationMark locationMark = pOwner.GetRelativeLocation(default, 0, false, false);
                // 入队
                locationMarks.Insert(0, locationMark);

                // 检查数量超过就删除最后一个
                if (tempMileage > (Count() + 1) * locationSpace)
                {
                    locationMarks.RemoveAt(locationMarks.Count - 1);
                }
                else
                {
                    totleMileage = tempMileage;
                }
            }
            return location;
        }

        private void UpdateStandLocation(Stand stand, ref int markIndex)
        {
            if (stand.Data.IsTrain)
            {
                // 查找可以用的记录点
                double length = 0;
                LocationMark preMark = null;
                for (int j = markIndex; j < locationMarks.Count; j++)
                {
                    markIndex = j;
                    LocationMark mark = locationMarks[j];
                    if (null == preMark)
                    {
                        preMark = mark;
                        continue;
                    }
                    length += mark.Location.DistanceFrom(preMark.Location);
                    preMark = mark;
                    if (length >= locationSpace)
                    {
                        break;
                    }
                }

                if (null != preMark)
                {
                    stand.UpdateLocation(preMark);
                    return;
                }
            }
            // 获取挂载对象的位置和方向
            LocationMark locationMark = pObject.GetRelativeLocation(stand.Data.Offset, stand.Data.Direction, stand.Data.IsOnTurret, stand.Data.IsOnWorld);
            stand.UpdateLocation(locationMark);
        }

        public bool HasSpace()
        {
            return totleMileage > Count() * locationSpace;
        }

        public bool HasStand()
        {
            foreach (AttachEffect ae in AttachEffects)
            {
                if (null != ae.Stand && ae.IsActive())
                {
                    return true;
                }
            }
            return false;
        }

        public CrateBuffData CountAttachStatusMultiplier()
        {
            CrateBuffData multiplier = new CrateBuffData();
            foreach (AttachEffect ae in AttachEffects)
            {
                // if (null != ae.AttachStatus && ae.AttachStatus.Active)
                // {
                //     multiplier.FirepowerMultiplier *= ae.AttachStatus.Type.FirepowerMultiplier;
                //     multiplier.ArmorMultiplier *= ae.AttachStatus.Type.ArmorMultiplier;
                //     multiplier.SpeedMultiplier *= ae.AttachStatus.Type.SpeedMultiplier;
                //     multiplier.ROFMultiplier *= ae.AttachStatus.Type.ROFMultiplier;
                //     multiplier.Cloakable |= ae.AttachStatus.Type.Cloakable;
                //     multiplier.ForceDecloak |= ae.AttachStatus.Type.ForceDecloak;
                //     // Logger.Log("Count {0}, ae {1}", multiplier, ae.AttachStatus.Type);
                // }
            }
            return multiplier;
        }

        public override void OnRender()
        {
            isDead = pObject.IsDead();
            CoordStruct location = pOwner.Ref.Base.GetCoords();
            for (int i = Count() - 1; i >= 0; i--)
            {
                AttachEffect ae = AttachEffects[i];
                if (ae.IsActive())
                {
                    ae.OnRender(location);
                }
            }
        }

        public override void OnRenderEnd()
        {
            renderFlag = !isDead;
            if (renderFlag)
            {
                // 记录下位置
                CoordStruct location = MarkLocation();
                // 更新替身的位置
                int markIndex = 0;
                for (int i = Count() - 1; i >= 0; i--)
                {
                    AttachEffect ae = AttachEffects[i];
                    if (ae.IsActive())
                    {
                        // 如果是替身，额外执行替身的定位操作
                        if (null != ae.Stand && ae.Stand.IsAlive())
                        {
                            UpdateStandLocation(ae.Stand, ref markIndex); // 调整位置
                        }
                        ae.OnRenderEnd(location);
                    }
                }
            }
        }

        public override void OnUpdate()
        {
            isDead = pObject.IsDead();
            // 添加Section上记录的AE
            if (!pObject.IsDeadOrInvisible())
            {
                AttachEffectTypeData aeTypeData = Ini.GetConfig<AttachEffectTypeData>(Ini.RulesDependency, section).Data;
                Attach(aeTypeData);
                this.attachEffectOnceFlag = true;
            }

            // 记录下位置
            CoordStruct location = pOwner.Ref.Base.GetCoords();
            if (!renderFlag)
            {
                location = MarkLocation();
            }
            // 逐个触发有效的AEbuff，并移除无效的AEbuff
            int markIndex = 0;
            for (int i = Count() - 1; i >= 0; i--)
            {
                AttachEffect ae = AttachEffects[i];
                if (ae.IsActive())
                {
                    if (!renderFlag && null != ae.Stand && ae.Stand.IsAlive())
                    {
                        // 替身不需要渲染时，在update中调整替身的位置
                        UpdateStandLocation(ae.Stand, ref markIndex);
                    }
                    // Logger.Log($"{Game.CurrentFrame} - {pOwner} [{pOwner.Ref.Type.Ref.Base.ID}] {ae.Type.Name} 执行更新");
                    ae.OnUpdate(location, isDead);
                }
                else
                {
                    AttachEffectData data = ae.AEData;
                    int delay = data.RandomDelay.GetRandomValue(ae.AEData.Delay);
                    if (delay > 0)
                    {
                        DisableDelayTimers[data.Name] = new TimerStruct(delay);
                    }
                    // Logger.Log($"{Game.CurrentFrame} 单位 [{section}]{pObject} 持有AE类型[{data.Name}] 失效，从列表中移除，不可再赋予延迟 {delay}");
                    ae.Disable(location);
                    AttachEffects.Remove(ae);
                    // 如果有Next，则赋予新的AE
                    string nextAE = data.Next;
                    if (!string.IsNullOrEmpty(nextAE))
                    {
                        // Logger.Log($"{Game.CurrentFrame} 单位 [{section}]{pObject} 添加AE类型[{data.Name}]的Next类型[{nextAE}]");
                        Attach(nextAE, ae.pSourceHouse, ae.pSource, false);
                    }
                }
            }
            renderFlag = false;
        }

        public override void OnLateUpdate()
        {
            isDead = pObject.IsDead();
            CoordStruct location = pOwner.Ref.Base.GetCoords();
            for (int i = Count() - 1; i >= 0; i--)
            {
                AttachEffect ae = AttachEffects[i];
                if (ae.IsActive())
                {
                    ae.OnLateUpdate(location, isDead);
                }
            }
        }

        public override void OnTemporalUpdate(Pointer<TemporalClass> pTemporal)
        {
            for (int i = Count() - 1; i >= 0; i--)
            {
                AttachEffect ae = AttachEffects[i];
                if (ae.IsActive())
                {
                    ae.OnTemporalUpdate(pTemporal);
                }
            }
        }

        public override void OnPut(Pointer<CoordStruct> pCoord, DirType dirType)
        {
            foreach (AttachEffect ae in AttachEffects)
            {
                if (ae.IsActive())
                {
                    ae.OnPut(pCoord, dirType);
                }
            }
        }

        public override void OnRemove()
        {
            CoordStruct location = pOwner.Ref.Base.GetCoords();
            foreach (AttachEffect ae in AttachEffects)
            {
                if (ae.AEData.DiscardOnEntry)
                {
                    ae.Disable(location);
                }
                else
                {
                    if (ae.IsActive())
                    {
                        ae.OnRemove();
                    }
                }
            }
        }

        public override void OnReceiveDamage(Pointer<int> pDamage, int distanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
            Pointer<ObjectClass> pAttacker, bool ignoreDefenses, bool preventPassengerEscape, Pointer<HouseClass> pAttackingHouse)
        {
            foreach (AttachEffect ae in AttachEffects)
            {
                if (ae.IsActive())
                {
                    ae.OnReceiveDamage(pDamage, distanceFromEpicenter, pWH, pAttacker, ignoreDefenses, preventPassengerEscape, pAttackingHouse);
                }
            }

        }

        public override void OnDetonate(Pointer<CoordStruct> pCoords)
        {
            DestroyAll(pCoords.Data);
        }

        public override void OnReceiveDamageDestroy()
        {
            DestroyAll(pObject.Ref.Base.GetCoords());
        }

        public void DestroyAll(CoordStruct location)
        {
            foreach (AttachEffect ae in AttachEffects)
            {
                if (ae.IsActive())
                {
                    ae.OnReceiveDamageDestroy();
                }
            }
        }

        public override void OnUnInit()
        {
            CoordStruct location = pOwner.Ref.Base.GetCoords();
            for (int i = Count() - 1; i >= 0; i--)
            {
                AttachEffect ae = AttachEffects[i];
                if (ae.IsAnyAlive())
                {
                    // Logger.Log($"{Game.CurrentFrame} - {ae.Type.Name} 注销，执行关闭");
                    ae.Disable(location);
                }
                // Logger.Log($"{Game.CurrentFrame} - {ae.Type.Name} 注销，移出列表");
                AttachEffects.Remove(ae);
            }
            AttachEffects.Clear();
        }

        public override void OnGuardCommand()
        {
            foreach (AttachEffect ae in AttachEffects)
            {
                if (ae.IsActive())
                {
                    ae.OnGuardCommand();
                }
            }
        }

        public override void OnStopCommand()
        {
            foreach (AttachEffect ae in AttachEffects)
            {
                if (ae.IsActive())
                {
                    ae.OnStopCommand();
                }
            }
        }
    }

}
