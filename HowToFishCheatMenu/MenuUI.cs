using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>
	/// UI 3.0：单窗口 + 六页签（玩家/世界/赌场/钓鱼/显示/杂项）。
	/// 视觉：渐变标题栏 + 状态徽章、iOS 式开关行、主/次按钮层级、窗口阴影描边。
	/// 规避 GC 的关键点：
	/// - 所有 GUIContent / GUIStyle 在初始化时创建并复用
	/// - 布局用 Ui 静态游标推进，不走 GUILayout
	/// - 运行期字符串只在状态变化时生成
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
		private static bool _hostNow;
		private static string _hostText = string.Empty;
		private static GUIContent _versionChip;

		internal static bool MenuOpen => _menuOpen;

		// ---- 标签缓存 ----
		private static GUIContent _title;
		private static GUIContent _close;
		private static GUIContent[] _tabs;
		private static GUIContent _cCheats;
		private static GUIContent _cGod, _cOneShot, _cAmmo, _cNoRecoil, _cRapid, _cHunger;
		private static GUIContent _cBite, _cAutoReel, _cWeight, _cRoulette, _cSlots;
		private static GUIContent _cEsp, _cEspNames, _cEspWorth, _cItemEsp, _cPerf, _cAmmoHud;
		private static GUIContent _cFF, _cFree;
		private static GUIContent[] _cSpeed;
		private static GUIContent[] _cJump;
		private static GUIContent[] _cSell;
		private static GUIContent _cDollar, _cEuro;
		private static GUIContent _cSetMoney, _cAddMoney;
		private static GUIContent _cTpPrev, _cTpNext, _cTpMini, _cTpGo, _cIslandMinus, _cIslandPlus;
		private static GUIContent _cKillBoss, _cKillAlive, _cClearJournal, _cJournalComplete;
		private static GUIContent _cSpawnHeader, _cDrip, _cDead;
		private static GUIContent _cPerfTab;
		private static GUIContent _cDiffEasy, _cDiffDefault, _cDiffHard;
		private static GUIContent _cFullRestore;
		private static GUIContent _cSkins, _cAchUnlock, _cAchLock, _cFinish;
		private static GUIContent _cSellAction;
		private static GUIContent _cCasinoHint, _cReelHint, _cBiteHint, _cWeightHint, _cSearchHint;
		private static GUIContent _cMoneySection, _cTpSection, _cCreatureSection, _cRulesSection, _cProgressSection;
		private static GUIContent _cScaleLabel;

		internal static void Init(Plugin plugin)
		{
			_plugin = plugin;
			_moneyInput = Plugin.MoneyAmount.Value.ToString();

			_title = Ui.GC("鱼力全开 · 作弊菜单", "How to Fish · Cheat Menu");
			_close = new GUIContent("✕");
			_versionChip = new GUIContent("v" + Plugin.PluginVersion);
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
			_cGod = H(Ui.GC("上帝模式", "God mode"), host);
			_cOneShot = H(Ui.GC("一击必杀", "One shot"), host);
			_cAmmo = Ui.GC("无限弹匣", "Infinite mag");
			_cNoRecoil = Ui.GC("无后座", "No recoil");
			_cRapid = Ui.GC("极速射击", "Rapid fire");
			_cHunger = H(Ui.GC("永不饥饿", "No hunger"), host);
			_cBite = Ui.GC("秒咬钩", "Instant bite");
			_cAutoReel = Ui.GC("自动收线（钩上有鱼才收）", "Auto reel (fish hooked)");
			_cWeight = H(Ui.GC("满重量渔获", "Max weight"), host);
			_cRoulette = H(Ui.GC("轮盘必胜", "Roulette always win"), host);
			_cSlots = H(Ui.GC("老虎机必出传说", "Slots legendary"), host);

			_cSpeed = BuildCycle(Plugin.SpeedOptions, "移速倍率", "Speed");
			_cJump = BuildCycle(Plugin.JumpOptions, "跳跃倍率", "Jump");
			_cSell = BuildCycle(Plugin.SellOptions, "卖鱼价值", "Sell worth");
			_cSellAction = H(Ui.GC("卖鱼价值倍率", "Sell worth multiplier"), host);

			_cDollar = Ui.GC("美元 $", "USD $");
			_cEuro = Ui.GC("欧元 €", "EUR €");
			_cSetMoney = H(Ui.GC("设定余额", "Set balance"), host);
			_cAddMoney = Ui.GC("加钱", "Add money");

			_cTpPrev = Ui.GC("← 上一岛", "← Prev");
			_cTpNext = Ui.GC("下一岛 →", "Next →");
			_cTpMini = H(Ui.GC("随机小岛", "Random mini island"), host);
			_cTpGo = Ui.GC("传送", "Go");
			_cIslandMinus = new GUIContent("－");
			_cIslandPlus = new GUIContent("＋");

			_cKillBoss = H(Ui.GC("秒杀 Boss", "Kill boss"), host);
			_cKillAlive = H(Ui.GC("击杀全部活物", "Kill all alive"), host);
			_cClearJournal = H(Ui.GC("清空图鉴", "Clear journal"), host);
			_cJournalComplete = H(Ui.GC("图鉴全收集", "Complete journal"), host);
			_cSpawnHeader = Ui.GC("生成器（仅主机）", "Spawner (host)");
			_cDrip = Ui.GC("闪光", "Drip");
			_cDead = Ui.GC("死亡", "Dead");
			_cSearchHint = Ui.GC("输入 prefab 名过滤（◆ = 生物）", "Type prefab name to filter (◆ = creature)");

			_cEsp = Ui.GC("鱼群雷达", "Fish radar");
			_cEspNames = Ui.GC("雷达名字", "Radar names");
			_cEspWorth = Ui.GC("雷达显示价值", "Radar worth");
			_cItemEsp = Ui.GC("物品雷达", "Item radar");
			_cPerf = Ui.GC("性能监视", "Perf overlay");
			_cAmmoHud = Ui.GC("弹药显示", "Ammo HUD");
			_cPerfTab = Ui.GC("性能监视", "Perf overlay");

			_cDiffEasy = Ui.GC("简单", "Easy");
			_cDiffDefault = Ui.GC("默认", "Default");
			_cDiffHard = Ui.GC("困难", "Hard");
			_cFF = H(Ui.GC("友方伤害", "Friendly fire"), host);
			_cFree = H(Ui.GC("免费购物", "Free shopping"), host);
			_cFullRestore = H(Ui.GC("满状态（血量/饱食/毒/火）", "Full restore (hp/food/poison/fire)"), host);

			_cSkins = H(Ui.GC("解锁全部皮肤", "Unlock all skins"), host);
			_cAchUnlock = H(Ui.GC("解锁全部成就", "Unlock achievements"), host);
			_cAchLock = H(Ui.GC("锁定全部成就", "Lock achievements"), host);
			_cFinish = H(Ui.GC("结束本局（存档结算）", "Finish game"), host);

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
			_cScaleLabel = Ui.GC("界面缩放", "UI scale");
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
				Ui.DrawShadow(_winRect, 16f * Ui.Scale);
				_winRect = GUI.Window(WinId, _winRect, WindowFunc, GUIContent.none, Ui.Win);
			}
		}

		private static void DrawWindow(int id)
		{
			float s = Ui.Scale;
			float w = _winRect.width;

			// ---- 标题栏（渐变 + 徽章） ----
			Rect header = new Rect(1f, 1f, w - 2f, Ui.HeaderH * s);
			GUI.Box(header, GUIContent.none, Ui.Header);
			Rect titleR = new Rect(Ui.Pad * s, 0f, w - 250f * s, header.height);
			GUI.Label(titleR, _title, Ui.Title);

			if (!_hostStatusBuilt)
			{
				_hostNow = CheatCore.IsServer;
				_hostText = Ui.UseZh ? (_hostNow ? "主机" : "客户端") : (_hostNow ? "HOST" : "CLIENT");
				_hostStatusBuilt = true;
			}
			// 主机徽章（圆点 + 文本）
			float chipH = 20f * s;
			Rect hostR = new Rect(w - 36f * s - 58f * s, (header.height - chipH) * 0.5f, 58f * s, chipH);
			GUI.Box(hostR, GUIContent.none, Ui.Chip);
			Ui.Dot(new Rect(hostR.x + 7f * s, hostR.y + (hostR.height - 6f * s) * 0.5f, 6f * s, 6f * s),
				_hostNow ? Ui.ColOn : Ui.ColOff);
			GUI.Label(new Rect(hostR.x + 16f * s, hostR.y, hostR.width - 18f * s, hostR.height), _hostText, Ui.Small);
			// 版本徽章
			Rect verR = new Rect(hostR.x - 8f * s - 56f * s, hostR.y, 56f * s, chipH);
			GUI.Label(verR, _versionChip, Ui.Chip);
			// 关闭按钮
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
			float viewH = _winRect.height - top - 36f * s;
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

			// ---- 底栏（上缘细分隔线） ----
			float footY = _winRect.height - 30f * s;
			Color old = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, 0.07f);
			GUI.DrawTexture(new Rect(6f * s, footY - 3f * s, w - 12f * s, 1f), Texture2D.whiteTexture);
			GUI.color = old;
			Rect footer = new Rect(Ui.Pad * s, footY, w - Ui.Pad * 2f * s, 22f * s);
			GUI.Label(footer, Plugin.FooterText, Ui.Footer);

			GUI.DragWindow(new Rect(0f, 0f, w, header.height));
		}

		// ================= 玩家 =================
		private static void DrawPlayer()
		{
			float s = Ui.Scale;
			bool host = CheatCore.IsServer;

			if (Ui.Ghost(_cCheats))
			{
				CheatCore.EnableCheats();
				CheatCore.Say(Msg.Done);
			}
			Ui.Next();
			if (Ui.ToggleRow(_cGod, CheatCore.GodOn, host))
			{
				CheatCore.ToggleGod();
			}
			Ui.Next();
			if (Ui.ToggleRow(_cOneShot, CheatCore.OneShotOn, host))
			{
				CheatCore.ToggleOneShot();
			}
			Ui.Next();
			if (Ui.ToggleRow(_cAmmo, CheatCore.InfiniteAmmo))
			{
				CheatCore.InfiniteAmmo = !CheatCore.InfiniteAmmo;
				CheatCore.Say(CheatCore.InfiniteAmmo ? Msg.AmmoOn : Msg.AmmoOff);
			}
			Ui.Next();
			if (Ui.ToggleRow(_cNoRecoil, CheatCore.NoRecoil))
			{
				CheatCore.NoRecoil = !CheatCore.NoRecoil;
			}
			Ui.Next();
			if (Ui.ToggleRow(_cRapid, CheatCore.RapidFire))
			{
				CheatCore.RapidFire = !CheatCore.RapidFire;
			}
			Ui.Next();
			if (Ui.ToggleRow(_cHunger, CheatCore.HungerFreeze, host))
			{
				CheatCore.HungerFreeze = !CheatCore.HungerFreeze;
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

			Ui.Gap();
			Ui.SectionRow(_cMoneySection);
			Ui.Next();

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

			Rect fieldR = Ui.Slice(0f, 0.60f);
			string edited = GUI.TextField(fieldR, _moneyInput, Ui.Field);
			if (!ReferenceEquals(edited, _moneyInput))
			{
				_moneyInput = edited;
			}
			Rect addR = Ui.Slice(0.62f, 0.38f);
			if (Ui.Primary(_cAddMoney, host) && host && int.TryParse(_moneyInput, out int amt))
			{
				CheatCore.AddMoney(amt);
			}
			Ui.Next();

			if (Ui.Ghost(_cSetMoney, host) && host && int.TryParse(_moneyInput, out int set))
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
			if (GUI.Button(half1, _cTpPrev, Ui.BtnGhost))
			{
				CheatCore.TpIsland(true);
			}
			if (GUI.Button(half2, _cTpNext, Ui.BtnGhost))
			{
				CheatCore.TpIsland(false);
			}
			Ui.Next();
			if (Ui.Ghost(_cTpMini, host))
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
			if (GUI.Button(minusR, _cIslandMinus, Ui.BtnGhost) && _mainIslands != null && _mainIslands.Length > 0)
			{
				_islandSel = (_islandSel + _mainIslands.Length - 1) % _mainIslands.Length;
			}
			if (GUI.Button(plusR, _cIslandPlus, Ui.BtnGhost) && _mainIslands != null && _mainIslands.Length > 0)
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
				if (Ui.Primary(_cTpGo, host) && host)
				{
					CheatCore.TpIsland(_mainIslands[sel]);
				}
			}
			Ui.Next();

			Ui.Gap();
			Ui.SectionRow(_cCreatureSection);
			Ui.Next();
			Rect c1 = Ui.Slice(0f, 0.49f);
			Rect c2 = Ui.Slice(0.51f, 0.49f);
			if (GUI.Button(c1, _cKillBoss, host ? Ui.BtnGhost : Ui.BtnDisabled))
			{
				CheatCore.KillBoss();
			}
			if (GUI.Button(c2, _cKillAlive, host ? Ui.BtnGhost : Ui.BtnDisabled))
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
			Rect c3 = Ui.Slice(0f, 0.49f);
			Rect c4 = Ui.Slice(0.51f, 0.49f);
			if (GUI.Button(c3, _cClearJournal, host ? Ui.BtnGhost : Ui.BtnDisabled))
			{
				CheatCore.SetAllCreaturesKilled(false, false);
				CheatCore.Say(Msg.Done);
			}
			if (GUI.Button(c4, _cJournalComplete, host ? Ui.BtnGhost : Ui.BtnDisabled))
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
			if (Ui.ToggleRow(_cRoulette, CheatCore.RouletteWin, host))
			{
				CheatCore.RouletteWin = !CheatCore.RouletteWin;
			}
			Ui.Next();
			if (Ui.ToggleRow(_cSlots, CheatCore.SlotsLegendary, host))
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
			if (Ui.ToggleRow(_cBite, CheatCore.InstantBite))
			{
				CheatCore.InstantBite = !CheatCore.InstantBite;
				CheatCore.Say(CheatCore.InstantBite ? Msg.BiteOn : Msg.BiteOff);
			}
			Ui.Next();
			if (Ui.ToggleRow(_cAutoReel, CheatCore.AutoReel))
			{
				CheatCore.AutoReel = !CheatCore.AutoReel;
			}
			Ui.Next();
			if (CheatCore.AutoReel)
			{
				Ui.LabelRow(_cReelHint, Ui.Small);
				Ui.Next();
			}
			if (Ui.ToggleRow(_cWeight, CheatCore.MaxWeight, CheatCore.IsServer))
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
			if (Ui.ToggleRow(_cEsp, Overlays.EspEnabled))
			{
				Overlays.ToggleEsp();
			}
			Ui.Next();
			if (Ui.ToggleRow(_cEspNames, Plugin.EspShowNames.Value))
			{
				Plugin.EspShowNames.Value = !Plugin.EspShowNames.Value;
			}
			Ui.Next();
			if (Ui.ToggleRow(_cEspWorth, Plugin.EspShowWorth.Value))
			{
				Plugin.EspShowWorth.Value = !Plugin.EspShowWorth.Value;
			}
			Ui.Next();
			if (Ui.ToggleRow(_cItemEsp, Plugin.ItemEspEnabled.Value))
			{
				Plugin.ItemEspEnabled.Value = !Plugin.ItemEspEnabled.Value;
			}
			Ui.Next();
			if (Ui.ToggleRow(_cAmmoHud, Plugin.AmmoHudEnabled.Value))
			{
				Plugin.AmmoHudEnabled.Value = !Plugin.AmmoHudEnabled.Value;
			}
			Ui.Next();
			if (Ui.ToggleRow(_cPerfTab, Overlays.PerfEnabled))
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
			if (Ui.ToggleRow(_cFF, ff, host))
			{
				CheatCore.ToggleFriendlyFire(!ff);
			}
			Ui.Next();
			if (Ui.ToggleRow(_cFree, CheatCore.FreeShopping, host))
			{
				CheatCore.FreeShopping = !CheatCore.FreeShopping;
			}
			Ui.Next();
			if (Ui.Button(_cSell[Plugin.SellIndex], host) && host)
			{
				Plugin.CycleSell();
			}
			Ui.Next();
			if (Ui.Ghost(_cFullRestore, host))
			{
				CheatCore.FullRestore();
			}
			Ui.Next();

			Ui.Gap();
			Ui.SectionRow(_cProgressSection);
			Ui.Next();
			Rect p1 = Ui.Slice(0f, 0.49f);
			Rect p2 = Ui.Slice(0.51f, 0.49f);
			if (GUI.Button(p1, _cSkins, host ? Ui.BtnGhost : Ui.BtnDisabled))
			{
				CheatCore.UnlockAllSkins();
			}
			if (GUI.Button(p2, _cAchUnlock, host ? Ui.BtnGhost : Ui.BtnDisabled))
			{
				CheatCore.UnlockAchievements();
			}
			Ui.Next();
			Rect p3 = Ui.Slice(0f, 0.49f);
			Rect p4 = Ui.Slice(0.51f, 0.49f);
			if (GUI.Button(p3, _cAchLock, host ? Ui.BtnGhost : Ui.BtnDisabled))
			{
				CheatCore.LockAchievements();
			}
			if (GUI.Button(p4, _cFinish, host ? Ui.BtnGhost : Ui.BtnDisabled))
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
