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
	}
}
