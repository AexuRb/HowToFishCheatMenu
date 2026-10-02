using System;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>
	/// 补丁零装箱原则：
	/// - 写 auto-property backing field 用编译一次的表达式委托（FieldInfo.SetValue 会装箱值类型）
	/// - 引用类型字段用缓存 FieldInfo（GetValue 不装箱）
	/// - 移速用 Harmony ref 注入直接改字段，方法结束后还原，避免复利放大
	/// 注意字段注入名规则："___" + 字段原名，游戏字段带下划线所以是 ____walkSpeed。
	/// </summary>
	public static class Patches
	{
		private static Action<Bait, float> _setBaitCatchTime;
		private static Action<Weapon, int> _setWeaponAmmo;

		public static void Init()
		{
			FieldInfo catchTime = AccessTools.Field(typeof(Bait), "<RandomizedCatchTime>k__BackingField");
			FieldInfo ammo = AccessTools.Field(typeof(Weapon), "<Ammo>k__BackingField");
			if (catchTime != null)
			{
				_setBaitCatchTime = CreateSetter<Bait, float>(catchTime);
			}
			if (ammo != null)
			{
				_setWeaponAmmo = CreateSetter<Weapon, int>(ammo);
			}
		}

		private static Action<T, V> CreateSetter<T, V>(FieldInfo field)
		{
			ParameterExpression target = Expression.Parameter(typeof(T));
			ParameterExpression value = Expression.Parameter(typeof(V));
			BinaryExpression assign = Expression.Assign(Expression.Field(target, field), value);
			return Expression.Lambda<Action<T, V>>(assign, target, value).Compile();
		}

		[HarmonyPatch(typeof(CreatureManager), "FindFishForBait")]
		private static class Bait_InstantBite
		{
			// 不打 get_RandomizedCatchTime（微型 getter 会被 Mono JIT 内联进唯一调用点），
			// 改在 FindFishForBait 入口直接写 backing field，逻辑其余部分保持原样
			private static void Prefix(Bait bait)
			{
				if (CheatCore.InstantBite && _setBaitCatchTime != null)
				{
					_setBaitCatchTime(bait, 0f);
				}
			}
		}

		[HarmonyPatch(typeof(PlayerMovement), "UpdateMoveSpeed")]
		private static class Movement_Speed
		{
			private static void Prefix(ref float ____walkSpeed, ref float ____sprintSpeed, out float __state)
			{
				float m = CheatCore.SpeedMulti;
				if (m != 1f)
				{
					__state = m;
					____walkSpeed *= m;
					____sprintSpeed *= m;
					return;
				}
				__state = 1f;
			}

			private static void Postfix(ref float ____walkSpeed, ref float ____sprintSpeed, float __state)
			{
				if (__state != 1f)
				{
					____walkSpeed /= __state;
					____sprintSpeed /= __state;
				}
			}
		}

		[HarmonyPatch(typeof(PlayerMovement), "Jump")]
		private static class Movement_Jump
		{
			// Rigidbody 是引用类型，FieldInfo.GetValue 不装箱
			private static readonly FieldInfo RigField = AccessTools.Field(typeof(PlayerMovement), "_rig");

			private static void Postfix(PlayerMovement __instance)
			{
				float m = CheatCore.JumpMulti;
				if (m == 1f || RigField == null)
				{
					return;
				}
				Rigidbody rig = RigField.GetValue(__instance) as Rigidbody;
				if (!rig)
				{
					return;
				}
				Vector3 v = rig.linearVelocity;
				if (v.y > 0.05f)
				{
					v.y *= m;
					rig.linearVelocity = v;
				}
			}
		}

		[HarmonyPatch(typeof(Weapon), "Shoot")]
		private static class Weapon_InfiniteAmmo
		{
			// 开枪扣弹后把弹匣顶回满，同时阻止 Ammo==0 触发的自动装填队列
			private static void Postfix(Weapon __instance)
			{
				if (!CheatCore.InfiniteAmmo || _setWeaponAmmo == null)
				{
					return;
				}
				Attachments att = __instance.Attachments;
				if (att)
				{
					_setWeaponAmmo(__instance, att.AmmoPerMag);
				}
			}
		}

		// ================= 2.1 新增补丁 =================

		[HarmonyPatch(typeof(LocalCasino), "ServerUpdateRouletteGame")]
		private static class Casino_RouletteGuide
		{
			// 轮盘平滑引导：颜色是纯角度映射（槽宽 9.73°，槽0=绿、奇=黑、偶=红），
			// 每个服务器物理 tick 把球绕轮盘轴朝最近的目标色槽位缓动，球自然停稳后
			// 游戏自己算出的就是下注色，视觉与结果一致。ServerRouletteResult 强制补丁保留作兜底。
			private static readonly FieldInfo BallField = AccessTools.Field(typeof(LocalCasino), "_ball");
			private static readonly FieldInfo WheelField = AccessTools.Field(typeof(LocalCasino), "_wheel");
			private static readonly FieldInfo SlotSizeField = AccessTools.Field(typeof(LocalCasino), "_slotSize");
			private static readonly FieldInfo SpinningField = AccessTools.Field(typeof(LocalCasino), "_isSpinning");
			private static readonly FieldInfo BetColorField = AccessTools.Field(typeof(CasinoManager), "_curBetColor");

			private const float StepDeg = 4f;    // 每物理 tick 最大引导角
			private const float SettleDeg = 2.5f; // 进入槽位中心该范围内停止引导，交给物理停稳

			private static void Prefix(LocalCasino __instance)
			{
				if (!CheatCore.RouletteWin || !CasinoManager.IsBetting)
				{
					return;
				}
				Rigidbody ball = BallField?.GetValue(__instance) as Rigidbody;
				Transform wheel = WheelField?.GetValue(__instance) as Transform;
				bool spinning = SpinningField != null && (bool)SpinningField.GetValue(__instance);
				if (!ball || !wheel || !spinning || SlotSizeField == null || BetColorField == null)
				{
					return;
				}
				float slotSize = (float)SlotSizeField.GetValue(__instance);
				var bet = (BetColor)BetColorField.GetValue(null);

				Vector3 wheelPos = wheel.position;
				Vector3 ballPos = ball.transform.position;
				Vector3 toWheel = wheelPos - ballPos;
				toWheel.y = 0f;
				if (toWheel.sqrMagnitude < 0.0001f)
				{
					return;
				}
				// 复刻游戏角度公式：ballYaw 为 LookAt(wheel) 后的朝向角，rel = (ballYaw - wheelYaw) mod 360
				float ballYaw = Mathf.Atan2(toWheel.x, toWheel.z) * Mathf.Rad2Deg;
				float wheelYaw = wheel.eulerAngles.y;
				float rel = Mathf.Repeat(ballYaw - wheelYaw, 360f);

				float targetRel;
				switch (bet)
				{
					case BetColor.Green:
						targetRel = 0.5f * slotSize;
						break;
					case BetColor.Black:
						targetRel = NearestSlotCenter(rel, slotSize, 1);
						break;
					default:
						targetRel = NearestSlotCenter(rel, slotSize, 2);
						break;
				}

				float err = Mathf.Repeat(targetRel - rel + 540f, 360f) - 180f;
				if (Mathf.Abs(err) < SettleDeg)
				{
					// 已在目标槽内：水平阻尼帮助球停稳（游戏需同一槽停留 2 秒才结算）
					Vector3 vel = ball.linearVelocity;
					vel.x *= 0.85f;
					vel.z *= 0.85f;
					ball.linearVelocity = vel;
					return;
				}
				// 绕轮盘中心 Y 轴把球位旋转 step 度（绕 +Y 旋转 δ 度恰好使 yaw 增 δ，从而增大 rel）
				float step = Mathf.Clamp(err, -StepDeg, StepDeg);
				Vector3 offset = ballPos - wheelPos;
				Vector3 rotated = wheelPos + Quaternion.Euler(0f, step, 0f) * offset;
				rotated.y = ballPos.y;
				ball.MovePosition(rotated);
			}

			/// <summary>距 rel 最近且余数为 parity（1=黑奇数槽，2=红偶数槽，绿=槽0）的槽中心角。</summary>
			private static float NearestSlotCenter(float rel, float slotSize, int parity)
			{
				int idx = Mathf.FloorToInt(rel / slotSize);
				int candidate = idx;
				if (candidate % 2 != parity % 2 || candidate == 0)
				{
					int up = idx + 1;
					int down = idx - 1;
					float distUp = SlotDist(rel, up, slotSize);
					float distDown = SlotDist(rel, down, slotSize);
					candidate = distUp <= distDown ? up : down;
				}
				return (candidate + 0.5f) * slotSize;
			}

			private static float SlotDist(float rel, int idx, float slotSize)
			{
				float center = (idx + 0.5f) * slotSize;
				float d = Mathf.Repeat(center - rel + 540f, 360f) - 180f;
				return Mathf.Abs(d);
			}
		}

		[HarmonyPatch(typeof(Weapon), "AddModelRecoil")]
		private static class Weapon_NoRecoilModel
		{
			private static bool Prefix()
			{
				return !CheatCore.NoRecoil;
			}
		}

		[HarmonyPatch(typeof(PlayerCamera), "Recoil")]
		private static class Camera_NoRecoil
		{
			private static bool Prefix()
			{
				return !CheatCore.NoRecoil;
			}
		}

		[HarmonyPatch(typeof(PlayerToolMovement), "Recoil")]
		private static class ToolMovement_NoRecoil
		{
			private static bool Prefix()
			{
				return !CheatCore.NoRecoil;
			}
		}

		[HarmonyPatch(typeof(Weapon), "Shoot")]
		private static class Weapon_NoRecoilKnockback
		{
			// 字段注入名 = "___" 三前缀 + 字段全名（字段自带下划线，共四个）；
			// 写错会导致整个 PatchAll 中断、后续补丁全部丢失
			private static void Prefix(ref int ____recoilKnockback, out int __state)
			{
				__state = ____recoilKnockback;
				if (CheatCore.NoRecoil)
				{
					____recoilKnockback = 0;
				}
			}

			private static void Postfix(ref int ____recoilKnockback, int __state)
			{
				____recoilKnockback = __state;
			}
		}

		[HarmonyPatch(typeof(Weapon), "Shoot")]
		private static class Weapon_BlockShootInMenu
		{
			// 菜单打开期间彻底禁止开枪：动作禁用挡不住 Weapon.Update 里
			// _holdingFireInput/_queuedShoot 卡住状态的派发（该路径不受暂停与 BlockInputs 门控），
			// 穿透点击会持续走火并弹出命中数字
			private static bool Prefix()
			{
				return !MenuUI.MenuOpen;
			}
		}

		[HarmonyPatch(typeof(Weapon), "HasCooldown")]
		private static class Weapon_RapidFire
		{
			// 冷却是动画驱动的（HasCooldown 查 _anim.IsPlaying("Fire")），旁路即"极速射击"：
			// 半自动点击即发，全自动按住每帧连发
			private static bool Prefix(ref bool __result)
			{
				if (!CheatCore.RapidFire)
				{
					return true;
				}
				__result = false;
				return false;
			}
		}

		// ================= 2.0 补丁 =================

		[HarmonyPatch(typeof(CreatureUtils), "GetWeightWithStandardDeviation")]
		private static class Weight_MaxWeight
		{
			// 游戏用高斯分布 roll 重量并 Clamp(0.25, 1.75)，1.75 即最大体重（破纪录 + 最高卖价）
			private static bool Prefix(ref float __result)
			{
				if (!CheatCore.MaxWeight || !CheatCore.IsServer)
				{
					return true;
				}
				__result = 1.75f;
				return false;
			}
		}

		[HarmonyPatch(typeof(PlayerVitals), "LowerFullnessTick")]
		private static class Vitals_NoHungerTick
		{
			// LowerFullness 本体较小可能被 Mono JIT 内联进调用点，故同时打在调用方上双保险
			private static bool Prefix()
			{
				return !(CheatCore.HungerFreeze && CheatCore.IsServer);
			}
		}

		[HarmonyPatch(typeof(PlayerVitals), "LowerFullness")]
		private static class Vitals_NoHunger
		{
			private static bool Prefix()
			{
				return !(CheatCore.HungerFreeze && CheatCore.IsServer);
			}
		}

		[HarmonyPatch(typeof(MoneyManager), "CanAfford")]
		private static class Money_FreeShopping
		{
			private static bool Prefix(ref bool __result)
			{
				if (!CheatCore.FreeShopping || !CheatCore.IsServer)
				{
					return true;
				}
				__result = true;
				return false;
			}
		}

		[HarmonyPatch(typeof(MoneyManager), "RemoveMoney")]
		private static class Money_FreeShoppingDeduct
		{
			// 免费购物：CanAfford 放行后这里不再扣钱。ctrl+N 自助扣钱走同方法，开着免费购物时一并跳过，可接受
			private static bool Prefix()
			{
				return !(CheatCore.FreeShopping && CheatCore.IsServer);
			}
		}

		[HarmonyPatch(typeof(CasinoManager), "ServerRouletteResult")]
		private static class Casino_RouletteWin
		{
			// 开奖色强制改为主机下注色。字段注入名 = "___" + 字段原名，_curBetColor 自带下划线所以是四个下划线
			private static void Prefix(ref BetColor winColor, BetColor ____curBetColor)
			{
				if (CheatCore.RouletteWin && CasinoManager.IsBetting)
				{
					winColor = ____curBetColor;
				}
			}
		}

		[HarmonyPatch(typeof(MoneyManager), "SellItem")]
		private static class Money_SellMultiplier
		{
			// 打在 SellItem 而不是 get_TotalWorth：getter 多处内联且会波及赌场押注计价；
			// 这里只把差额补进对应钱包，卖出所得 = 原价 × 倍率
			private static void Postfix(Item item)
			{
				float m = CheatCore.SellMulti;
				if (m <= 1f || !CheatCore.IsServer)
				{
					return;
				}
				MoneyManager inst = MoneyManager.Instance;
				if (!inst)
				{
					return;
				}
				int bonus = (int)(item.TotalWorth * (m - 1f));
				if (bonus <= 0)
				{
					return;
				}
				if (item.Currency == Currency.Euro)
				{
					inst._euro.Value += bonus;
				}
				else
				{
					inst._money.Value += bonus;
				}
			}
		}
	}
}
