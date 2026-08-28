using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

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
		public const string PluginName = "How to Fish Cheat Menu";
		public const string PluginVersion = "1.2.0";

		internal static ManualLogSource Log;

		private ConfigEntry<bool> _enableCheatsAtStart;
		private ConfigEntry<int> _moneyAmount;
		private ConfigEntry<string> _speedMults;
		private ConfigEntry<string> _jumpMults;

		private ConfigEntry<KeyCode> _menuKey;
		private ConfigEntry<KeyCode> _godKey;
		private ConfigEntry<KeyCode> _instantBiteKey;
		private ConfigEntry<KeyCode> _speedCycleKey;
		private ConfigEntry<KeyCode> _moneyKey;
		private ConfigEntry<KeyCode> _espKey;
		private ConfigEntry<KeyCode> _perfKey;
		private ConfigEntry<KeyCode> _infiniteAmmoKey;

		internal static ConfigEntry<bool> EspShowNames;
		internal static ConfigEntry<bool> AmmoHudEnabled;
		internal static float[] SpeedOptions;
		internal static float[] JumpOptions;
		internal static int SpeedIndex;
		internal static int JumpIndex;

		private Harmony _harmony;

		private void Awake()
		{
			Log = Logger;

			_enableCheatsAtStart = Config.Bind("General", "EnableCheatsAtStart", true,
				"游戏启动后自动开启游戏内置作弊通道 (ClientSettings.CheatsEnabled)");
			_moneyAmount = Config.Bind("General", "MoneyAmount", 10000,
				"加钱按钮每次增加的金额");
			_speedMults = Config.Bind("General", "SpeedMultipliers", "1,1.5,2,3,5",
				"移速倍率可选项，逗号分隔，按 F4 循环切换");
			_jumpMults = Config.Bind("General", "JumpMultipliers", "1,2,3,5",
				"跳跃倍率可选项，逗号分隔，按 F6 循环切换");

			_menuKey = Config.Bind("Hotkeys", "MenuKey", KeyCode.F1, "开关作弊菜单");
			_godKey = Config.Bind("Hotkeys", "GodModeKey", KeyCode.F2, "上帝模式");
			_instantBiteKey = Config.Bind("Hotkeys", "InstantBiteKey", KeyCode.F3, "秒咬钩开关");
			_speedCycleKey = Config.Bind("Hotkeys", "SpeedCycleKey", KeyCode.F4, "循环切换移速倍率");
			_moneyKey = Config.Bind("Hotkeys", "AddMoneyKey", KeyCode.F5, "加钱");
			_espKey = Config.Bind("Hotkeys", "EspKey", KeyCode.F6, "鱼群雷达开关");
			_perfKey = Config.Bind("Hotkeys", "PerfOverlayKey", KeyCode.F7, "性能监视开关");
			_infiniteAmmoKey = Config.Bind("Hotkeys", "InfiniteAmmoKey", KeyCode.F8, "无限弹匣开关");

			EspShowNames = Config.Bind("Esp", "ShowNames", true, "雷达是否显示生物名字");
			AmmoHudEnabled = Config.Bind("Hud", "AmmoHud", true, "手持远程武器时在右下角显示 当前弹/弹匣容量");

			SpeedOptions = ParseFloatList(_speedMults.Value, 1f);
			JumpOptions = ParseFloatList(_jumpMults.Value, 1f);

			if (_enableCheatsAtStart.Value)
			{
				CheatCore.EnableCheats();
			}

			MenuUI.Init(this, _moneyAmount);
			Overlays.Init(this);

			_harmony = new Harmony(PluginGuid);
			try
			{
				Patches.Init();
				// 必须传 Assembly：补丁类是嵌套类，PatchAll(Type) 只处理容器类型本身（无注解）会静默跳过
				_harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
				int patched = 0;
				foreach (var method in _harmony.GetPatchedMethods())
				{
					patched++;
					Log.LogInfo($"Harmony patched: {method.DeclaringType?.Name}.{method.Name}");
				}
				Log.LogInfo($"Harmony total patched methods: {patched}");
			}
			catch (System.Exception e)
			{
				Log.LogError($"Harmony patch failed: {e}");
			}

			Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
		}

		private void OnDestroy()
		{
			if (_harmony != null)
			{
				_harmony.UnpatchSelf();
			}
		}

		private void Update()
		{
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
				CheatCore.AddMoney(_moneyAmount.Value);
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

			Overlays.TickPerfAccumulator();
		}

		private void OnGUI()
		{
			// 关闭所有覆盖层时完全不做任何工作
			if (!MenuUI.MenuOpen && !Overlays.EspEnabled && !Overlays.PerfEnabled)
			{
				return;
			}
			MenuUI.OnGUI();
			Overlays.OnGUI();
		}

		internal static void CycleSpeed()
		{
			if (SpeedOptions == null || SpeedOptions.Length == 0)
			{
				return;
			}
			SpeedIndex = (SpeedIndex + 1) % SpeedOptions.Length;
			CheatCore.SpeedMulti = SpeedOptions[SpeedIndex];
			MenuUI.NotifySpeedChanged();
		}

		internal static void CycleJump()
		{
			if (JumpOptions == null || JumpOptions.Length == 0)
			{
				return;
			}
			JumpIndex = (JumpIndex + 1) % JumpOptions.Length;
			CheatCore.JumpMulti = JumpOptions[JumpIndex];
			MenuUI.NotifyJumpChanged();
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
