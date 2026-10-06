using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>
	/// UI 4.2 "Candy"（H 糖果街机 · 完整版）：
	/// 单一糖果窗口 + 分组卡片（白底墨描边硬阴影，彩色标签牌骑上沿）。
	/// 规避 GC：所有 GUIContent / 纹理 / 样式一次构建复用；布局用 Ui 静态游标推进；
	/// 卡片高度逐帧收敛（首帧估算，次帧起实测）。
	/// </summary>
	internal static class MenuUI
	{
		private const int WinId = 0x48544633;
		private static bool _menuOpen;
		private static int _tab;
		private static Rect _winRect = new Rect(40f, 40f, 500f, 680f);
		private static readonly GUI.WindowFunction WindowFunc = DrawWindow;
		private static readonly float[] _pageHeights = new float[6];
		private static readonly Vector2[] _scrolls = new Vector2[6];

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

		// ---- 标签牌注册 ----
		private static int _pSurvival, _pWeapon, _pMoney, _pTp, _pCreature, _pSpawner;
		private static int _pCasino, _pFishing, _pOverlay, _pScale, _pRules, _pProgress;

		internal static bool MenuOpen => _menuOpen;
		internal static bool PanelOpen => _menuOpen;

		// ---- 标签缓存 ----
		private static GUIContent _close;
		private static GUIContent _logo;
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
		private static GUIContent _cDrip, _cDead;
		private static GUIContent _cDiffEasy, _cDiffDefault, _cDiffHard;
		private static GUIContent _cFullRestore;
		private static GUIContent _cSkins, _cAchUnlock, _cAchLock, _cFinish;
		private static GUIContent _cCasinoHint, _cReelHint, _cBiteHint, _cWeightHint, _cSearchHint;
		private static GUIContent _cScaleLabel;

		internal static void Init(Plugin plugin)
		{
			_moneyInput = Plugin.MoneyAmount.Value.ToString();

			_close = new GUIContent("✕");
			_logo = new GUIContent("鱼");
			_tabs = new[]
			{
				Ui.GC("玩家", "Player"),
				Ui.GC("世界", "World"),
				Ui.GC("赌场", "Casino"),
				Ui.GC("钓鱼", "Fishing"),
				Ui.GC("显示", "HUD"),
				Ui.GC("杂项", "Misc"),
			};

			// 标签牌：内容 + 配色（0 香蕉 1 天蓝 2 丁香 3 薄荷 4 珊瑚 5 奶油）
			_pSurvival = Ui.RegisterPlate(Ui.GC("生存"), 3);
			_pWeapon = Ui.RegisterPlate(Ui.GC("武器"), 1);
			_pMoney = Ui.RegisterPlate(Ui.GC("金钱"), 0);
			_pTp = Ui.RegisterPlate(Ui.GC("传送"), 1);
			_pCreature = Ui.RegisterPlate(Ui.GC("生物"), 4);
			_pSpawner = Ui.RegisterPlate(Ui.GC("生成器"), 2);
			_pCasino = Ui.RegisterPlate(Ui.GC("赌场"), 2);
			_pFishing = Ui.RegisterPlate(Ui.GC("钓鱼辅助"), 3);
			_pOverlay = Ui.RegisterPlate(Ui.GC("显示"), 1);
			_pScale = Ui.RegisterPlate(Ui.GC("界面缩放"), 0);
			_pRules = Ui.RegisterPlate(Ui.GC("服务器规则"), 1);
			_pProgress = Ui.RegisterPlate(Ui.GC("进度"), 0);

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
			_cWeight = H(Ui.GC("满重量渔获 ×1.75", "Max weight ×1.75"), host);
			_cRoulette = H(Ui.GC("轮盘必胜", "Roulette always win"), host);
			_cSlots = H(Ui.GC("老虎机必出传说", "Slots legendary"), host);

			_cSpeed = BuildCycle(Plugin.SpeedOptions, "移速倍率", "Speed");
			_cJump = BuildCycle(Plugin.JumpOptions, "跳跃倍率", "Jump");
			_cSell = BuildCycle(Plugin.SellOptions, "卖鱼价值", "Sell worth");

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
			_cDrip = Ui.GC("闪光", "Drip");
			_cDead = Ui.GC("死亡", "Dead");
			_cSearchHint = Ui.GC("输入 prefab 名过滤（◆ = 生物）", "Type prefab name to filter (◆ = creature)");

			_cEsp = Ui.GC("鱼群雷达", "Fish radar");
			_cEspNames = Ui.GC("雷达名字", "Radar names");
			_cEspWorth = Ui.GC("雷达显示价值", "Radar worth");
			_cItemEsp = Ui.GC("物品雷达", "Item radar");
			_cPerf = Ui.GC("性能监视", "Perf overlay");
			_cAmmoHud = Ui.GC("弹药显示", "Ammo HUD");

			_cDiffEasy = Ui.GC("简单", "Easy");
			_cDiffDefault = Ui.GC("默认", "Default");
			_cDiffHard = Ui.GC("困难", "Hard");
			_cFF = H(Ui.GC("友方伤害", "Friendly fire"), host);
			_cFree = H(Ui.GC("免费购物", "Free shopping"), host);
			_cFullRestore = H(Ui.GC("满状态（血量/饱食/毒/火）", "Full restore"), host);

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
				arr[i] = Ui.GC(zh + " ×" + values[i], en + " ×" + values[i]);
			}
			return arr;
		}

		internal static void ToggleMenu()
		{
			_menuOpen = !_menuOpen;
			if (_menuOpen)
			{
				EnsureSpawnList();
			}
		}

		/// <summary>窗口开启时按数字键 1-6 直达对应页签。</summary>
		internal static void HandleNumberKeys()
		{
			if (!_menuOpen)
			{
				return;
			}
			for (int i = 0; i < 6; i++)
			{
				if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
				{
					_tab = i;
				}
			}
		}

		internal static void OnGUI()
		{
			Ui.EnsureStyles();
			if (!_menuOpen)
			{
				return;
			}
			Ui.CardFrameReset(_tab);
			Ui.HardShadow(_winRect, 7f * Ui.Scale);
			_winRect = GUI.Window(WinId, _winRect, WindowFunc, GUIContent.none, Ui.Panel);
		}

		private static void DrawWindow(int id)
		{
			float s = Ui.Scale;
			float w = _winRect.width;

			// ---- 页眉：logo + 标题 + 主机徽章 + 关闭 ----
			Rect logoR = new Rect(14f * s, 10f * s, 44f * s, 44f * s);
			Ui.HardShadow(logoR, 3f * s);
			GUI.DrawTexture(logoR, Ui.TagTex(0));
			GUI.Label(logoR, _logo, Ui.Tag);
			Rect titleR = new Rect(66f * s, 0f, w - 290f * s, 60f * s);
			GUI.Label(titleR, Ui.GC("鱼力全开 · 作弊菜单", "How to Fish · Cheat Menu"), Ui.Title);

			if (!_hostStatusBuilt)
			{
				_hostNow = CheatCore.IsServer;
				_hostText = Ui.UseZh ? (_hostNow ? "★ 主机" : "客户端") : (_hostNow ? "★ HOST" : "CLIENT");
				_hostStatusBuilt = true;
			}
			float chipH = 26f * s;
			Rect hostR = new Rect(w - 150f * s, (60f * s - chipH) * 0.5f, 88f * s, chipH);
			Ui.HardShadow(hostR, 2.5f * s);
			GUI.Box(hostR, GUIContent.none, Ui.Chip);
			Ui.Dot(new Rect(hostR.x + 9f * s, hostR.y + (hostR.height - 7f * s) * 0.5f, 7f * s, 7f * s), _hostNow ? Ui.ColMint : Ui.ColOff);
			GUI.Label(new Rect(hostR.x + 20f * s, hostR.y, hostR.width - 22f * s, hostR.height), _hostText, Ui.Chip);
			Rect closeR = new Rect(w - 42f * s, (60f * s - 28f * s) * 0.5f, 28f * s, 28f * s);
			Ui.HardShadow(closeR, 2.5f * s);
			if (GUI.Button(closeR, _close, Ui.Tab))
			{
				ToggleMenu();
				return;
			}

			// ---- 芯片页签 ----
			Rect chips = new Rect(Ui.Pad * s, 66f * s, w - Ui.Pad * 2f * s, 36f * s);
			float cw = (chips.width - 5f * 8f * s) / 6f;
			for (int i = 0; i < 6; i++)
			{
				Rect r = new Rect(chips.x + i * (cw + 8f * s), chips.y, cw, chips.height);
				Ui.HardShadow(r, 2.5f * s);
				if (GUI.Button(r, _tabs[i], _tab == i ? Ui.TabActive : Ui.Tab))
				{
					_tab = i;
				}
			}

			// ---- 内容滚动 ----
			float top = chips.y + chips.height + 14f * s;
			Rect view = new Rect(Ui.Pad * s, top, w - Ui.Pad * 2f * s, _winRect.height - top - 14f * s);
			float contentH = Mathf.Max(_pageHeights[_tab], view.height);
			Vector2 scroll = GUI.BeginScrollView(view, _scrolls[_tab], new Rect(0f, 0f, view.width - 14f, contentH));
			Ui.BeginArea(0f, 6f * s, view.width - 14f);
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

			GUI.DragWindow(new Rect(0f, 0f, w, 60f * s));
		}

		// ================= 玩家 =================
		private static void DrawPlayer()
		{
			bool host = CheatCore.IsServer;

			if (Ui.Ghost(_cCheats))
			{
				CheatCore.EnableCheats();
				CheatCore.Say(Msg.Done);
			}
			Ui.Next();

			Ui.CardBegin(_pSurvival);
			if (Ui.ToggleRow(_cGod, CheatCore.GodOn, host)) { CheatCore.ToggleGod(); }
			Ui.Next();
			if (Ui.ToggleRow(_cOneShot, CheatCore.OneShotOn, host)) { CheatCore.ToggleOneShot(); }
			Ui.Next();
			if (Ui.ToggleRow(_cHunger, CheatCore.HungerFreeze, host)) { CheatCore.HungerFreeze = !CheatCore.HungerFreeze; }
			Ui.CardEnd();

			Ui.CardBegin(_pWeapon);
			if (Ui.ToggleRow(_cAmmo, CheatCore.InfiniteAmmo))
			{
				CheatCore.InfiniteAmmo = !CheatCore.InfiniteAmmo;
				CheatCore.Say(CheatCore.InfiniteAmmo ? Msg.AmmoOn : Msg.AmmoOff);
			}
			Ui.Next();
			if (Ui.ToggleRow(_cNoRecoil, CheatCore.NoRecoil)) { CheatCore.NoRecoil = !CheatCore.NoRecoil; }
			Ui.Next();
			if (Ui.ToggleRow(_cRapid, CheatCore.RapidFire)) { CheatCore.RapidFire = !CheatCore.RapidFire; }
			Ui.Next();
			if (Ui.Button(_cSpeed[Plugin.SpeedIndex])) { Plugin.CycleSpeed(); }
			Ui.Next();
			if (Ui.Button(_cJump[Plugin.JumpIndex])) { Plugin.CycleJump(); }
			Ui.CardEnd();

			Ui.CardBegin(_pMoney);
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
			if (!ReferenceEquals(edited, _moneyInput)) { _moneyInput = edited; }
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
			Ui.CardEnd();
		}

		// ================= 世界 =================
		private static void DrawWorld()
		{
			bool host = CheatCore.IsServer;

			Ui.CardBegin(_pTp);
			Rect half1 = Ui.Slice(0f, 0.49f);
			Rect half2 = Ui.Slice(0.51f, 0.49f);
			if (GUI.Button(half1, _cTpPrev, Ui.BtnGhost)) { CheatCore.TpIsland(true); }
			if (GUI.Button(half2, _cTpNext, Ui.BtnGhost)) { CheatCore.TpIsland(false); }
			Ui.Next();
			if (Ui.Ghost(_cTpMini, host)) { CheatCore.TpRandomMini(); }
			Ui.Next();

			if (_mainIslands == null || _mainIslands.Length == 0) { BuildMainIslands(); }
			if (_islandSel < 0 && _mainIslands != null && _mainIslands.Length > 0) { _islandSel = 0; }
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
				if (Ui.Primary(_cTpGo, host) && host) { CheatCore.TpIsland(_mainIslands[sel]); }
			}
			Ui.CardEnd();

			Ui.CardBegin(_pCreature);
			Rect c1 = Ui.Slice(0f, 0.49f);
			Rect c2 = Ui.Slice(0.51f, 0.49f);
			if (GUI.Button(c1, _cKillBoss, host ? Ui.BtnGhost : Ui.BtnDisabled)) { CheatCore.KillBoss(); }
			if (GUI.Button(c2, _cKillAlive, host ? Ui.BtnGhost : Ui.BtnDisabled))
			{
				CheatCore.Say(CheatCore.KillAllAliveCreatures() ? Msg.Done : Msg.NeedHost);
			}
			Ui.Next();
			Rect c3 = Ui.Slice(0f, 0.49f);
			Rect c4 = Ui.Slice(0.51f, 0.49f);
			if (GUI.Button(c3, _cClearJournal, host ? Ui.BtnGhost : Ui.BtnDisabled))
			{
				CheatCore.SetAllCreaturesKilled(false, false);
				CheatCore.Say(Msg.Done);
			}
			if (GUI.Button(c4, _cJournalComplete, host ? Ui.BtnGhost : Ui.BtnDisabled)) { CheatCore.CompleteJournal(); }
			Ui.CardEnd();

			Ui.CardBegin(_pSpawner);
			string edited = GUI.TextField(Ui.Slice(0f, 0.58f), _search, Ui.Field);
			if (!ReferenceEquals(edited, _search)) { _search = edited; FilterSpawnList(); }
			Rect dripR = Ui.Slice(0.60f, 0.19f);
			Rect deadR = Ui.Slice(0.80f, 0.20f);
			if (GUI.Button(dripR, _cDrip, _spawnDrip ? Ui.TabActive : Ui.Tab)) { _spawnDrip = !_spawnDrip; }
			if (GUI.Button(deadR, _cDead, _spawnDead ? Ui.TabActive : Ui.Tab)) { _spawnDead = !_spawnDead; }
			Ui.Next();
			Ui.LabelRow(_cSearchHint, Ui.Small);
			Ui.Next();
			Ui.Row(26f * Ui.Scale);
			int count = _viewLabels.Length;
			for (int i = 0; i < count; i++)
			{
				if (GUI.Button(Ui.Slice(0f, 1f), _viewLabels[i], Ui.Btn) && host)
				{
					CheatCore.SpawnPrefab(_viewItems[i], _spawnDrip, _spawnDead);
				}
				Ui.Next();
			}
			Ui.CardEnd();
		}

		private static void BuildMainIslands()
		{
			try
			{
				int total = IslandManager.TotalIslands;
				if (total <= 0) { _mainIslands = new byte[0]; return; }
				List<byte> list = new List<byte>(total);
				for (byte i = 0; i < total; i++)
				{
					if (IslandManager.IsMainIsland(i)) { list.Add(i); }
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
			bool host = CheatCore.IsServer;
			Ui.CardBegin(_pCasino);
			if (Ui.ToggleRow(_cRoulette, CheatCore.RouletteWin, host)) { CheatCore.RouletteWin = !CheatCore.RouletteWin; }
			Ui.Next();
			if (Ui.ToggleRow(_cSlots, CheatCore.SlotsLegendary, host))
			{
				CheatCore.SlotsLegendary = !CheatCore.SlotsLegendary;
				CheatCore.ApplySlotsCheat();
			}
			Ui.Next();
			Ui.LabelRow(_cCasinoHint, Ui.Small);
			Ui.CardEnd();
		}

		// ================= 钓鱼 =================
		private static void DrawFishing()
		{
			Ui.CardBegin(_pFishing);
			if (Ui.ToggleRow(_cBite, CheatCore.InstantBite))
			{
				CheatCore.InstantBite = !CheatCore.InstantBite;
				CheatCore.Say(CheatCore.InstantBite ? Msg.BiteOn : Msg.BiteOff);
			}
			Ui.Next();
			if (Ui.ToggleRow(_cAutoReel, CheatCore.AutoReel)) { CheatCore.AutoReel = !CheatCore.AutoReel; }
			Ui.Next();
			if (Ui.ToggleRow(_cWeight, CheatCore.MaxWeight, CheatCore.IsServer)) { CheatCore.MaxWeight = !CheatCore.MaxWeight; }
			Ui.Next();
			Ui.LabelRow(_cReelHint, Ui.Small);
			Ui.Next();
			Ui.LabelRow(_cWeightHint, Ui.Small);
			Ui.Next();
			Ui.LabelRow(_cBiteHint, Ui.Small);
			Ui.CardEnd();
		}

		// ================= 显示 =================
		private static void DrawDisplay()
		{
			Ui.CardBegin(_pOverlay);
			if (Ui.ToggleRow(_cEsp, Overlays.EspEnabled)) { Overlays.ToggleEsp(); }
			Ui.Next();
			if (Ui.ToggleRow(_cEspNames, Plugin.EspShowNames.Value)) { Plugin.EspShowNames.Value = !Plugin.EspShowNames.Value; }
			Ui.Next();
			if (Ui.ToggleRow(_cEspWorth, Plugin.EspShowWorth.Value)) { Plugin.EspShowWorth.Value = !Plugin.EspShowWorth.Value; }
			Ui.Next();
			if (Ui.ToggleRow(_cItemEsp, Plugin.ItemEspEnabled.Value)) { Plugin.ItemEspEnabled.Value = !Plugin.ItemEspEnabled.Value; }
			Ui.Next();
			if (Ui.ToggleRow(_cAmmoHud, Plugin.AmmoHudEnabled.Value)) { Plugin.AmmoHudEnabled.Value = !Plugin.AmmoHudEnabled.Value; }
			Ui.Next();
			if (Ui.ToggleRow(_cPerf, Overlays.PerfEnabled)) { Overlays.TogglePerf(); }
			Ui.CardEnd();

			Ui.CardBegin(_pScale);
			Ui.Row(24f);
			Rect sliderR = Ui.Slice(0.02f, 0.66f);
			float v = GUI.HorizontalSlider(sliderR, Ui.Scale, 0.75f, 1.5f);
			if (Mathf.Abs(Ui.Scale - _scaleTextValue) > 0.005f)
			{
				_scaleTextValue = Ui.Scale;
				_scaleText = Ui.Scale.ToString("F2") + "×";
			}
			GUI.Label(Ui.Slice(0.72f, 0.26f), _scaleText, Ui.Value);
			if (Mathf.Abs(v - Ui.Scale) > 0.0005f)
			{
				Ui.Scale = Mathf.Clamp(v, 0.75f, 1.5f);
				Ui.InvalidateStyles();
				Plugin.SaveUiScale(Ui.Scale);
			}
			Ui.CardEnd();
		}

		private static float _scaleTextValue = -1f;
		private static string _scaleText = "1.00×";

		// ================= 杂项 =================
		private static void DrawMisc()
		{
			bool host = CheatCore.IsServer;

			Ui.CardBegin(_pRules);
			Rect d1 = Ui.Slice(0f, 0.32f);
			Rect d2 = Ui.Slice(0.34f, 0.32f);
			Rect d3 = Ui.Slice(0.68f, 0.32f);
			int diff = CheatCore.GetDifficulty();
			if (GUI.Button(d1, _cDiffEasy, diff == 0 ? Ui.TabActive : Ui.Tab)) { CheatCore.SetDifficulty(0); }
			if (GUI.Button(d2, _cDiffDefault, diff == 1 ? Ui.TabActive : Ui.Tab)) { CheatCore.SetDifficulty(1); }
			if (GUI.Button(d3, _cDiffHard, diff == 2 ? Ui.TabActive : Ui.Tab)) { CheatCore.SetDifficulty(2); }
			Ui.Next();
			bool ff = ServerSettings.Instance && ServerSettings.UseFriendlyFire;
			if (Ui.ToggleRow(_cFF, ff, host)) { CheatCore.ToggleFriendlyFire(!ff); }
			Ui.Next();
			if (Ui.ToggleRow(_cFree, CheatCore.FreeShopping, host)) { CheatCore.FreeShopping = !CheatCore.FreeShopping; }
			Ui.Next();
			if (Ui.Button(_cSell[Plugin.SellIndex], host) && host) { Plugin.CycleSell(); }
			Ui.Next();
			if (Ui.Ghost(_cFullRestore, host)) { CheatCore.FullRestore(); }
			Ui.CardEnd();

			Ui.CardBegin(_pProgress);
			Rect p1 = Ui.Slice(0f, 0.49f);
			Rect p2 = Ui.Slice(0.51f, 0.49f);
			if (GUI.Button(p1, _cSkins, host ? Ui.BtnGhost : Ui.BtnDisabled)) { CheatCore.UnlockAllSkins(); }
			if (GUI.Button(p2, _cAchUnlock, host ? Ui.BtnGhost : Ui.BtnDisabled)) { CheatCore.UnlockAchievements(); }
			Ui.Next();
			Rect p3 = Ui.Slice(0f, 0.49f);
			Rect p4 = Ui.Slice(0.51f, 0.49f);
			if (GUI.Button(p3, _cAchLock, host ? Ui.BtnGhost : Ui.BtnDisabled)) { CheatCore.LockAchievements(); }
			if (GUI.Button(p4, _cFinish, host ? Ui.BtnGhost : Ui.BtnDisabled)) { CheatCore.FinishGame(); }
			Ui.CardEnd();
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

			Dictionary<string, int> nameCounts = new Dictionary<string, int>(names.Length);
			for (int i = 0; i < names.Length; i++)
			{
				if (nameCounts.TryGetValue(names[i], out int c)) { nameCounts[names[i]] = c + 1; }
				else { nameCounts[names[i]] = 1; }
			}
			Dictionary<string, int> suffixUsed = new Dictionary<string, int>(names.Length);
			for (int i = 0; i < names.Length; i++)
			{
				Item it = items[i];
				if (it && nameCounts[names[i]] > 1)
				{
					string prefab = StripClone(it.name);
					if (suffixUsed.TryGetValue(names[i], out int used)) { suffixUsed[names[i]] = used + 1; }
					else { suffixUsed[names[i]] = 1; }
					names[i] = names[i] + " (" + prefab + "#" + suffixUsed[names[i]] + ")";
				}
			}
			Array.Sort(names, items, StringComparer.Ordinal);

			_allItems = items;
			_allIsCreature = creatures;
			_allLabels = new GUIContent[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
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

		private static string LocalizedName(Item it)
		{
			try
			{
				string n = it.GetName();
				if (!string.IsNullOrEmpty(n)) { return n; }
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
