using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>
	/// UI 2.0：单窗口 + 六页签（玩家/世界/赌场/钓鱼/显示/杂项）。
	/// 规避 GC 的关键点：
	/// - 所有 GUIContent / GUIStyle 在初始化时创建并复用
	/// - 布局用 Ui 静态游标推进，不走 GUILayout
	/// - 运行期字符串只在状态变化时生成（倍率按钮、主机状态、搜索过滤）
	/// </summary>
	internal static class MenuUI
	{
		private const int WinId = 0x48544632;
		private const float WinW = 430f;
		private const float WinH = 690f;

		private static bool _menuOpen;
		private static int _tab;
		private static Rect _winRect = new Rect(30f, 30f, WinW, WinH);
		private static readonly GUI.WindowFunction WindowFunc = DrawWindow;
		private static readonly float[] _pageHeights = new float[6];
		private static readonly Vector2[] _scrolls = new Vector2[6];

		private static Plugin _plugin;

		// ---- 金钱输入 ----
		private static string _moneyInput = "10000";

		// ---- 生成器 ----
		private static bool _listBuilt;
		private static Item[] _allItems = new Item[0];
		private static GUIContent[] _allLabels = new GUIContent[0];
		private static bool[] _allIsCreature = new bool[0];
		private static Item[] _viewItems = new Item[0];
		private static GUIContent[] _viewLabels = new GUIContent[0];
		private static string _search = string.Empty;
		private static bool _spawnDrip;
		private static bool _spawnDead;

		// ---- 指定岛传送 ----
		private static byte[] _mainIslands;
		private static int _islandSel = -1;
		private static GUIContent _islandLabel = GUIContent.none;

		// ---- 主机状态（打开菜单时刷新一次） ----
		private static bool _hostStatusBuilt;
		private static GUIContent _hostStatus;

		internal static bool MenuOpen => _menuOpen;

		// ---- 标签缓存 ----
		private static GUIContent _title;
		private static GUIContent _close;
		private static GUIContent[] _tabs;
		private static GUIContent _cCheats;
		private static GUIContent _cGodOn, _cGodOff;
		private static GUIContent _cOneShotOn, _cOneShotOff;
		private static GUIContent _cAmmoOn, _cAmmoOff;
		private static GUIContent _cBiteOn, _cBiteOff;
		private static GUIContent _cHungerOn, _cHungerOff;
		private static GUIContent _cRouletteOn, _cRouletteOff;
		private static GUIContent _cSlotsOn, _cSlotsOff;
		private static GUIContent _cWeightOn, _cWeightOff;
		private static GUIContent _cAutoReelOn, _cAutoReelOff;
		private static GUIContent _cNoRecoilOn, _cNoRecoilOff;
		private static GUIContent _cRapidOn, _cRapidOff;
		private static GUIContent _cJournalComplete;
		private static GUIContent _cScaleLabel;
		private static GUIContent[] _cSpeed;
		private static GUIContent[] _cJump;
		private static GUIContent[] _cSell;
		private static GUIContent _cDollar, _cEuro;
		private static GUIContent _cSetMoney, _cAddMoney;
		private static GUIContent _cTpPrev, _cTpNext, _cTpMini, _cTpGo, _cIslandMinus, _cIslandPlus;
		private static GUIContent _cKillBoss, _cKillAlive, _cClearJournal;
		private static GUIContent _cSpawnHeader, _cDrip, _cDead;
		private static GUIContent _cEspOn, _cEspOff;
		private static GUIContent _cEspNamesOn, _cEspNamesOff;
		private static GUIContent _cEspWorthOn, _cEspWorthOff;
		private static GUIContent _cItemEspOn, _cItemEspOff;
		private static GUIContent _cPerfOn, _cPerfOff;
		private static GUIContent _cAmmoHudOn, _cAmmoHudOff;
		private static GUIContent _cDiffEasy, _cDiffDefault, _cDiffHard;
		private static GUIContent _cFFOn, _cFFOff;
		private static GUIContent _cFreeOn, _cFreeOff;
		private static GUIContent _cFullRestore;
		private static GUIContent _cSkins, _cAchUnlock, _cAchLock, _cFinish;
		private static GUIContent _cCasinoHint, _cReelHint, _cBiteHint, _cWeightHint, _cSearchHint;
		private static GUIContent _cMoneySection, _cTpSection, _cCreatureSection, _cRulesSection, _cProgressSection;

		internal static void Init(Plugin plugin)
		{
			_plugin = plugin;
			_moneyInput = Plugin.MoneyAmount.Value.ToString();

			_title = Ui.GC("鱼力全开 · 作弊菜单 v2.1", "How to Fish · Cheat Menu v2.1");
			_close = new GUIContent("X");
			_tabs = new[]
			{
				Ui.GC("玩家", "Player"),
				Ui.GC("世界", "World"),
				Ui.GC("赌场", "Casino"),
				Ui.GC("钓鱼", "Fishing"),
				Ui.GC("显示", "HUD"),
				Ui.GC("杂项", "Misc"),
			};

			string host = Ui.HostSuffix;
			_cCheats = Ui.GC("开启游戏内置作弊", "Enable built-in cheats");
			_cGodOn = H(Ui.GC("上帝模式：开", "God mode: ON"), host);
			_cGodOff = H(Ui.GC("上帝模式：关", "God mode: OFF"), host);
			_cOneShotOn = H(Ui.GC("一击必杀：开", "One shot: ON"), host);
			_cOneShotOff = H(Ui.GC("一击必杀：关", "One shot: OFF"), host);
			_cAmmoOn = Ui.GC("无限弹匣：开", "Infinite mag: ON");
			_cAmmoOff = Ui.GC("无限弹匣：关", "Infinite mag: OFF");
			_cBiteOn = Ui.GC("秒咬钩：开", "Instant bite: ON");
			_cBiteOff = Ui.GC("秒咬钩：关", "Instant bite: OFF");
			_cHungerOn = H(Ui.GC("永不饥饿：开", "No hunger: ON"), host);
			_cHungerOff = H(Ui.GC("永不饥饿：关", "No hunger: OFF"), host);
			_cRouletteOn = H(Ui.GC("轮盘必胜：开", "Roulette always win: ON"), host);
			_cRouletteOff = H(Ui.GC("轮盘必胜：关", "Roulette always win: OFF"), host);
			_cSlotsOn = H(Ui.GC("老虎机必出传说：开", "Slots legendary: ON"), host);
			_cSlotsOff = H(Ui.GC("老虎机必出传说：关", "Slots legendary: OFF"), host);
			_cWeightOn = H(Ui.GC("满重量渔获：开", "Max weight: ON"), host);
			_cWeightOff = H(Ui.GC("满重量渔获：关", "Max weight: OFF"), host);
			_cAutoReelOn = Ui.GC("自动收线：开（钩上有鱼才收）", "Auto reel: ON (fish hooked)");
			_cAutoReelOff = Ui.GC("自动收线：关", "Auto reel: OFF");
			_cNoRecoilOn = Ui.GC("无后座：开", "No recoil: ON");
			_cNoRecoilOff = Ui.GC("无后座：关", "No recoil: OFF");
			_cRapidOn = Ui.GC("极速射击：开", "Rapid fire: ON");
			_cRapidOff = Ui.GC("极速射击：关", "Rapid fire: OFF");
			_cJournalComplete = H(Ui.GC("图鉴全收集（含闪光）", "Complete journal (incl. drip)"), host);
			_cScaleLabel = Ui.GC("界面缩放", "UI scale");

			_cSpeed = BuildCycle(Plugin.SpeedOptions, "移速倍率", "Speed");
			_cJump = BuildCycle(Plugin.JumpOptions, "跳跃倍率", "Jump");
			_cSell = BuildCycle(Plugin.SellOptions, "卖鱼价值", "Sell worth");

			_cDollar = Ui.GC("美元 $", "USD $");
			_cEuro = Ui.GC("欧元 €", "EUR €");
			_cSetMoney = H(Ui.GC("设定余额", "Set balance"), host);
			_cAddMoney = H(Ui.GC("加钱", "Add money"), host);

			_cTpPrev = Ui.GC("← 上一岛", "← Prev");
			_cTpNext = Ui.GC("下一岛 →", "Next →");
			_cTpMini = H(Ui.GC("随机小岛", "Random mini island"), host);
			_cTpGo = Ui.GC("传送", "Go");
			_cIslandMinus = new GUIContent("-");
			_cIslandPlus = new GUIContent("+");

			_cKillBoss = H(Ui.GC("秒杀 Boss", "Kill boss"), host);
			_cKillAlive = H(Ui.GC("击杀全部活物（稍后自动补怪）", "Kill all alive (auto respawn)"), host);
			_cClearJournal = H(Ui.GC("清空图鉴击杀记录", "Clear journal records"), host);
			_cSpawnHeader = Ui.GC("生成器（仅主机）", "Spawner (host)");
			_cDrip = Ui.GC("闪光", "Drip");
			_cDead = Ui.GC("死亡", "Dead");
			_cSearchHint = Ui.GC("在搜索框输入 prefab 名过滤（◆=生物）", "Type prefab name to filter (◆=creature)");

			_cEspOn = Ui.GC("鱼群雷达：开", "Fish radar: ON");
			_cEspOff = Ui.GC("鱼群雷达：关", "Fish radar: OFF");
			_cEspNamesOn = Ui.GC("雷达名字：显示", "Radar names: ON");
			_cEspNamesOff = Ui.GC("雷达名字：隐藏", "Radar names: OFF");
			_cEspWorthOn = Ui.GC("雷达显示价值：开", "Radar worth: ON");
			_cEspWorthOff = Ui.GC("雷达显示价值：关", "Radar worth: OFF");
			_cItemEspOn = Ui.GC("物品雷达：开", "Item radar: ON");
			_cItemEspOff = Ui.GC("物品雷达：关", "Item radar: OFF");
			_cPerfOn = Ui.GC("性能监视：开", "Perf overlay: ON");
			_cPerfOff = Ui.GC("性能监视：关", "Perf overlay: OFF");
			_cAmmoHudOn = Ui.GC("弹药显示：开", "Ammo HUD: ON");
			_cAmmoHudOff = Ui.GC("弹药显示：关", "Ammo HUD: OFF");

			_cDiffEasy = Ui.GC("简单", "Easy");
			_cDiffDefault = Ui.GC("默认", "Default");
			_cDiffHard = Ui.GC("困难", "Hard");
			_cFFOn = H(Ui.GC("友方伤害：开", "Friendly fire: ON"), host);
			_cFFOff = H(Ui.GC("友方伤害：关", "Friendly fire: OFF"), host);
			_cFreeOn = H(Ui.GC("免费购物：开", "Free shopping: ON"), host);
			_cFreeOff = H(Ui.GC("免费购物：关", "Free shopping: OFF"), host);
			_cFullRestore = H(Ui.GC("满状态（血量/饱食/毒/火）", "Full restore (hp/food/poison/fire)"), host);

			_cSkins = H(Ui.GC("解锁全部皮肤", "Unlock all skins"), host);
			_cAchUnlock = H(Ui.GC("解锁全部成就", "Unlock achievements"), host);
			_cAchLock = H(Ui.GC("锁定全部成就", "Lock achievements"), host);
			_cFinish = H(Ui.GC("结束本局（存档结算）", "Finish game (end screen)"), host);

			_cCasinoHint = Ui.GC("轮盘：下注后开奖前开启；老虎机：拉杆前开启（仅主机）",
				"Roulette: enable after bet; Slots: before rolling (host only)");
			_cReelHint = Ui.GC("原型功能：大鱼拉力过高时可能断线，可随时切回手动",
				"Experimental: heavy fish may snap the line");
			_cBiteHint = Ui.GC("秒咬钩需鱼饵沉入水下，按原判定选鱼", "Instant bite: bait must sink underwater first");
			_cWeightHint = Ui.GC("开启后新生成的鱼都是最大体重（1.75 倍）", "Fish spawned while on roll max weight (1.75x)");

			_cMoneySection = Ui.GC("金钱", "Money");
			_cTpSection = Ui.GC("传送", "Teleport");
			_cCreatureSection = Ui.GC("生物", "Creatures");
			_cRulesSection = Ui.GC("服务器规则", "Server rules");
			_cProgressSection = Ui.GC("进度", "Progress");
		}

		private static GUIContent H(GUIContent c, string hostSuffix)
		{
			return new GUIContent(c.text + hostSuffix, c.image, c.tooltip);
		}

		private static GUIContent[] BuildCycle(float[] values, string zh, string en)
		{
			GUIContent[] arr = new GUIContent[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				arr[i] = Ui.GC(zh + " x" + values[i], en + " x" + values[i]);
			}
			return arr;
		}

		internal static void ToggleMenu()
		{
			_menuOpen = !_menuOpen;
			if (_menuOpen)
			{
				_hostStatusBuilt = false;
				EnsureSpawnList();
			}
		}

		internal static void OnGUI()
		{
			Ui.EnsureStyles();
			if (_menuOpen)
			{
				_winRect = GUI.Window(WinId, _winRect, WindowFunc, GUIContent.none, Ui.Win);
			}
		}

		private static void DrawWindow(int id)
		{
			float s = Ui.Scale;
			float w = _winRect.width;

			// ---- 标题栏 ----
			Rect header = new Rect(1f, 1f, w - 2f, Ui.HeaderH * s);
			GUI.Box(header, GUIContent.none, Ui.Header);
			Rect titleR = new Rect(Ui.Pad * s, 0f, w - 90f * s, header.height);
			GUI.Label(titleR, _title, Ui.Title);
			if (!_hostStatusBuilt)
			{
				bool hostNow = CheatCore.IsServer;
				string zh = Ui.UseZh ? (hostNow ? "主机" : "客户端") : (hostNow ? "HOST" : "CLIENT");
				_hostStatus = new GUIContent("<color=#" + (hostNow ? Ui.HostTagHex : "8a94a0") + ">" + zh + "</color>");
				_hostStatusBuilt = true;
			}
			Rect statusR = new Rect(w - 78f * s, 0f, 42f * s, header.height);
			GUI.Label(statusR, _hostStatus, Ui.Small);
			Rect closeR = new Rect(w - 32f * s, (header.height - 24f * s) * 0.5f, 24f * s, 24f * s);
			if (GUI.Button(closeR, _close, Ui.Tab))
			{
				ToggleMenu();
				return;
			}

			// ---- 页签 ----
			Rect tabsR = new Rect(Ui.Pad * s, header.height + 8f * s, w - Ui.Pad * 2f * s, Ui.TabH * s);
			float tabW = (tabsR.width - 5f * Ui.RowGap * s) / 6f;
			for (int i = 0; i < 6; i++)
			{
				Rect r = new Rect(tabsR.x + i * (tabW + Ui.RowGap * s), tabsR.y, tabW, tabsR.height);
				if (GUI.Button(r, _tabs[i], i == _tab ? Ui.TabActive : Ui.Tab))
				{
					_tab = i;
				}
			}

			// ---- 内容（外层滚动；内容高度用上一帧收敛值，一帧内稳定） ----
			float top = tabsR.y + tabsR.height + 8f * s;
			float viewH = _winRect.height - top - 34f * s;
			Rect viewR = new Rect(Ui.Pad * s, top, w - Ui.Pad * 2f * s, viewH);
			float contentH = Mathf.Max(_pageHeights[_tab], viewH);
			// scrollView 样式的悬停/聚焦背景已在 Ui.EnsureStyles 清空（游戏皮肤污染源）
			Vector2 scroll = GUI.BeginScrollView(viewR, _scrolls[_tab], new Rect(0f, 0f, viewR.width - 14f, contentH));
			Ui.BeginArea(0f, 4f * s, viewR.width - 14f);
			switch (_tab)
			{
				case 0: DrawPlayer(); break;
				case 1: DrawWorld(); break;
				case 2: DrawCasino(); break;
				case 3: DrawFishing(); break;
				case 4: DrawDisplay(); break;
				default: DrawMisc(); break;
			}
			_pageHeights[_tab] = Ui.CursorY + Ui.Pad * s;
			GUI.EndScrollView();
			_scrolls[_tab] = scroll;

			// ---- 底栏 ----
			Rect footer = new Rect(Ui.Pad * s, _winRect.height - 28f * s, w - Ui.Pad * 2f * s, 20f * s);
			GUI.Label(footer, Plugin.FooterText, Ui.Footer);

			GUI.DragWindow(new Rect(0f, 0f, w, header.height));
		}

		// ================= 玩家 =================
		private static void DrawPlayer()
		{
			float s = Ui.Scale;
			if (Ui.Button(_cCheats))
			{
				CheatCore.EnableCheats();
				CheatCore.Say(Msg.Done);
			}
			Ui.Next();
			if (Ui.Toggle(CheatCore.GodOn ? _cGodOn : _cGodOff, CheatCore.GodOn, CheatCore.IsServer))
			{
				CheatCore.ToggleGod();
			}
			Ui.Next();
			if (Ui.Toggle(CheatCore.OneShotOn ? _cOneShotOn : _cOneShotOff, CheatCore.OneShotOn, CheatCore.IsServer))
			{
				CheatCore.ToggleOneShot();
			}
			Ui.Next();
			if (Ui.Toggle(CheatCore.InfiniteAmmo ? _cAmmoOn : _cAmmoOff, CheatCore.InfiniteAmmo))
			{
				CheatCore.InfiniteAmmo = !CheatCore.InfiniteAmmo;
				CheatCore.Say(CheatCore.InfiniteAmmo ? Msg.AmmoOn : Msg.AmmoOff);
			}
			Ui.Next();
			if (Ui.Toggle(CheatCore.NoRecoil ? _cNoRecoilOn : _cNoRecoilOff, CheatCore.NoRecoil))
			{
				CheatCore.NoRecoil = !CheatCore.NoRecoil;
			}
			Ui.Next();
			if (Ui.Toggle(CheatCore.RapidFire ? _cRapidOn : _cRapidOff, CheatCore.RapidFire))
			{
				CheatCore.RapidFire = !CheatCore.RapidFire;
			}
			Ui.Next();
			if (Ui.Button(_cSpeed[Plugin.SpeedIndex]))
			{
				Plugin.CycleSpeed();
			}
			Ui.Next();
			if (Ui.Button(_cJump[Plugin.JumpIndex]))
			{
				Plugin.CycleJump();
			}
			Ui.Next();
			if (Ui.Toggle(CheatCore.HungerFreeze ? _cHungerOn : _cHungerOff, CheatCore.HungerFreeze, CheatCore.IsServer))
			{
				CheatCore.HungerFreeze = !CheatCore.HungerFreeze;
			}
			Ui.Next();
			Ui.LabelRow(_cBiteHint, Ui.Small);
			Ui.Next();

			Ui.Gap();
			Ui.SectionRow(_cMoneySection);
			Ui.Next();
			bool host = CheatCore.IsServer;

			Rect r1 = Ui.Slice(0f, 0.49f);
			Rect r2 = Ui.Slice(0.51f, 0.49f);
			if (GUI.Button(r1, _cDollar, CheatCore.MoneyCurrency == Currency.Dollar ? Ui.TabActive : Ui.Tab))
			{
				CheatCore.MoneyCurrency = Currency.Dollar;
			}
			if (GUI.Button(r2, _cEuro, CheatCore.MoneyCurrency == Currency.Euro ? Ui.TabActive : Ui.Tab))
			{
				CheatCore.MoneyCurrency = Currency.Euro;
			}
			Ui.Next();

			Rect fieldR = Ui.Slice(0f, 0.62f);
			string edited = GUI.TextField(fieldR, _moneyInput, Ui.Field);
			if (!ReferenceEquals(edited, _moneyInput))
			{
				_moneyInput = edited;
			}
			Rect addR = Ui.Slice(0.64f, 0.36f);
			if (GUI.Button(addR, _cAddMoney, host ? Ui.BtnPrimary : Ui.BtnDisabled) && host && int.TryParse(_moneyInput, out int amt))
			{
				CheatCore.AddMoney(amt);
			}
			Ui.Next();

			if (Ui.Button(_cSetMoney, host ? Ui.Btn : Ui.BtnDisabled) && host && int.TryParse(_moneyInput, out int set))
			{
				CheatCore.SetMoney(set);
			}
			Ui.Next();
			Ui.Gap(2f * s);
		}

		// ================= 世界 =================
		private static void DrawWorld()
		{
			float s = Ui.Scale;
			bool host = CheatCore.IsServer;

			Ui.SectionRow(_cTpSection);
			Ui.Next();
			Rect half1 = Ui.Slice(0f, 0.49f);
			Rect half2 = Ui.Slice(0.51f, 0.49f);
			if (GUI.Button(half1, _cTpPrev, Ui.Btn))
			{
				CheatCore.TpIsland(true);
			}
			if (GUI.Button(half2, _cTpNext, Ui.Btn))
			{
				CheatCore.TpIsland(false);
			}
			Ui.Next();
			if (Ui.Button(_cTpMini, host))
			{
				CheatCore.TpRandomMini();
			}
			Ui.Next();

			// 指定岛：[-] 岛N [+] [传送]；列表为空（如在大厅打开过菜单）则持续尝试重建
			if (_mainIslands == null || _mainIslands.Length == 0)
			{
				BuildMainIslands();
			}
			if (_islandSel < 0 && _mainIslands != null && _mainIslands.Length > 0)
			{
				_islandSel = 0;
			}
			Rect minusR = Ui.Slice(0f, 0.12f);
			Rect labelR = Ui.Slice(0.14f, 0.32f);
			Rect plusR = Ui.Slice(0.48f, 0.12f);
			Rect goR = Ui.Slice(0.62f, 0.38f);
			if (GUI.Button(minusR, _cIslandMinus, Ui.Tab) && _mainIslands != null && _mainIslands.Length > 0)
			{
				_islandSel = (_islandSel + _mainIslands.Length - 1) % _mainIslands.Length;
			}
			if (GUI.Button(plusR, _cIslandPlus, Ui.Tab) && _mainIslands != null && _mainIslands.Length > 0)
			{
				_islandSel = (_islandSel + 1) % _mainIslands.Length;
			}
			if (_islandSel >= 0 && _mainIslands != null && _mainIslands.Length > 0)
			{
				int sel = Mathf.Clamp(_islandSel, 0, _mainIslands.Length - 1);
				if (!_islandLabel.text.EndsWith(_mainIslands[sel].ToString()))
				{
					_islandLabel = new GUIContent((Ui.UseZh ? "岛 " : "Island ") + _mainIslands[sel]);
				}
				GUI.Label(labelR, _islandLabel, Ui.Value);
				if (GUI.Button(goR, _cTpGo, host ? Ui.BtnPrimary : Ui.BtnDisabled) && host)
				{
					CheatCore.TpIsland(_mainIslands[sel]);
				}
			}
			Ui.Next();

			Ui.Gap();
			Ui.SectionRow(_cCreatureSection);
			Ui.Next();
			if (Ui.Button(_cKillBoss, host))
			{
				CheatCore.KillBoss();
			}
			Ui.Next();
			if (Ui.Button(_cKillAlive, host))
			{
				if (CheatCore.KillAllAliveCreatures())
				{
					CheatCore.Say(Msg.Done);
				}
				else
				{
					CheatCore.Say(Msg.NeedHost);
				}
			}
			Ui.Next();
			if (Ui.Button(_cClearJournal, host))
			{
				CheatCore.SetAllCreaturesKilled(false, false);
				CheatCore.Say(Msg.Done);
			}
			Ui.Next();
			if (Ui.Button(_cJournalComplete, host))
			{
				CheatCore.CompleteJournal();
			}
			Ui.Next();

			Ui.Gap();
			Ui.SectionRow(_cSpawnHeader);
			Ui.Next();

			string edited = GUI.TextField(Ui.Slice(0f, 0.58f), _search, Ui.Field);
			if (!ReferenceEquals(edited, _search))
			{
				_search = edited;
				FilterSpawnList();
			}
			Rect dripR = Ui.Slice(0.60f, 0.19f);
			Rect deadR = Ui.Slice(0.80f, 0.20f);
			if (GUI.Button(dripR, _cDrip, _spawnDrip ? Ui.TabActive : Ui.Tab))
			{
				_spawnDrip = !_spawnDrip;
			}
			if (GUI.Button(deadR, _cDead, _spawnDead ? Ui.TabActive : Ui.Tab))
			{
				_spawnDead = !_spawnDead;
			}
			Ui.Next();
			Ui.LabelRow(_cSearchHint, Ui.Small);
			Ui.Next();

			// 列表（由外层滚动承载）
			Ui.Row(24f * s);
			int count = _viewLabels.Length;
			for (int i = 0; i < count; i++)
			{
				if (GUI.Button(Ui.Slice(0f, 1f), _viewLabels[i], Ui.Btn) && host)
				{
					CheatCore.SpawnPrefab(_viewItems[i], _spawnDrip, _spawnDead);
				}
				Ui.Next();
			}
		}

		private static void BuildMainIslands()
		{
			try
			{
				int total = IslandManager.TotalIslands;
				if (total <= 0)
				{
					_mainIslands = new byte[0];
					return;
				}
				List<byte> list = new List<byte>(total);
				for (byte i = 0; i < total; i++)
				{
					if (IslandManager.IsMainIsland(i))
					{
						list.Add(i);
					}
				}
				_mainIslands = list.ToArray();
				_islandSel = list.Count > 0 ? 0 : -1;
			}
			catch (Exception)
			{
				_mainIslands = new byte[0];
				_islandSel = -1;
			}
		}

		// ================= 赌场 =================
		private static void DrawCasino()
		{
			float s = Ui.Scale;
			bool host = CheatCore.IsServer;
			if (Ui.Toggle(CheatCore.RouletteWin ? _cRouletteOn : _cRouletteOff, CheatCore.RouletteWin, host))
			{
				CheatCore.RouletteWin = !CheatCore.RouletteWin;
			}
			Ui.Next();
			if (Ui.Toggle(CheatCore.SlotsLegendary ? _cSlotsOn : _cSlotsOff, CheatCore.SlotsLegendary, host))
			{
				CheatCore.SlotsLegendary = !CheatCore.SlotsLegendary;
				CheatCore.ApplySlotsCheat();
			}
			Ui.Next();
			Ui.LabelRow(_cCasinoHint, Ui.Small);
			Ui.Next();
			Ui.Gap(2f * s);
		}

		// ================= 钓鱼 =================
		private static void DrawFishing()
		{
			float s = Ui.Scale;
			if (Ui.Toggle(CheatCore.InstantBite ? _cBiteOn : _cBiteOff, CheatCore.InstantBite))
			{
				CheatCore.InstantBite = !CheatCore.InstantBite;
				CheatCore.Say(CheatCore.InstantBite ? Msg.BiteOn : Msg.BiteOff);
			}
			Ui.Next();
			if (Ui.Toggle(CheatCore.AutoReel ? _cAutoReelOn : _cAutoReelOff, CheatCore.AutoReel))
			{
				CheatCore.AutoReel = !CheatCore.AutoReel;
			}
			Ui.Next();
			if (CheatCore.AutoReel)
			{
				Ui.LabelRow(_cReelHint, Ui.Small);
				Ui.Next();
			}
			if (Ui.Toggle(CheatCore.MaxWeight ? _cWeightOn : _cWeightOff, CheatCore.MaxWeight, CheatCore.IsServer))
			{
				CheatCore.MaxWeight = !CheatCore.MaxWeight;
			}
			Ui.Next();
			Ui.LabelRow(_cWeightHint, Ui.Small);
			Ui.Next();
			Ui.LabelRow(_cBiteHint, Ui.Small);
			Ui.Next();
			Ui.Gap(2f * s);
		}

		// ================= 显示 =================
		private static void DrawDisplay()
		{
			if (Ui.Toggle(Overlays.EspEnabled ? _cEspOn : _cEspOff, Overlays.EspEnabled))
			{
				Overlays.ToggleEsp();
			}
			Ui.Next();
			if (Ui.Toggle(Plugin.EspShowNames.Value ? _cEspNamesOn : _cEspNamesOff, Plugin.EspShowNames.Value))
			{
				Plugin.EspShowNames.Value = !Plugin.EspShowNames.Value;
			}
			Ui.Next();
			if (Ui.Toggle(Plugin.EspShowWorth.Value ? _cEspWorthOn : _cEspWorthOff, Plugin.EspShowWorth.Value))
			{
				Plugin.EspShowWorth.Value = !Plugin.EspShowWorth.Value;
			}
			Ui.Next();
			if (Ui.Toggle(Plugin.ItemEspEnabled.Value ? _cItemEspOn : _cItemEspOff, Plugin.ItemEspEnabled.Value))
			{
				Plugin.ItemEspEnabled.Value = !Plugin.ItemEspEnabled.Value;
			}
			Ui.Next();
			if (Ui.Toggle(Plugin.AmmoHudEnabled.Value ? _cAmmoHudOn : _cAmmoHudOff, Plugin.AmmoHudEnabled.Value))
			{
				Plugin.AmmoHudEnabled.Value = !Plugin.AmmoHudEnabled.Value;
			}
			Ui.Next();
			if (Ui.Toggle(Overlays.PerfEnabled ? _cPerfOn : _cPerfOff, Overlays.PerfEnabled))
			{
				Overlays.TogglePerf();
			}
			Ui.Next();

			Ui.Gap();
			Ui.LabelRow(_cScaleLabel, Ui.Dim);
			Ui.Next();
			Ui.Row(22f);
			Rect sliderR = Ui.Slice(0.02f, 0.66f);
			float v = GUI.HorizontalSlider(sliderR, Ui.Scale, 0.75f, 1.5f);
			// 数值文本缓存：仅在跨过 0.01 档位时重建字符串
			if (Mathf.Abs(Ui.Scale - _scaleTextValue) > 0.005f)
			{
				_scaleTextValue = Ui.Scale;
				_scaleText = Ui.Scale.ToString("F2") + "x";
			}
			GUI.Label(Ui.Slice(0.72f, 0.26f), _scaleText, Ui.Value);
			if (Mathf.Abs(v - Ui.Scale) > 0.0005f)
			{
				Ui.Scale = Mathf.Clamp(v, 0.75f, 1.5f);
				Ui.InvalidateStyles();
				Plugin.SaveUiScale(Ui.Scale);
			}
			Ui.Next();
		}

		private static float _scaleTextValue = -1f;
		private static string _scaleText = "1.00x";

		// ================= 杂项 =================
		private static void DrawMisc()
		{
			float s = Ui.Scale;
			bool host = CheatCore.IsServer;

			Ui.SectionRow(_cRulesSection);
			Ui.Next();

			Rect d1 = Ui.Slice(0f, 0.32f);
			Rect d2 = Ui.Slice(0.34f, 0.32f);
			Rect d3 = Ui.Slice(0.68f, 0.32f);
			int diff = CheatCore.GetDifficulty();
			if (GUI.Button(d1, _cDiffEasy, diff == 0 ? Ui.TabActive : Ui.Tab))
			{
				CheatCore.SetDifficulty(0);
			}
			if (GUI.Button(d2, _cDiffDefault, diff == 1 ? Ui.TabActive : Ui.Tab))
			{
				CheatCore.SetDifficulty(1);
			}
			if (GUI.Button(d3, _cDiffHard, diff == 2 ? Ui.TabActive : Ui.Tab))
			{
				CheatCore.SetDifficulty(2);
			}
			Ui.Next();

			bool ff = ServerSettings.Instance && ServerSettings.UseFriendlyFire;
			if (Ui.Toggle(ff ? _cFFOn : _cFFOff, ff, host))
			{
				CheatCore.ToggleFriendlyFire(!ff);
			}
			Ui.Next();
			if (Ui.Toggle(CheatCore.FreeShopping ? _cFreeOn : _cFreeOff, CheatCore.FreeShopping, host))
			{
				CheatCore.FreeShopping = !CheatCore.FreeShopping;
			}
			Ui.Next();
			if (Ui.Button(_cSell[Plugin.SellIndex], host ? Ui.Btn : Ui.BtnDisabled) && host)
			{
				Plugin.CycleSell();
			}
			Ui.Next();
			if (Ui.Button(_cFullRestore, host))
			{
				CheatCore.FullRestore();
			}
			Ui.Next();

			Ui.Gap();
			Ui.SectionRow(_cProgressSection);
			Ui.Next();
			if (Ui.Button(_cSkins, host))
			{
				CheatCore.UnlockAllSkins();
			}
			Ui.Next();
			if (Ui.Button(_cAchUnlock, host))
			{
				CheatCore.UnlockAchievements();
			}
			Ui.Next();
			if (Ui.Button(_cAchLock, host))
			{
				CheatCore.LockAchievements();
			}
			Ui.Next();
			if (Ui.Button(_cFinish, host))
			{
				CheatCore.FinishGame();
			}
			Ui.Next();
			Ui.Gap(2f * s);
		}

		// ================= 生成器列表 =================
		private static void EnsureSpawnList()
		{
			if (_listBuilt)
			{
				return;
			}
			_listBuilt = true;
			Item[] items;
			try
			{
				items = Resources.LoadAll<Item>("Items");
			}
			catch (Exception)
			{
				return;
			}
			if (items == null || items.Length == 0)
			{
				return;
			}

			string[] names = new string[items.Length];
			bool[] creatures = new bool[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				Item it = items[i];
				if (!it)
				{
					names[i] = "?" + i;
					continue;
				}
				// prefab 上 Item.Creature 恒为 null（运行时才赋值），必须 GetComponent 判定
				creatures[i] = it.GetComponent<Creature>();
				names[i] = LocalizedName(it);
			}

			// 重名条目追加 prefab 名区分（如两个"天使鱼"变体）
			Dictionary<string, int> nameCounts = new Dictionary<string, int>(names.Length);
			for (int i = 0; i < names.Length; i++)
			{
				if (nameCounts.TryGetValue(names[i], out int c))
				{
					nameCounts[names[i]] = c + 1;
				}
				else
				{
					nameCounts[names[i]] = 1;
				}
			}
			Dictionary<string, int> suffixUsed = new Dictionary<string, int>(names.Length);
			for (int i = 0; i < names.Length; i++)
			{
				Item it = items[i];
				if (it && nameCounts[names[i]] > 1)
				{
					string prefab = StripClone(it.name);
					if (suffixUsed.TryGetValue(names[i], out int used))
					{
						suffixUsed[names[i]] = used + 1;
					}
					else
					{
						suffixUsed[names[i]] = 1;
					}
					names[i] = names[i] + " (" + prefab + "#" + suffixUsed[names[i]] + ")";
				}
			}
			Array.Sort(names, items, StringComparer.Ordinal);

			_allItems = items;
			_allIsCreature = creatures;
			_allLabels = new GUIContent[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				// 生物加青色菱形前缀（Btn.richText=true 承担富文本）
				string prefix = creatures[i] ? "<color=#" + Ui.HostTagHex + ">◆ </color>" : "";
				_allLabels[i] = new GUIContent(prefix + names[i]);
			}
			FilterSpawnList();
		}

		private static void FilterSpawnList()
		{
			string filter = _search == null ? string.Empty : _search.Trim();
			if (filter.Length == 0)
			{
				_viewItems = _allItems;
				_viewLabels = _allLabels;
				return;
			}
			// 打字期分配可接受：仅在搜索串变化时重建
			List<Item> items = new List<Item>(_allItems.Length);
			List<GUIContent> labels = new List<GUIContent>(_allItems.Length);
			for (int i = 0; i < _allItems.Length; i++)
			{
				if (_allItems[i] && _allItems[i].name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					items.Add(_allItems[i]);
					labels.Add(_allLabels[i]);
				}
			}
			_viewItems = items.ToArray();
			_viewLabels = labels.ToArray();
		}

		/// <summary>取物品本地化名（游戏内悬停/商店同一来源）；本地化未就绪时回退 prefab 名。</summary>
		private static string LocalizedName(Item it)
		{
			try
			{
				string n = it.GetName();
				if (!string.IsNullOrEmpty(n))
				{
					return n;
				}
			}
			catch (Exception)
			{
			}
			return StripClone(it.name);
		}

		private static string StripClone(string name)
		{
			if (name != null && name.Length > 7 && name.EndsWith("(Clone)", StringComparison.Ordinal))
			{
				return name.Substring(0, name.Length - 7);
			}
			return name;
		}
	}
}
