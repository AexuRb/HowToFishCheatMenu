using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>
	/// UI 5.0 "Candy"（H 糖果街机 · GUILayout 版）：
	/// 单一糖果窗口，GUILayout 自动布局——行高/卡高由布局引擎计算，不会重叠；
	/// 缩放经 GUI.matrix 统一生效。卡片 = 阴影外层组 + 白卡内层组，标签牌骑上沿。
	/// </summary>
	internal static class MenuUI
	{
		private const int WinId = 0x48544633;
		private static bool _menuOpen;
		private static int _tab;
		private static float _winX = 60f, _winY = 60f;
		private static Vector2 _scroll;
		private static readonly int[] _tabOnCount = new int[6];

		// ---- 金钱输入 ----
		private static string _moneyInput = "10000";

		// ---- 生成器 ----
		private static bool _listBuilt;
		private static Item[] _allItems = new Item[0];
		private static GUIContent[] _allLabels = new GUIContent[0];
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

		// ---- 标签牌 ----
		private static readonly GUIContent[] _plateContent = new GUIContent[12];
		private static readonly int[] _plateColor = new int[12];
		private static readonly Dictionary<int, float> _plateW = new Dictionary<int, float>();
		private const int
			_pSurvival = 0, _pWeapon = 1, _pMoney = 2, _pTp = 3, _pCreature = 4, _pSpawner = 5,
			_pCasino = 6, _pFishing = 7, _pOverlay = 8, _pScale = 9, _pRules = 10, _pProgress = 11;

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

			Plate(_pSurvival, "生存", 3);
			Plate(_pWeapon, "武器", 1);
			Plate(_pMoney, "金钱", 0);
			Plate(_pTp, "传送", 1);
			Plate(_pCreature, "生物", 4);
			Plate(_pSpawner, "生成器", 2);
			Plate(_pCasino, "赌场", 2);
			Plate(_pFishing, "钓鱼辅助", 3);
			Plate(_pOverlay, "显示", 1);
			Plate(_pScale, "界面缩放", 0);
			Plate(_pRules, "服务器规则", 1);
			Plate(_pProgress, "进度", 0);

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

		private static void Plate(int idx, string text, int colorIdx)
		{
			_plateContent[idx] = new GUIContent(text);
			_plateColor[idx] = colorIdx;
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
			// 统一缩放：设计坐标 1:1 书写，矩阵负责放大；IMGUI 会自动逆变换鼠标命中
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Ui.Scale, Ui.Scale, 1f));
			float dw = Screen.width / Ui.Scale;
			float dh = Screen.height / Ui.Scale;
			Rect win = new Rect(Mathf.Clamp(_winX, 8f, Mathf.Max(8f, dw - 508f)),
				Mathf.Clamp(_winY, 8f, Mathf.Max(8f, dh - 200f)), 500f, 690f);
			GUILayout.Window(WinId, win, DrawWindow, GUIContent.none, Ui.WinPanel);
			GUI.matrix = Matrix4x4.identity;
		}

		private static void DrawWindow(int id)
		{
			// ---- 页眉 ----
			GUILayout.BeginHorizontal();
			GUILayout.Space(6f);
			Rect logoR = GUILayoutUtility.GetRect(44f, 44f, GUILayout.Width(44f), GUILayout.Height(44f));
			Ui.DrawShadowed(logoR, Ui.PlateTex(0));
			GUI.Label(logoR, _logo, Ui.TagLabel);
			GUILayout.Space(8f);
			GUILayout.Label(Ui.GC("鱼力全开 · 作弊菜单", "How to Fish · Cheat Menu"), Ui.DocklessTitle, GUILayout.Height(44f));
			GUILayout.FlexibleSpace();
			DrawHostChip();
			Rect closeR = GUILayoutUtility.GetRect(30f, 30f, GUILayout.Width(30f), GUILayout.Height(30f));
			Ui.DrawShadowed(closeR, Ui.PlateTex(4));
			if (GUI.Button(closeR, _close, Ui.TagLabel))
			{
				ToggleMenu();
				GUIUtility.ExitGUI();
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(10f);

			// ---- 芯片页签 ----
			GUILayout.BeginHorizontal();
			float chipW = (500f - 32f - 5f * 8f) / 6f;
			for (int i = 0; i < 6; i++)
			{
				Rect r = GUILayoutUtility.GetRect(chipW, 36f, GUILayout.Width(chipW), GUILayout.Height(36f));
				if (GUI.Button(r, _tabs[i], _tab == i ? Ui.TabActive : Ui.Tab))
				{
					_tab = i;
				}
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(8f);

			// ---- 内容 ----
			_scroll = GUILayout.BeginScrollView(_scroll, false, false, GUI.skin.horizontalScrollbar,
				GUI.skin.verticalScrollbar, GUIStyle.none);
			switch (_tab)
			{
				case 0: DrawPlayer(); break;
				case 1: DrawWorld(); break;
				case 2: DrawCasino(); break;
				case 3: DrawFishing(); break;
				case 4: DrawDisplay(); break;
				default: DrawMisc(); break;
			}
			GUILayout.EndScrollView();

			GUI.DragWindow(new Rect(0f, 0f, 10000f, 52f));
		}

		private static void DrawHostChip()
		{
			if (!_hostStatusBuilt)
			{
				_hostNow = CheatCore.IsServer;
				_hostText = Ui.UseZh ? (_hostNow ? "★ 主机" : "客户端") : (_hostNow ? "★ HOST" : "CLIENT");
				_hostStatusBuilt = true;
			}
			Rect chipR = GUILayoutUtility.GetRect(92f, 30f, GUILayout.Width(92f), GUILayout.Height(30f));
			GUI.Box(chipR, GUIContent.none, Ui.Chip);
			Ui.Dot(new Rect(chipR.x + 9f, chipR.y + (chipR.height - 8f) * 0.5f, 8f, 8f), _hostNow ? Ui.ColMint : Ui.ColOff);
			GUI.Label(new Rect(chipR.x + 22f, chipR.y, chipR.width - 24f, chipR.height), _hostText, Ui.Chip);
		}

		// ---- 卡片骨架：阴影外层组 + 白卡内层组 + 骑边标签牌 ----
		private static void CardBegin(int plateIdx)
		{
			GUILayout.BeginVertical(Ui.CardShadow);
			GUILayout.BeginVertical(Ui.CardInner);
			GUILayout.Space(14f);
		}

		private static void CardEnd(int plateIdx)
		{
			GUILayout.EndVertical();
			GUILayout.EndVertical();
			if (Event.current.type == EventType.Repaint)
			{
				Rect outer = GUILayoutUtility.GetLastRect();
				if (outer.height > 10f)
				{
					float pw = PlateWidth(plateIdx);
					Rect plate = new Rect(outer.x + 14f, outer.y - 12f, pw, 26f);
					Ui.DrawShadowed(plate, Ui.PlateTex(_plateColor[plateIdx]));
					GUI.Label(plate, _plateContent[plateIdx], Ui.TagLabel);
				}
			}
		}

		private static float PlateWidth(int plateIdx)
		{
			if (!_plateW.TryGetValue(plateIdx, out float w))
			{
				w = Ui.TagLabel.CalcSize(_plateContent[plateIdx]).x + 30f;
				_plateW[plateIdx] = w;
			}
			return w;
		}

		// ---- 开关行 ----
		private static bool ToggleRow(GUIContent label, bool on, bool interactable = true)
		{
			Rect row = GUILayoutUtility.GetRect(0f, 37f, GUILayout.ExpandWidth(true));
			Event e = Event.current;
			bool hover = interactable && e.type == EventType.Repaint && row.Contains(e.mousePosition);
			if (hover)
			{
				Ui.DrawRowHover(row);
			}
			GUI.Label(new Rect(row.x + 8f, row.y, row.width - 74f, row.height), label, interactable ? Ui.Label : Ui.Dim);
			Rect sw = new Rect(row.xMax - 60f, row.y + (row.height - 22f) * 0.5f, 50f, 22f);
			Ui.DrawSwitch(sw, on);
			return interactable && GUI.Button(row, GUIContent.none, GUIStyle.none);
		}

		// ---- 分段控件行 ----
		private static void SegRow(GUIContent a, GUIContent b, bool aActive, Action setA, Action setB)
		{
			GUILayout.BeginHorizontal();
			Rect r1 = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
			Rect r2 = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
			if (GUI.Button(r1, a, aActive ? Ui.TabActive : Ui.Tab)) { setA(); }
			if (GUI.Button(r2, b, !aActive ? Ui.TabActive : Ui.Tab)) { setB(); }
			GUILayout.EndHorizontal();
		}

		private static void SegRow3(GUIContent a, GUIContent b, GUIContent c, int active, Action<int> set)
		{
			GUILayout.BeginHorizontal();
			GUIContent[] cs = { a, b, c };
			for (int i = 0; i < 3; i++)
			{
				Rect r = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
				if (GUI.Button(r, cs[i], active == i ? Ui.TabActive : Ui.Tab))
				{
					set(i);
				}
			}
			GUILayout.EndHorizontal();
		}

		private static void ActionRow2(GUIContent l, GUIContent r, bool en, Action al, Action ar)
		{
			GUILayout.BeginHorizontal();
			Rect r1 = GUILayoutUtility.GetRect(0f, 37f, GUILayout.ExpandWidth(true));
			Rect r2 = GUILayoutUtility.GetRect(0f, 37f, GUILayout.ExpandWidth(true));
			if (GUI.Button(r1, l, en ? Ui.BtnGhost : Ui.BtnDisabled) && en) { al(); }
			if (GUI.Button(r2, r, en ? Ui.BtnGhost : Ui.BtnDisabled) && en) { ar(); }
			GUILayout.EndHorizontal();
		}

		// ================= 玩家 =================
		private static void DrawPlayer()
		{
			bool host = CheatCore.IsServer;

			if (GUILayout.Button(_cCheats, Ui.BtnGhost, GUILayout.Height(38f)))
			{
				CheatCore.EnableCheats();
				CheatCore.Say(Msg.Done);
			}
			GUILayout.Space(6f);

			CardBegin(_pSurvival);
			if (ToggleRow(_cGod, CheatCore.GodOn, host)) { CheatCore.ToggleGod(); }
			if (ToggleRow(_cOneShot, CheatCore.OneShotOn, host)) { CheatCore.ToggleOneShot(); }
			if (ToggleRow(_cHunger, CheatCore.HungerFreeze, host)) { CheatCore.HungerFreeze = !CheatCore.HungerFreeze; }
			CardEnd(_pSurvival);

			CardBegin(_pWeapon);
			if (ToggleRow(_cAmmo, CheatCore.InfiniteAmmo))
			{
				CheatCore.InfiniteAmmo = !CheatCore.InfiniteAmmo;
				CheatCore.Say(CheatCore.InfiniteAmmo ? Msg.AmmoOn : Msg.AmmoOff);
			}
			if (ToggleRow(_cNoRecoil, CheatCore.NoRecoil)) { CheatCore.NoRecoil = !CheatCore.NoRecoil; }
			if (ToggleRow(_cRapid, CheatCore.RapidFire)) { CheatCore.RapidFire = !CheatCore.RapidFire; }
			if (GUILayout.Button(_cSpeed[Plugin.SpeedIndex], Ui.BtnGhost, GUILayout.Height(37f))) { Plugin.CycleSpeed(); }
			if (GUILayout.Button(_cJump[Plugin.JumpIndex], Ui.BtnGhost, GUILayout.Height(37f))) { Plugin.CycleJump(); }
			CardEnd(_pWeapon);

			CardBegin(_pMoney);
			SegRow(_cDollar, _cEuro, CheatCore.MoneyCurrency == Currency.Dollar,
				() => CheatCore.MoneyCurrency = Currency.Dollar,
				() => CheatCore.MoneyCurrency = Currency.Euro);
			GUILayout.BeginHorizontal();
			_moneyInput = GUILayout.TextField(_moneyInput, Ui.Field, GUILayout.Height(40f));
			if (GUILayout.Button(_cAddMoney, Ui.BtnPrimary, GUILayout.Height(40f), GUILayout.Width(96f)) && host)
			{
				if (int.TryParse(_moneyInput, out int amt)) { CheatCore.AddMoney(amt); }
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(6f);
			if (GUILayout.Button(_cSetMoney, Ui.BtnGhost, GUILayout.Height(37f)) && host)
			{
				if (int.TryParse(_moneyInput, out int set)) { CheatCore.SetMoney(set); }
			}
			CardEnd(_pMoney);
		}

		// ================= 世界 =================
		private static void DrawWorld()
		{
			bool host = CheatCore.IsServer;

			CardBegin(_pTp);
			ActionRow2(_cTpPrev, _cTpNext, true, () => CheatCore.TpIsland(true), () => CheatCore.TpIsland(false));
			if (GUILayout.Button(_cTpMini, Ui.BtnGhost, GUILayout.Height(37f)) && host) { CheatCore.TpRandomMini(); }
			GUILayout.Space(4f);
			GUILayout.BeginHorizontal();
			Rect minusR = GUILayoutUtility.GetRect(0f, 36f, GUILayout.Width(52f));
			Rect labelR = GUILayoutUtility.GetRect(0f, 36f, GUILayout.ExpandWidth(true));
			Rect plusR = GUILayoutUtility.GetRect(0f, 36f, GUILayout.Width(52f));
			Rect goR = GUILayoutUtility.GetRect(0f, 36f, GUILayout.ExpandWidth(true));
			if (_mainIslands == null || _mainIslands.Length == 0) { BuildMainIslands(); }
			if (_islandSel < 0 && _mainIslands != null && _mainIslands.Length > 0) { _islandSel = 0; }
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
			GUILayout.EndHorizontal();
			CardEnd(_pTp);

			CardBegin(_pCreature);
			ActionRow2(_cKillBoss, _cKillAlive, host, CheatCore.KillBoss, () =>
			{
				CheatCore.Say(CheatCore.KillAllAliveCreatures() ? Msg.Done : Msg.NeedHost);
			});
			ActionRow2(_cClearJournal, _cJournalComplete, host,
				() => { CheatCore.SetAllCreaturesKilled(false, false); CheatCore.Say(Msg.Done); },
				CheatCore.CompleteJournal);
			CardEnd(_pCreature);

			CardBegin(_pSpawner);
			GUILayout.BeginHorizontal();
			_search = GUILayout.TextField(_search, Ui.Field, GUILayout.Height(38f));
			Rect dripR = GUILayoutUtility.GetRect(52f, 38f, GUILayout.Width(52f));
			Rect deadR = GUILayoutUtility.GetRect(52f, 38f, GUILayout.Width(52f));
			if (GUI.Button(dripR, _cDrip, _spawnDrip ? Ui.TabActive : Ui.Tab)) { _spawnDrip = !_spawnDrip; }
			if (GUI.Button(deadR, _cDead, _spawnDead ? Ui.TabActive : Ui.Tab)) { _spawnDead = !_spawnDead; }
			GUILayout.EndHorizontal();
			GUILayout.Label(_cSearchHint, Ui.Small);
			GUILayout.Space(4f);
			int count = _viewLabels.Length;
			for (int i = 0; i < count; i++)
			{
				if (GUILayout.Button(_viewLabels[i], Ui.ListBtn, GUILayout.Height(30f)) && host)
				{
					CheatCore.SpawnPrefab(_viewItems[i], _spawnDrip, _spawnDead);
				}
			}
			CardEnd(_pSpawner);
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
			CardBegin(_pCasino);
			if (ToggleRow(_cRoulette, CheatCore.RouletteWin, host)) { CheatCore.RouletteWin = !CheatCore.RouletteWin; }
			if (ToggleRow(_cSlots, CheatCore.SlotsLegendary, host))
			{
				CheatCore.SlotsLegendary = !CheatCore.SlotsLegendary;
				CheatCore.ApplySlotsCheat();
			}
			GUILayout.Label(_cCasinoHint, Ui.Small);
			CardEnd(_pCasino);
		}

		// ================= 钓鱼 =================
		private static void DrawFishing()
		{
			CardBegin(_pFishing);
			if (ToggleRow(_cBite, CheatCore.InstantBite))
			{
				CheatCore.InstantBite = !CheatCore.InstantBite;
				CheatCore.Say(CheatCore.InstantBite ? Msg.BiteOn : Msg.BiteOff);
			}
			if (ToggleRow(_cAutoReel, CheatCore.AutoReel)) { CheatCore.AutoReel = !CheatCore.AutoReel; }
			if (ToggleRow(_cWeight, CheatCore.MaxWeight, CheatCore.IsServer)) { CheatCore.MaxWeight = !CheatCore.MaxWeight; }
			GUILayout.Label(_cReelHint, Ui.Small);
			GUILayout.Label(_cWeightHint, Ui.Small);
			GUILayout.Label(_cBiteHint, Ui.Small);
			CardEnd(_pFishing);
		}

		// ================= 显示 =================
		private static void DrawDisplay()
		{
			CardBegin(_pOverlay);
			if (ToggleRow(_cEsp, Overlays.EspEnabled)) { Overlays.ToggleEsp(); }
			if (ToggleRow(_cEspNames, Plugin.EspShowNames.Value)) { Plugin.EspShowNames.Value = !Plugin.EspShowNames.Value; }
			if (ToggleRow(_cEspWorth, Plugin.EspShowWorth.Value)) { Plugin.EspShowWorth.Value = !Plugin.EspShowWorth.Value; }
			if (ToggleRow(_cItemEsp, Plugin.ItemEspEnabled.Value)) { Plugin.ItemEspEnabled.Value = !Plugin.ItemEspEnabled.Value; }
			if (ToggleRow(_cAmmoHud, Plugin.AmmoHudEnabled.Value)) { Plugin.AmmoHudEnabled.Value = !Plugin.AmmoHudEnabled.Value; }
			if (ToggleRow(_cPerf, Overlays.PerfEnabled)) { Overlays.TogglePerf(); }
			CardEnd(_pOverlay);

			CardBegin(_pScale);
			GUILayout.BeginHorizontal();
			GUILayout.Label(_cScaleLabel, Ui.Label, GUILayout.Width(90f));
			float v = GUILayout.HorizontalSlider(Ui.Scale, 0.75f, 1.5f);
			GUILayout.Label(_scaleText, Ui.Value, GUILayout.Width(64f));
			GUILayout.EndHorizontal();
			if (Mathf.Abs(v - Ui.Scale) > 0.0005f)
			{
				Ui.Scale = Mathf.Clamp(v, 0.75f, 1.5f);
				Plugin.SaveUiScale(Ui.Scale);
			}
			CardEnd(_pScale);
		}

		private static string _scaleText = "1.00×";

		// ================= 杂项 =================
		private static void DrawMisc()
		{
			bool host = CheatCore.IsServer;

			CardBegin(_pRules);
			SegRow3(_cDiffEasy, _cDiffDefault, _cDiffHard, CheatCore.GetDifficulty(), CheatCore.SetDifficulty);
			bool ff = ServerSettings.Instance && ServerSettings.UseFriendlyFire;
			if (ToggleRow(_cFF, ff, host)) { CheatCore.ToggleFriendlyFire(!ff); }
			if (ToggleRow(_cFree, CheatCore.FreeShopping, host)) { CheatCore.FreeShopping = !CheatCore.FreeShopping; }
			if (GUILayout.Button(_cSell[Plugin.SellIndex], Ui.BtnGhost, GUILayout.Height(37f)) && host) { Plugin.CycleSell(); }
			if (GUILayout.Button(_cFullRestore, Ui.BtnGhost, GUILayout.Height(37f)) && host) { CheatCore.FullRestore(); }
			CardEnd(_pRules);

			CardBegin(_pProgress);
			ActionRow2(_cSkins, _cAchUnlock, host, CheatCore.UnlockAllSkins, CheatCore.UnlockAchievements);
			ActionRow2(_cAchLock, _cFinish, host, CheatCore.LockAchievements, CheatCore.FinishGame);
			CardEnd(_pProgress);
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
