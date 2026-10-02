using System.Reflection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using HarmonyLib;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>
	/// 游戏内置 API 的薄封装。所有调用方都做主机校验：
	/// 金钱 / 刷物品 / 上帝模式（饥饿 tick）/ 赌场等由服务器权威，客户端身份调用会被游戏自身拒绝。
	/// </summary>
	public static class CheatCore
	{
		public static bool InstantBite;
		public static bool InfiniteAmmo;
		public static bool AutoReel;
		public static bool MaxWeight;
		public static bool FreeShopping;
		public static bool HungerFreeze;
		public static bool RouletteWin;
		public static bool SlotsLegendary;
		public static bool NoRecoil;
		public static bool RapidFire;
		public static float SpeedMulti = 1f;
		public static float JumpMulti = 1f;
		public static float SellMulti = 1f;
		public static Currency MoneyCurrency = Currency.Dollar;

		public static bool IsServer
		{
			get
			{
				Server s = Server.Instance;
				return s && s.IsServerInitialized;
			}
		}

		public static void Say(string message)
		{
			ChatManager.ChatMessage(message);
		}

		public static void EnableCheats()
		{
			ClientSettings.ToggleCheats(true);
		}

		public static bool CheatsEnabled => ClientSettings.CheatsEnabled;

		public static bool GodOn => PlayerManager.InGodMode;

		public static void ToggleGod()
		{
			PlayerManager.ToggleGodMode();
			Say(GodOn ? Msg.GodOn : Msg.GodOff);
		}

		public static bool OneShotOn
		{
			get
			{
				ServerSettings inst = ServerSettings.Instance;
				return inst && ServerSettings.OneShotEnabled;
			}
		}

		public static void ToggleOneShot()
		{
			ServerSettings inst = ServerSettings.Instance;
			if (!inst)
			{
				return;
			}
			inst.ToggleOneShot();
			Say(OneShotOn ? Msg.OneShotOn : Msg.OneShotOff);
		}

		// ---- 金钱（双货币） ----

		public static void AddMoney(int amount)
		{
			MoneyManager money = MoneyManager.Instance;
			if (!money || !money.IsServerInitialized)
			{
				Say(Msg.NeedHost);
				return;
			}
			Player p = Player.LocalPlayer;
			if (!p)
			{
				return;
			}
			MoneyManager.AddMoney(amount, p, MoneyCurrency);
		}

		/// <summary>直接写 SyncVar 设定精确余额（仅主机）。</summary>
		public static void SetMoney(int amount)
		{
			MoneyManager money = MoneyManager.Instance;
			if (!money || !money.IsServerInitialized)
			{
				Say(Msg.NeedHost);
				return;
			}
			amount = Mathf.Max(0, amount);
			if (MoneyCurrency == Currency.Euro)
			{
				money._euro.Value = amount;
			}
			else
			{
				money._money.Value = amount;
			}
			Say(Msg.Done);
		}

		// ---- 难度与规则 ----

		public static void SetDifficulty(int index)
		{
			ServerSettings inst = ServerSettings.Instance;
			if (!inst || !inst.IsServerInitialized)
			{
				Say(Msg.NeedHost);
				return;
			}
			inst.SetDifficulty((Difficulty)index);
		}

		public static int GetDifficulty()
		{
			ServerSettings inst = ServerSettings.Instance;
			return inst ? (int)ServerSettings.Difficulty : 1;
		}

		public static void ToggleFriendlyFire(bool to)
		{
			ServerSettings inst = ServerSettings.Instance;
			if (!inst || !inst.IsServerInitialized)
			{
				Say(Msg.NeedHost);
				return;
			}
			inst.ToggleFriendlyFire(to);
		}

		/// <summary>一键回满：血量/饱食度拉满，清空毒与火（仅主机）。</summary>
		public static void FullRestore()
		{
			Player p = Player.LocalPlayer;
			if (!p)
			{
				return;
			}
			PlayerVitals v = p.Vitals;
			if (!v || !v.IsServerInitialized)
			{
				Say(Msg.NeedHost);
				return;
			}
			v.ServerResetVitals();
			if (SvFullness != null)
			{
				((SyncVar<int>)SvFullness.GetValue(v)).Value = 100;
			}
			if (SvPoison != null)
			{
				((SyncVar<int>)SvPoison.GetValue(v)).Value = 0;
			}
			if (SvFire != null)
			{
				((SyncVar<int>)SvFire.GetValue(v)).Value = 0;
			}
			Say(Msg.Done);
		}

		// ---- 传送 ----

		public static void TpIsland(bool prev)
		{
			OnlineIslandManager.TpToNextIsland(prev);
		}

		/// <summary>传送到指定主岛（场景序号）。</summary>
		public static void TpIsland(byte island)
		{
			OnlineIslandManager.TpToSpecificIsland(island);
		}

		/// <summary>随机小岛：走游戏私有方法（O+Shift 同路径）。</summary>
		public static void TpRandomMini()
		{
			OnlineIslandManager m = OnlineIslandManager.Instance;
			if (!m || !IsServer)
			{
				Say(Msg.NeedHost);
				return;
			}
			if (_tpRandomMini == null)
			{
				_tpRandomMini = AccessTools.Method(typeof(OnlineIslandManager), "TpToRandomMiniIsland");
			}
			_tpRandomMini?.Invoke(m, null);
		}

		private static MethodInfo _tpRandomMini;

		// ---- 赌场 ----

		public static void ApplySlotsCheat()
		{
			if (!SlotsLegendary)
			{
				SlotMachineManager.SetCheatSkin(null, byte.MaxValue);
				return;
			}
			Boat boat = BoatManager.Boat;
			SkinPreset preset = boat ? boat.SkinPreset : null;
			if (!preset)
			{
				Say(Msg.NoBoat);
				SlotsLegendary = false;
				return;
			}
			byte idx = byte.MaxValue;
			byte rare = byte.MaxValue;
			for (byte i = 0; i < preset.Skins.Count; i++)
			{
				Rarity r = preset.Skins[i].Rarity;
				if (r == Rarity.Legendary)
				{
					idx = i;
					break;
				}
				if (r == Rarity.Rare && rare == byte.MaxValue)
				{
					rare = i;
				}
			}
			if (idx == byte.MaxValue)
			{
				idx = rare;
			}
			if (idx == byte.MaxValue)
			{
				Say(Msg.NoLegendary);
				SlotsLegendary = false;
				return;
			}
			SlotMachineManager.SetCheatSkin(null, idx);
		}

		// ---- 生成器 ----

		public static void SpawnPrefab(Item prefab, bool drip, bool dead)
		{
			ItemManager items = ItemManager.Instance;
			if (!items || !IsServer)
			{
				Say(Msg.NeedHost);
				return;
			}
			Camera cam = GameInfo.CurCamera;
			if (!cam)
			{
				return;
			}
			Vector3 pos = cam.transform.position + cam.transform.forward * 2f;

			// prefab 上 Item.Creature 恒为 null（Creature.cs:549 运行时才 _creature = this），
			// 必须用 GetComponent 判定，否则生物全走纯物品路径、闪光/死亡标志失效
			if (!prefab.GetComponent<Creature>())
			{
				items.SpawnNewItem(prefab, pos, Quaternion.identity);
				return;
			}
			Item inst = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
			Creature c = inst.GetComponent<Creature>();
			if (!c)
			{
				Server.Instance.Spawn(inst.gameObject);
				return;
			}
			// 与游戏 /spawn /spawndrip 命令同路径：Instantiate → 标记 → 网络生成
			if (drip)
			{
				c.SetDrip();
			}
			if (dead)
			{
				c.ServerKillOnSpawn();
			}
			Server.Instance.Spawn(inst.gameObject);
			// 生成后补标记：此时网络对象已初始化，SetDrip 的守卫必过，视觉一定应用
			if (drip)
			{
				c.SetDrip();
			}
			if (dead && !c.IsDead)
			{
				Transform t = c.transform;
				c.LocalHit(t, t.position, Vector3.up, Player.LocalPlayer, 999999, false, Vector3.zero);
			}
		}

		public static void SpawnItem(Item prefab)
		{
			SpawnPrefab(prefab, false, false);
		}

		// ---- 世界 ----

		public static void KillBoss()
		{
			Creature boss = BossManager.Boss;
			Player p = Player.LocalPlayer;
			if (!boss || !p)
			{
				Say(Msg.NoBoss);
				return;
			}
			Transform t = boss.transform;
			boss.LocalHit(t, t.position, Vector3.up, p, 999999, false, Vector3.zero);
		}

		public static void SetAllCreaturesKilled(bool killed, bool drip)
		{
			GameInfo.ToggleAllCreaturesKilled(killed, drip);
		}

		/// <summary>
		/// 真实击杀当前所有活生物（与 /killboss 同路径：LocalHit → ServerRpc → ServerChangeHp → 全网 OnDeath）。
		/// 生物尸体留在世界里，场景刷怪点约 10 秒后自动补齐。
		/// </summary>
		public static bool KillAllAliveCreatures()
		{
			CreatureManager cm = CreatureManager.Instance;
			Player p = Player.LocalPlayer;
			if (!cm || !p)
			{
				return false;
			}
			System.Collections.Generic.List<Creature> list = AliveCreatures(cm);
			if (list == null)
			{
				return false;
			}
			int killed = 0;
			// 倒序遍历：LocalHit 触发死亡后列表可能被并发剔除
			for (int i = list.Count - 1; i >= 0; i--)
			{
				Creature c = list[i];
				if (!c)
				{
					continue;
				}
				Transform t = c.transform;
				c.LocalHit(t, t.position, Vector3.up, p, 999999, false, Vector3.zero);
				killed++;
			}
			return killed > 0;
		}

		private static readonly FieldInfo AliveField =
			AccessTools.Field(typeof(CreatureManager), "_aliveCreatures");

		private static System.Collections.Generic.List<Creature> AliveCreatures(CreatureManager cm)
		{
			if (AliveField == null)
			{
				return null;
			}
			return AliveField.GetValue(cm) as System.Collections.Generic.List<Creature>;
		}

		// ---- 杂项 ----

		public static void UnlockAllSkins()
		{
			if (!IsServer)
			{
				Say(Msg.NeedHost);
				return;
			}
			SaveManager.LockAllSkins();
			Item[] withSkins = GameInfo.ItemWithSkinsforCommands;
			if (withSkins != null)
			{
				for (int i = 0; i < withSkins.Length; i++)
				{
					Item item = withSkins[i];
					if (!item || !item.SkinPreset)
					{
						continue;
					}
					int count = item.SkinPreset.Skins.Count;
					for (int j = 0; j < count; j++)
					{
						SaveManager.UnlockSkin(item.ID, (byte)j);
					}
				}
			}
			Boat boat = BoatManager.Boat;
			if (boat && boat.SkinPreset)
			{
				int boatSkins = boat.SkinPreset.Skins.Count;
				for (int j = 0; j < boatSkins; j++)
				{
					SaveManager.UnlockSkin(byte.MaxValue, (byte)j);
				}
			}
			Say(Msg.SkinsUnlocked);
		}

		public static void UnlockAchievements()
		{
			AchievementManager.ToggleAllAchievements(true);
			Say(Msg.Done);
		}

		public static void LockAchievements()
		{
			AchievementManager.ToggleAllAchievements(false);
			Say(Msg.Done);
		}

		public static void FinishGame()
		{
			EndGameManager end = EndGameManager.Instance;
			if (!end)
			{
				return;
			}
			end.FinishGameInput();
		}

		/// <summary>图鉴全收集：普通与闪光(drip)击杀记录都拉满（清空图鉴的反向操作）。</summary>
		public static void CompleteJournal()
		{
			GameInfo.ToggleAllCreaturesKilled(true, false);
			GameInfo.ToggleAllCreaturesKilled(true, true);
			Say(Msg.Done);
		}

		// ---- 自动收线 ----

		private static System.Action<FishingRodCast, UnityEngine.InputSystem.InputAction.CallbackContext> _reelIn;
		private static System.Action<FishingRodCast, UnityEngine.InputSystem.InputAction.CallbackContext> _reelCancel;
		private static readonly FieldInfo RodBaitField = AccessTools.Field(typeof(FishingRod), "_bait");
		private static readonly FieldInfo ReelingField = AccessTools.Field(typeof(FishingRod), "_isReelingIn");

		private static Bait GetBait(FishingRod rod)
		{
			return RodBaitField?.GetValue(rod) as Bait;
		}

		/// <summary>
		/// 自动收线（仅钩上有鱼时）：自校正状态机。真实"按住"的连续收线发生在
		/// FishingRod.FixedUpdate（_holdReelTimer 累计），因此这里只在 _isReelingIn
		/// 与目标状态不符时模拟一次按下/松开事件，绝不每帧重按（否则线长会以帧率速度收缩导致秒断线）。
		/// </summary>
		public static void FishingTick()
		{
			if (!AutoReel && _reelIn == null)
			{
				return;
			}
			Player p = Player.LocalPlayer;
			Item held = p ? p.Holding.HeldItem : null;
			FishingRodCast rod = held ? held.GetComponent<FishingRodCast>() : null;

			bool want = false;
			if (AutoReel && rod)
			{
				Bait bait = GetBait(rod);
				want = bait && bait.ItemOnBait;
			}

			bool isReeling = rod && ReelingField != null && (bool)ReelingField.GetValue(rod);
			if (want == isReeling)
			{
				return;
			}
			if (_reelIn == null)
			{
				_reelIn = AccessTools.MethodDelegate<System.Action<FishingRodCast, UnityEngine.InputSystem.InputAction.CallbackContext>>(
					AccessTools.Method(typeof(FishingRodCast), "ReelInInput"));
				_reelCancel = AccessTools.MethodDelegate<System.Action<FishingRodCast, UnityEngine.InputSystem.InputAction.CallbackContext>>(
					AccessTools.Method(typeof(FishingRodCast), "ReelInCancelled"));
			}
			if (want)
			{
				_reelIn(rod, default(UnityEngine.InputSystem.InputAction.CallbackContext));
			}
			else if (rod)
			{
				_reelCancel(rod, default(UnityEngine.InputSystem.InputAction.CallbackContext));
			}
		}

		// ---- 反射缓存（PlayerVitals 四项数值） ----

		private static readonly FieldInfo SvFullness = AccessTools.Field(typeof(PlayerVitals), "_syncedFullness");
		private static readonly FieldInfo SvPoison = AccessTools.Field(typeof(PlayerVitals), "_syncedPoison");
		private static readonly FieldInfo SvFire = AccessTools.Field(typeof(PlayerVitals), "_syncedFire");
	}

	/// <summary>所有提示都用常量字符串，运行期零分配。</summary>
	public static class Msg
	{
		public const string NeedHost = "[Mod] 该功能只有主机可用";
		public const string NoBoss = "[Mod] 当前没有 Boss";
		public const string Done = "[Mod] 完成";
		public const string GodOn = "[Mod] 上帝模式: 开";
		public const string GodOff = "[Mod] 上帝模式: 关";
		public const string OneShotOn = "[Mod] 一击必杀: 开";
		public const string OneShotOff = "[Mod] 一击必杀: 关";
		public const string BiteOn = "[Mod] 秒咬钩: 开";
		public const string BiteOff = "[Mod] 秒咬钩: 关";
		public const string AmmoOn = "[Mod] 无限弹匣: 开";
		public const string AmmoOff = "[Mod] 无限弹匣: 关";
		public const string SkinsUnlocked = "[Mod] 皮肤已全部解锁";
		public const string NoBoat = "[Mod] 船还没就绪，无法设置老虎机";
		public const string NoLegendary = "[Mod] 船没有传说皮肤，已跳过";
	}
}
