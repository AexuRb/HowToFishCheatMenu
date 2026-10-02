using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HTF.CheatMenu
{
	// 全程遵守零 GC 分配规范：
	// - Update 只做键值比较与状态调用，不分配
	// - 所有 GUIContent / GUIStyle / 委托 / 反射信息在初始化时缓存为 static readonly
	// - 运行期字符串只在状态变化或 1Hz 时生成
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "com.htf.cheatmenu";
		public const string PluginName = "HowToFishCheatMenu";
		public const string PluginVersion = "2.1.0";

		internal static ManualLogSource Log;

		private ConfigEntry<bool> _enableCheatsAtStart;
		private ConfigEntry<int> _moneyAmount;
		private ConfigEntry<string> _speedMults;
		private ConfigEntry<string> _jumpMults;
		private ConfigEntry<string> _sellMults;
		private ConfigEntry<float> _uiScale;

		private ConfigEntry<KeyCode> _menuKey;
		private ConfigEntry<KeyCode> _godKey;
		private ConfigEntry<KeyCode> _instantBiteKey;
		private ConfigEntry<KeyCode> _speedCycleKey;
		private ConfigEntry<KeyCode> _moneyKey;
		private ConfigEntry<KeyCode> _espKey;
		private ConfigEntry<KeyCode> _perfKey;
		private ConfigEntry<KeyCode> _infiniteAmmoKey;

		internal static ConfigEntry<float> UiScaleEntry;
		internal static ConfigEntry<bool> EspShowNames;
		internal static ConfigEntry<bool> EspShowWorth;
		internal static ConfigEntry<bool> ItemEspEnabled;
		internal static ConfigEntry<bool> AmmoHudEnabled;
		internal static ConfigEntry<int> MoneyAmount;
		internal static float[] SpeedOptions;
		internal static float[] JumpOptions;
		internal static float[] SellOptions;
		internal static int SpeedIndex;
		internal static int JumpIndex;
		internal static int SellIndex;
		internal static string FooterText = string.Empty;

		private Harmony _harmony;

		private void Awake()
		{
			Log = Logger;

			_enableCheatsAtStart = Config.Bind("General", "EnableCheatsAtStart", true,
				"游戏启动后自动开启游戏内置作弊通道 (ClientSettings.CheatsEnabled)");
			_moneyAmount = Config.Bind("General", "MoneyAmount", 10000,
				"菜单里加钱按钮默认金额（可在菜单输入框临时修改）");
			_speedMults = Config.Bind("General", "SpeedMultipliers", "1,1.5,2,3,5",
				"移速倍率可选项，逗号分隔，按 F4 循环切换");
			_jumpMults = Config.Bind("General", "JumpMultipliers", "1,2,3,5",
				"跳跃倍率可选项，逗号分隔，菜单按钮循环切换");
			_sellMults = Config.Bind("General", "SellMultipliers", "1,2,3,5,10",
				"卖鱼价值倍率可选项（仅主机生效），菜单按钮循环切换");
			_uiScale = Config.Bind("General", "UiScale", 1f,
				new ConfigDescription("菜单缩放", new AcceptableValueRange<float>(0.75f, 1.6f)));

			_menuKey = Config.Bind("Hotkeys", "MenuKey", KeyCode.F1, "开关作弊菜单");
			_godKey = Config.Bind("Hotkeys", "GodModeKey", KeyCode.F2, "上帝模式");
			_instantBiteKey = Config.Bind("Hotkeys", "InstantBiteKey", KeyCode.F3, "秒咬钩开关");
			_speedCycleKey = Config.Bind("Hotkeys", "SpeedCycleKey", KeyCode.F4, "循环切换移速倍率");
			_moneyKey = Config.Bind("Hotkeys", "AddMoneyKey", KeyCode.F5, "按菜单当前货币加钱");
			_espKey = Config.Bind("Hotkeys", "EspKey", KeyCode.F6, "鱼群雷达开关");
			_perfKey = Config.Bind("Hotkeys", "PerfOverlayKey", KeyCode.F7, "性能监视开关");
			_infiniteAmmoKey = Config.Bind("Hotkeys", "InfiniteAmmoKey", KeyCode.F8, "无限弹匣开关");

			EspShowNames = Config.Bind("Esp", "ShowNames", true, "雷达是否显示生物名字");
			EspShowWorth = Config.Bind("Esp", "ShowWorth", false, "雷达是否显示生物价值（$ / €）");
			ItemEspEnabled = Config.Bind("Esp", "ItemRadar", false, "地面物品雷达（青色点，不含生物）");
			AmmoHudEnabled = Config.Bind("Hud", "AmmoHud", true, "手持远程武器时在右下角显示 当前弹/弹匣容量");
			MoneyAmount = _moneyAmount;
			UiScaleEntry = _uiScale;

			Ui.Scale = Mathf.Clamp(_uiScale.Value, 0.75f, 1.6f);
			Ui.InitFont();

			SpeedOptions = ParseFloatList(_speedMults.Value, 1f);
			JumpOptions = ParseFloatList(_jumpMults.Value, 1f);
			SellOptions = ParseFloatList(_sellMults.Value, 1f);

			if (_enableCheatsAtStart.Value)
			{
				CheatCore.EnableCheats();
			}

			BuildFooter();
			MenuUI.Init(this);
			Overlays.Init(this);

			_harmony = new Harmony(PluginGuid);
			try
			{
				Patches.Init();
				// 逐类补丁：单个补丁类失败（如字段注入名写错）只丢该功能并记日志，
				// 不像 PatchAll 那样一抛就中断、后面所有补丁全部丢失
				int patchedTypes = 0;
				foreach (var type in AccessTools.GetTypesFromAssembly(System.Reflection.Assembly.GetExecutingAssembly()))
				{
					try
					{
						if (new PatchClassProcessor(_harmony, type).Patch() != null)
						{
							patchedTypes++;
						}
					}
					catch (System.Exception e)
					{
						Log.LogError($"Harmony patch class {type.Name} failed: {e.Message}");
					}
				}
				int patched = 0;
				foreach (var method in _harmony.GetPatchedMethods())
				{
					patched++;
					Log.LogInfo($"Harmony patched: {method.DeclaringType?.Name}.{method.Name}");
				}
				Log.LogInfo($"Harmony total patched methods: {patched} ({patchedTypes} classes)");
			}
			catch (System.Exception e)
			{
				Log.LogError($"Harmony patch failed: {e}");
			}

			Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
		}

		private void OnDestroy()
		{
			RestoreGameUiInput();
			RestoreGameplayInput();
			if (_harmony != null)
			{
				_harmony.UnpatchSelf();
			}
		}

		// ---- 点击穿透修复 ----
		// IMGUI 与游戏 UGUI（EventSystem）是两套独立输入：点作弊菜单时点击会穿透到
		// 下层 UGUI 按钮（如 ESC 菜单的"设置"），表现为"点一下背景就变"。
		// 菜单打开期间禁用游戏的事件系统（暂停界面可能启用另一套实例，故周期性全量扫描）。
		private readonly System.Collections.Generic.List<EventSystem> _esDisabled =
			new System.Collections.Generic.List<EventSystem>(2);
		private float _esNextScan;

		private void SuppressGameUiInput()
		{
			bool shouldDisable = MenuUI.MenuOpen && !ChatManager.IsTyping;
			if (shouldDisable)
			{
				if (Time.unscaledTime >= _esNextScan)
				{
					_esNextScan = Time.unscaledTime + 0.5f;
					EventSystem[] all = FindObjectsByType<EventSystem>();
					for (int i = 0; i < all.Length; i++)
					{
						if (all[i] && all[i].enabled && !_esDisabled.Contains(all[i]))
						{
							all[i].enabled = false;
							_esDisabled.Add(all[i]);
						}
					}
				}
			}
			else if (_esDisabled.Count > 0)
			{
				for (int i = 0; i < _esDisabled.Count; i++)
				{
					if (_esDisabled[i])
					{
						_esDisabled[i].enabled = true;
					}
				}
				_esDisabled.Clear();
			}
		}

		private void RestoreGameUiInput()
		{
			for (int i = 0; i < _esDisabled.Count; i++)
			{
				if (_esDisabled[i])
				{
					_esDisabled[i].enabled = true;
				}
			}
			_esDisabled.Clear();
		}

		// ---- 玩法输入屏蔽 ----
		// 游戏动作（开枪/挥杆/移动/视角等）走 PlayerInput 动作资产，与 IMGUI 输入互不知晓，
		// 点菜单会同时触发射击等动作。菜单打开期间逐个禁用启用中的动作，
		// 仅保留 "Pause"（ESC 必须始终可用）；关菜单时原样恢复。
		private readonly System.Collections.Generic.List<InputAction> _disabledActions =
			new System.Collections.Generic.List<InputAction>(24);

		private void SuppressGameplayInput()
		{
			PlayerInput input = GameInfo.Input;
			if (!input || !input.actions)
			{
				return;
			}
			if (MenuUI.MenuOpen)
			{
				if (_disabledActions.Count == 0)
				{
					foreach (InputActionMap map in input.actions.actionMaps)
					{
						InputAction pause = map.FindAction("Pause", throwIfNotFound: false);
						foreach (InputAction act in map.actions)
						{
							if (!act.enabled || act == pause)
							{
								continue;
							}
							act.Disable();
							_disabledActions.Add(act);
						}
					}
				}
				else
				{
					// 游戏运行中可能自行重新启用某些动作，每帧压回禁用态
					for (int i = 0; i < _disabledActions.Count; i++)
					{
						if (_disabledActions[i].enabled)
						{
							_disabledActions[i].Disable();
						}
					}
				}
			}
			else if (_disabledActions.Count > 0)
			{
				for (int i = 0; i < _disabledActions.Count; i++)
				{
					_disabledActions[i].Enable();
				}
				_disabledActions.Clear();
			}
		}

		private void RestoreGameplayInput()
		{
			for (int i = 0; i < _disabledActions.Count; i++)
			{
				_disabledActions[i].Enable();
			}
			_disabledActions.Clear();
		}

		// ---- 武器卡住状态清理 ----
		// 点菜单前的点击会把 Weapon._holdingFireInput/_queuedShoot 置位，
		// 而动作被禁用后 canceled 事件不再到来，Weapon.Update 会持续走火
		private static bool _wasMenuOpen;
		private static readonly System.Reflection.FieldInfo FireInputField =
			AccessTools.Field(typeof(Weapon), "_holdingFireInput");
		private static readonly System.Reflection.FieldInfo QueuedShootField =
			AccessTools.Field(typeof(Weapon), "_queuedShoot");

		private void ClearStuckFireInput()
		{
			Player p = Player.LocalPlayer;
			Item held = p ? p.Holding.HeldItem : null;
			Weapon w = held ? held.Weapon : null;
			if (!w)
			{
				return;
			}
			FireInputField?.SetValue(w, false);
			QueuedShootField?.SetValue(w, false);
		}

		private void Update()
		{
			// 暂停界面下允许正常开关菜单（F1 不受 ESC 影响）
			SuppressGameUiInput();
			SuppressGameplayInput();

			// 关闭菜单瞬间清掉武器卡住的按住/连发队列状态，防止一关菜单就开始走火
			if (_wasMenuOpen && !MenuUI.MenuOpen)
			{
				ClearStuckFireInput();
			}
			_wasMenuOpen = MenuUI.MenuOpen;

			// 热路径：只做 KeyCode 比较，不产生任何分配
			if (_menuKey.Value != KeyCode.None && Input.GetKeyDown(_menuKey.Value))
			{
				MenuUI.ToggleMenu();
			}

			if (_godKey.Value != KeyCode.None && Input.GetKeyDown(_godKey.Value))
			{
				CheatCore.ToggleGod();
			}

			if (_instantBiteKey.Value != KeyCode.None && Input.GetKeyDown(_instantBiteKey.Value))
			{
				CheatCore.InstantBite = !CheatCore.InstantBite;
				CheatCore.Say(CheatCore.InstantBite ? Msg.BiteOn : Msg.BiteOff);
			}

			if (_speedCycleKey.Value != KeyCode.None && Input.GetKeyDown(_speedCycleKey.Value))
			{
				CycleSpeed();
			}

			if (_moneyKey.Value != KeyCode.None && Input.GetKeyDown(_moneyKey.Value))
			{
				CheatCore.AddMoney(MoneyAmount.Value);
			}

			if (_espKey.Value != KeyCode.None && Input.GetKeyDown(_espKey.Value))
			{
				Overlays.ToggleEsp();
			}

			if (_perfKey.Value != KeyCode.None && Input.GetKeyDown(_perfKey.Value))
			{
				Overlays.TogglePerf();
			}

			if (_infiniteAmmoKey.Value != KeyCode.None && Input.GetKeyDown(_infiniteAmmoKey.Value))
			{
				CheatCore.InfiniteAmmo = !CheatCore.InfiniteAmmo;
				CheatCore.Say(CheatCore.InfiniteAmmo ? Msg.AmmoOn : Msg.AmmoOff);
			}

			CheatCore.FishingTick();
			Overlays.TickPerfAccumulator();
		}

		private void OnGUI()
		{
			// 关闭所有覆盖层时完全不做任何工作
			if (!MenuUI.MenuOpen && !Overlays.EspEnabled && !Overlays.PerfEnabled
				&& !ItemEspEnabled.Value && !AmmoHudEnabled.Value)
			{
				return;
			}
			MenuUI.OnGUI();
			Overlays.OnGUI();
		}

		private void BuildFooter()
		{
			bool zh = Ui.UseZh;
			string key(KeyCode k) => k.ToString();
			FooterText = zh
				? key(_menuKey.Value) + " 菜单   " + key(_godKey.Value) + " 上帝   " + key(_instantBiteKey.Value) + " 秒咬钩   "
					+ key(_speedCycleKey.Value) + " 移速   " + key(_moneyKey.Value) + " 加钱   " + key(_espKey.Value) + " 雷达   "
					+ key(_perfKey.Value) + " 性能   " + key(_infiniteAmmoKey.Value) + " 无限弹"
				: key(_menuKey.Value) + " menu   " + key(_godKey.Value) + " god   " + key(_instantBiteKey.Value) + " bite   "
					+ key(_speedCycleKey.Value) + " speed   " + key(_moneyKey.Value) + " money   " + key(_espKey.Value) + " esp   "
					+ key(_perfKey.Value) + " perf   " + key(_infiniteAmmoKey.Value) + " ammo";
		}

		internal static void CycleSpeed()
		{
			if (SpeedOptions == null || SpeedOptions.Length == 0)
			{
				return;
			}
			SpeedIndex = (SpeedIndex + 1) % SpeedOptions.Length;
			CheatCore.SpeedMulti = SpeedOptions[SpeedIndex];
		}

		internal static void CycleJump()
		{
			if (JumpOptions == null || JumpOptions.Length == 0)
			{
				return;
			}
			JumpIndex = (JumpIndex + 1) % JumpOptions.Length;
			CheatCore.JumpMulti = JumpOptions[JumpIndex];
		}

		internal static void CycleSell()
		{
			if (SellOptions == null || SellOptions.Length == 0)
			{
				return;
			}
			SellIndex = (SellIndex + 1) % SellOptions.Length;
			CheatCore.SellMulti = SellOptions[SellIndex];
		}

		/// <summary>缩放滑条实时写回配置文件。</summary>
		internal static void SaveUiScale(float value)
		{
			if (UiScaleEntry != null)
			{
				UiScaleEntry.Value = value;
			}
		}

		private static float[] ParseFloatList(string raw, float fallback)
		{
			if (string.IsNullOrEmpty(raw))
			{
				return new[] { fallback };
			}
			string[] parts = raw.Split(',');
			float[] result = new float[parts.Length];
			int count = 0;
			for (int i = 0; i < parts.Length; i++)
			{
				string part = parts[i].Trim();
				float value;
				if (part.Length > 0 && float.TryParse(part, out value))
				{
					result[count++] = value;
				}
			}
			if (count == 0)
			{
				return new[] { fallback };
			}
			if (count == result.Length)
			{
				return result;
			}
			float[] trimmed = new float[count];
			for (int i = 0; i < count; i++)
			{
				trimmed[i] = result[i];
			}
			return trimmed;
		}
	}
}
