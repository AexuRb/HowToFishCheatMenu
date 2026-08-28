using FishNet.Object;
using HarmonyLib;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>
	/// 游戏内置 API 的薄封装。所有调用方都做主机校验：
	/// 金钱 / 刷物品 / 上帝模式（饥饿 tick）等由服务器权威，客户端身份调用会被游戏自身拒绝。
	/// </summary>
	public static class CheatCore
	{
		public static bool InstantBite;
		public static bool InfiniteAmmo;
		public static float SpeedMulti = 1f;
		public static float JumpMulti = 1f;

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
			MoneyManager.AddMoney(amount, p);
		}

		public static void TpIsland(bool prev)
		{
			OnlineIslandManager.TpToNextIsland(prev);
		}

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

		private static readonly System.Reflection.FieldInfo AliveField =
			AccessTools.Field(typeof(CreatureManager), "_aliveCreatures");

		private static System.Collections.Generic.List<Creature> AliveCreatures(CreatureManager cm)
		{
			if (AliveField == null)
			{
				return null;
			}
			return AliveField.GetValue(cm) as System.Collections.Generic.List<Creature>;
		}

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

		public static void FinishGame()
		{
			EndGameManager end = EndGameManager.Instance;
			if (!end)
			{
				return;
			}
			end.FinishGameInput();
		}

		public static void SpawnItem(Item prefab)
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
			items.SpawnNewItem(prefab, pos, Quaternion.identity);
		}
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
	}
}
