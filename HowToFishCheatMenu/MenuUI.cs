using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>
	/// IMGUI 菜单。规避 GC 的关键点：
	/// - 所有 GUIContent / GUIStyle / GUI.WindowFunction 委托在初始化时创建并复用
	/// - 只用固定 Rect 的 GUI.*（不走 GUILayout 布局分配）
	/// - 标签全部为常量或预生成，运行期不拼字符串
	/// </summary>
	internal static class MenuUI
	{
		private const int WinId = 0x48544631;
		private const float Pad = 10f;
		private const float RowH = 30f;
		private const float RowGap = 6f;
		private const float SpawnRowH = 22f;

		private static bool _menuOpen;
		private static int _tab;
		private static Rect _winRect = new Rect(30f, 30f, 360f, 560f);
		private static Vector2 _scroll;

		private static readonly GUI.WindowFunction WindowFunc = DrawWindow;

		private static Plugin _plugin;
		private static ConfigEntry<int> _moneyAmount;

		// ---- 样式（首次 OnGUI 时创建一次） ----
		private static bool _stylesReady;
		private static GUIStyle _winStyle;
		private static GUIStyle _buttonStyle;
		private static GUIStyle _labelStyle;
		private static GUIStyle _boxStyle;
		private static GUIStyle _hudStyle;

		internal static Font UiFont;
		internal static bool UseZh;

		// ---- 标签缓存 ----
		private static GUIContent _title;
		private static GUIContent _tabPlayer;
		private static GUIContent _tabWorld;
		private static GUIContent _tabDisplay;
		private static GUIContent _tabMisc;
		private static GUIContent _lblSpawnHeader;
		private static GUIContent _footer;

		private static GUIContent _btnCheats;
		private static GUIContent _btnGodOn;
		private static GUIContent _btnGodOff;
		private static GUIContent _btnOneShotOn;
		private static GUIContent _btnOneShotOff;
		private static GUIContent _btnBiteOn;
		private static GUIContent _btnBiteOff;
		private static GUIContent _btnAmmoInfOn;
		private static GUIContent _btnAmmoInfOff;
		private static GUIContent[] _btnSpeed;
		private static GUIContent _btnMoney;
		private static GUIContent _btnTpNext;
		private static GUIContent _btnTpPrev;

		private static GUIContent _btnEspOn;
		private static GUIContent _btnEspOff;
		private static GUIContent _btnEspNamesOn;
		private static GUIContent _btnEspNamesOff;
		private static GUIContent _btnPerfOn;
		private static GUIContent _btnPerfOff;
		private static GUIContent _btnAmmoHudOn;
		private static GUIContent _btnAmmoHudOff;
		private static GUIContent _btnKillBoss;
		private static GUIContent _btnKillAlive;
		private static GUIContent _btnClearJournal;
		private static GUIContent _btnSkins;
		private static GUIContent _btnAch;
		private static GUIContent _btnFinish;

		private static GUIContent _lblBiteHint;

		// ---- 刷物品列表（首次打开菜单时构建一次） ----
		private static bool _listBuilt;
		private static Item[] _spawnItems;
		private static GUIContent[] _spawnLabels;

		internal static bool MenuOpen => _menuOpen;

		internal static GUIStyle LabelStyleForOverlay => _labelStyle;

		internal static void Init(Plugin plugin, ConfigEntry<int> moneyAmount)
		{
			_plugin = plugin;
			_moneyAmount = moneyAmount;

			UiFont = TryFont("Microsoft YaHei UI") ?? TryFont("Microsoft YaHei") ?? TryFont("SimHei");
			UseZh = UiFont != null;

			_title = GC("HTF 作弊菜单 v1.2", "HTF Cheat Menu v1.2");
			_tabPlayer = GC("玩家", "Player");
			_tabWorld = GC("世界", "World");
			_tabDisplay = GC("显示", "HUD");
			_tabMisc = GC("杂项", "Misc");
			_lblSpawnHeader = GC("刷物品（需要主机）— 点击生成到准星前", "Spawn items (host) — click to spawn in front");
			_lblBiteHint = GC("秒咬钩需鱼饵沉入水下，按原判定选鱼", "Instant bite: bait must sink underwater first");
			_footer = GC("F1 菜单  F2 上帝  F3 秒咬钩  F4 移速  F5 加钱  F6 雷达  F7 性能  F8 无限弹",
				"F1 menu  F2 god  F3 bite  F4 speed  F5 money  F6 esp  F7 perf  F8 ammo");

			_btnCheats = GC("内置作弊: 开", "Built-in cheats: ON");
			_btnGodOn = GC("上帝模式: 开", "God mode: ON");
			_btnGodOff = GC("上帝模式: 关", "God mode: OFF");
			_btnOneShotOn = GC("一击必杀: 开", "One shot: ON");
			_btnOneShotOff = GC("一击必杀: 关", "One shot: OFF");
			_btnBiteOn = GC("秒咬钩: 开", "Instant bite: ON");
			_btnBiteOff = GC("秒咬钩: 关", "Instant bite: OFF");
			_btnAmmoInfOn = GC("无限弹匣: 开", "Infinite mag: ON");
			_btnAmmoInfOff = GC("无限弹匣: 关", "Infinite mag: OFF");
			_btnMoney = GC("加钱 +" + moneyAmount.Value, "Add money +" + moneyAmount.Value);
			_btnTpNext = GC("下一岛 →", "Next island →");
			_btnTpPrev = GC("← 上一岛", "← Prev island");

			_btnEspOn = GC("鱼群雷达: 开", "Fish radar: ON");
			_btnEspOff = GC("鱼群雷达: 关", "Fish radar: OFF");
			_btnEspNamesOn = GC("雷达名字: 显示", "Radar names: ON");
			_btnEspNamesOff = GC("雷达名字: 隐藏", "Radar names: OFF");
			_btnPerfOn = GC("性能监视: 开", "Perf overlay: ON");
			_btnPerfOff = GC("性能监视: 关", "Perf overlay: OFF");
			_btnAmmoHudOn = GC("弹药显示: 开", "Ammo HUD: ON");
			_btnAmmoHudOff = GC("弹药显示: 关", "Ammo HUD: OFF");
			_btnKillBoss = GC("秒杀 Boss", "Kill boss");
			_btnKillAlive = GC("击杀全部活物（稍后自动补怪）", "Kill all alive (auto respawn)");
			_btnClearJournal = GC("清空图鉴击杀记录", "Clear journal records");
			_btnSkins = GC("解锁全部皮肤", "Unlock all skins");
			_btnAch = GC("解锁全部成就", "Unlock achievements");
			_btnFinish = GC("结束本局（存档结算）", "Finish game (end screen)");

			float[] speeds = Plugin.SpeedOptions;
			_btnSpeed = new GUIContent[speeds.Length];
			for (int i = 0; i < speeds.Length; i++)
			{
				_btnSpeed[i] = GC("移速倍率 x" + speeds[i], "Speed x" + speeds[i]);
			}

			float[] jumps = Plugin.JumpOptions;
			_btnJump = new GUIContent[jumps.Length];
			for (int i = 0; i < jumps.Length; i++)
			{
				_btnJump[i] = GC("跳跃倍率 x" + jumps[i], "Jump x" + jumps[i]);
			}
		}

		private static GUIContent[] _btnJump;

		internal static void ToggleMenu()
		{
			_menuOpen = !_menuOpen;
		}

		internal static void NotifySpeedChanged()
		{
		}

		internal static void NotifyJumpChanged()
		{
		}

		internal static void OnGUI()
		{
			EnsureStyles();
			if (_menuOpen)
			{
				if (!_listBuilt)
				{
					BuildSpawnList();
				}
				_winRect = GUI.Window(WinId, _winRect, WindowFunc, _title, _winStyle);
			}
		}

		private static void DrawWindow(int id)
		{
			Rect r = new Rect(Pad, 28f, _winRect.width - Pad * 2f, RowH);

			// Tab 行（4 等分）
			float tabW = (r.width - RowGap * 3f) * 0.25f;
			r.width = tabW;
			if (GUI.Button(r, _tabPlayer, _buttonStyle))
			{
				_tab = 0;
			}
			r.x += tabW + RowGap;
			if (GUI.Button(r, _tabWorld, _buttonStyle))
			{
				_tab = 1;
			}
			r.x += tabW + RowGap;
			if (GUI.Button(r, _tabDisplay, _buttonStyle))
			{
				_tab = 2;
			}
			r.x += tabW + RowGap;
			if (GUI.Button(r, _tabMisc, _buttonStyle))
			{
				_tab = 3;
			}
			r.y += RowH + RowGap;
			r.x = Pad;
			r.width = _winRect.width - Pad * 2f;

			switch (_tab)
			{
				case 0:
					DrawPlayerTab(ref r);
					break;
				case 1:
					DrawWorldTab(ref r);
					break;
				case 2:
					DrawDisplayTab(ref r);
					break;
				default:
					DrawMiscTab(ref r);
					break;
			}

			// 底部提示 + 拖动
			r.y = _winRect.height - 30f;
			r.height = 22f;
			GUI.Label(r, _footer, _labelStyle);
			GUI.DragWindow(new Rect(0f, 0f, _winRect.width, 24f));
		}

		// 玩家页：影响自身的作弊开关与数值
		private static void DrawPlayerTab(ref Rect r)
		{
			if (Btn(r, _btnCheats))
			{
				CheatCore.EnableCheats();
				CheatCore.Say(Msg.Done);
			}
			Next(ref r);
			if (Btn(r, CheatCore.GodOn ? _btnGodOn : _btnGodOff))
			{
				CheatCore.ToggleGod();
			}
			Next(ref r);
			if (Btn(r, CheatCore.OneShotOn ? _btnOneShotOn : _btnOneShotOff))
			{
				CheatCore.ToggleOneShot();
			}
			Next(ref r);
			if (Btn(r, CheatCore.InfiniteAmmo ? _btnAmmoInfOn : _btnAmmoInfOff))
			{
				CheatCore.InfiniteAmmo = !CheatCore.InfiniteAmmo;
				CheatCore.Say(CheatCore.InfiniteAmmo ? Msg.AmmoOn : Msg.AmmoOff);
			}
			Next(ref r);
			if (Btn(r, CheatCore.InstantBite ? _btnBiteOn : _btnBiteOff))
			{
				CheatCore.InstantBite = !CheatCore.InstantBite;
				CheatCore.Say(CheatCore.InstantBite ? Msg.BiteOn : Msg.BiteOff);
			}
			Next(ref r);
			if (Btn(r, _btnSpeed[Plugin.SpeedIndex]))
			{
				Plugin.CycleSpeed();
			}
			Next(ref r);
			if (Btn(r, _btnJump[Plugin.JumpIndex]))
			{
				Plugin.CycleJump();
			}
			Next(ref r);
			if (Btn(r, _btnMoney))
			{
				CheatCore.AddMoney(_moneyAmount.Value);
			}
			Next(ref r);

			r.height = 20f;
			GUI.Label(r, _lblBiteHint, _labelStyle);
		}

		// 世界页：传送、生物操作、刷物品
		private static void DrawWorldTab(ref Rect r)
		{
			float half = (r.width - RowGap) * 0.5f;
			float fullW = r.width;
			r.width = half;
			if (Btn(r, _btnTpPrev))
			{
				CheatCore.TpIsland(true);
			}
			r.x += half + RowGap;
			if (Btn(r, _btnTpNext))
			{
				CheatCore.TpIsland(false);
			}
			r.x = Pad;
			r.width = fullW;
			r.y += RowH + RowGap;

			if (Btn(r, _btnKillBoss))
			{
				CheatCore.KillBoss();
			}
			Next(ref r);
			if (Btn(r, _btnKillAlive))
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
			Next(ref r);
			if (Btn(r, _btnClearJournal))
			{
				CheatCore.SetAllCreaturesKilled(false, false);
				CheatCore.Say(Msg.Done);
			}
			Next(ref r);

			r.height = 22f;
			GUI.Label(r, _lblSpawnHeader, _labelStyle);
			r.y += 24f;
			float viewH = _winRect.height - r.y - 40f;
			if (viewH < 60f)
			{
				viewH = 60f;
			}
			r.height = viewH;

			int count = _spawnLabels != null ? _spawnLabels.Length : 0;
			float contentH = count * (SpawnRowH + 2f);
			_scroll = GUI.BeginScrollView(r, _scroll, new Rect(0f, 0f, r.width - 20f, contentH));
			Rect row = new Rect(0f, 0f, r.width - 24f, SpawnRowH);
			for (int i = 0; i < count; i++)
			{
				if (GUI.Button(row, _spawnLabels[i], _buttonStyle))
				{
					CheatCore.SpawnItem(_spawnItems[i]);
				}
				row.y += SpawnRowH + 2f;
			}
			GUI.EndScrollView();
		}

		// 显示页：屏幕覆盖层开关
		private static void DrawDisplayTab(ref Rect r)
		{
			if (Btn(r, Overlays.EspEnabled ? _btnEspOn : _btnEspOff))
			{
				Overlays.ToggleEsp();
			}
			Next(ref r);
			if (Btn(r, Plugin.EspShowNames.Value ? _btnEspNamesOn : _btnEspNamesOff))
			{
				Plugin.EspShowNames.Value = !Plugin.EspShowNames.Value;
			}
			Next(ref r);
			if (Btn(r, Plugin.AmmoHudEnabled.Value ? _btnAmmoHudOn : _btnAmmoHudOff))
			{
				Plugin.AmmoHudEnabled.Value = !Plugin.AmmoHudEnabled.Value;
			}
			Next(ref r);
			if (Btn(r, Overlays.PerfEnabled ? _btnPerfOn : _btnPerfOff))
			{
				Overlays.TogglePerf();
			}
		}

		// 杂项页：进度与结算
		private static void DrawMiscTab(ref Rect r)
		{
			if (Btn(r, _btnSkins))
			{
				CheatCore.UnlockAllSkins();
			}
			Next(ref r);
			if (Btn(r, _btnAch))
			{
				CheatCore.UnlockAchievements();
			}
			Next(ref r);
			if (Btn(r, _btnFinish))
			{
				CheatCore.FinishGame();
			}
		}

		private static bool Btn(Rect r, GUIContent content)
		{
			return GUI.Button(r, content, _buttonStyle);
		}

		private static void Next(ref Rect r)
		{
			r.y += RowH + RowGap;
		}

		private static void EnsureStyles()
		{
			if (_stylesReady)
			{
				return;
			}
			_winStyle = new GUIStyle(GUI.skin.window);
			_buttonStyle = new GUIStyle(GUI.skin.button);
			_labelStyle = new GUIStyle(GUI.skin.label);
			_boxStyle = new GUIStyle(GUI.skin.box);
			if (UiFont != null)
			{
				_winStyle.font = UiFont;
				_buttonStyle.font = UiFont;
				_labelStyle.font = UiFont;
				_boxStyle.font = UiFont;
			}
			_buttonStyle.fontSize = 14;
			_buttonStyle.alignment = TextAnchor.MiddleLeft;
			_buttonStyle.padding.left = 10;
			_labelStyle.fontSize = 12;
			_labelStyle.wordWrap = false;
			_hudStyle = new GUIStyle(_labelStyle);
			_hudStyle.fontSize = 30;
			_hudStyle.alignment = TextAnchor.MiddleRight;
			_stylesReady = true;
		}

		internal static GUIStyle HudStyleForOverlay => _hudStyle;

		internal static GUIStyle BoxStyleForOverlay => _boxStyle;

		private static void BuildSpawnList()
		{
			_listBuilt = true;
			Item[] items;
			try
			{
				items = Resources.LoadAll<Item>("Items");
			}
			catch (Exception)
			{
				_spawnItems = new Item[0];
				_spawnLabels = new GUIContent[0];
				return;
			}
			if (items == null || items.Length == 0)
			{
				_spawnItems = new Item[0];
				_spawnLabels = new GUIContent[0];
				return;
			}

			string[] names = new string[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				Item it = items[i];
				if (!it)
				{
					names[i] = "?" + i;
					continue;
				}
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

			_spawnItems = items;
			_spawnLabels = new GUIContent[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				_spawnLabels[i] = new GUIContent(names[i]);
			}
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

		private static GUIContent GC(string zh, string en)
		{
			return new GUIContent(UseZh ? zh : en);
		}

		private static Font TryFont(string name)
		{
			try
			{
				Font f = Font.CreateDynamicFontFromOSFont(name, 14);
				return f ? f : null;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}

	/// <summary>ESP 与性能监视共享的标签内容与矩形，全部复用，零分配。</summary>
	internal static class Overlays
	{
		// 注意：Rect/Content 是 struct，需要就地复用，因此不能声明为 readonly
		private static GUIContent Content = new GUIContent();
		private static GUIContent EmptyContent = new GUIContent();
		private static Rect Rect = new Rect();
		private static readonly Dictionary<int, string> Names = new Dictionary<int, string>();

		private static readonly FieldInfo AliveField = AccessTools.Field(typeof(CreatureManager), "_aliveCreatures");

		private static readonly Color ColNormal = new Color(0.85f, 0.95f, 1f, 0.95f);
		private static readonly Color ColDrip = new Color(1f, 0.78f, 0.1f, 1f);
		private static readonly Color ColBoss = new Color(1f, 0.25f, 0.2f, 1f);

		private static readonly Color ColAmmoOk = new Color(1f, 1f, 1f, 1f);
		private static readonly Color ColAmmoLow = new Color(1f, 0.76f, 0.15f, 1f);
		private static readonly Color ColAmmoEmpty = new Color(1f, 0.32f, 0.2f, 1f);

		private static bool _esp;
		private static bool _perf;

		private static float _accum;
		private static int _frames;
		private static string _perfText = string.Empty;

		private static int _lastAmmo = -1;
		private static int _lastMag = -1;
		private static string _ammoText = string.Empty;

		internal static bool EspEnabled => _esp;
		internal static bool PerfEnabled => _perf;

		internal static void Init(Plugin plugin)
		{
		}

		internal static void ToggleEsp()
		{
			_esp = !_esp;
		}

		internal static void TogglePerf()
		{
			_perf = !_perf;
			_perfText = string.Empty;
		}

		internal static void TickPerfAccumulator()
		{
			if (!_perf)
			{
				_accum = 0f;
				_frames = 0;
				return;
			}
			_accum += Time.unscaledDeltaTime;
			_frames++;
			if (_accum < 1f)
			{
				return;
			}
			float fps = _frames / _accum;
			long gcBytes = GC.GetTotalMemory(false);
			_perfText = "FPS " + fps.ToString("F0")
				+ "   GC " + (gcBytes >> 20) + "MB"
				+ "   GC0 " + GC.CollectionCount(0);
			_accum = 0f;
			_frames = 0;
		}

		internal static void OnGUI()
		{
			if (_esp)
			{
				DrawEsp();
			}
			if (Plugin.AmmoHudEnabled.Value)
			{
				DrawAmmoHud();
			}
			if (_perf && !string.IsNullOrEmpty(_perfText))
			{
				DrawShadowLabel(_perfText, 8f, 8f, 400f, 20f, MenuUI.LabelStyleForOverlay);
			}
		}

		/// <summary>手持远程武器时右下角显示 弹药/弹匣。文本只在数值变化时重建。</summary>
		private static void DrawAmmoHud()
		{
			Player p = Player.LocalPlayer;
			if (!p)
			{
				return;
			}
			Item held = p.Holding.HeldItem;
			if (!held)
			{
				return;
			}
			Weapon w = held.Weapon;
			if (!w)
			{
				return;
			}
			Attachments att = w.Attachments;
			if (!att)
			{
				return;
			}
			int ammo = w.Ammo;
			int mag = att.AmmoPerMag;
			if (ammo != _lastAmmo || mag != _lastMag)
			{
				_lastAmmo = ammo;
				_lastMag = mag;
				if (ammo == 0)
				{
					_ammoText = ammo + " / " + mag + "  [R]";
				}
				else
				{
					_ammoText = ammo + " / " + mag;
				}
			}

			// 底框 + 大字号 + 弹量变色：白(充足) 黄(≤30%) 红(空仓)
			GUI.color = new Color(0f, 0f, 0f, 0.55f);
			Rect.Set(Screen.width - 244f, Screen.height - 110f, 234f, 66f);
			GUI.Box(Rect, EmptyContent, MenuUI.BoxStyleForOverlay);
			GUI.color = ammo == 0 ? ColAmmoEmpty : (mag > 0 && ammo * 3 <= mag ? ColAmmoLow : ColAmmoOk);
			Rect.Set(Screen.width - 238f, Screen.height - 104f, 222f, 54f);
			Content.text = _ammoText;
			GUI.Label(Rect, Content, MenuUI.HudStyleForOverlay);
			GUI.color = Color.white;
		}

		/// <summary>带黑影的标签，共享 Rect/Content，零分配。</summary>
		private static void DrawShadowLabel(string text, float x, float y, float width, float height, GUIStyle style)
		{
			Content.text = text;
			GUI.color = Color.black;
			Rect.Set(x + 1f, y + 1f, width, height);
			GUI.Label(Rect, Content, style);
			GUI.color = Color.white;
			Rect.Set(x, y, width, height);
			GUI.Label(Rect, Content, style);
		}

		private static void DrawEsp()
		{
			Camera cam = GameInfo.CurCamera;
			if (!cam)
			{
				return;
			}
			CreatureManager cm = CreatureManager.Instance;
			if (!cm || AliveField == null)
			{
				return;
			}
			List<Creature> list = AliveField.GetValue(cm) as List<Creature>;
			if (list == null)
			{
				return;
			}

			bool showNames = Plugin.EspShowNames.Value;
			float screenH = Screen.height;
			float screenW = Screen.width;
			int count = list.Count;
			for (int i = 0; i < count; i++)
			{
				Creature c = list[i];
				if (!c)
				{
					continue;
				}
				Vector3 v = cam.WorldToScreenPoint(c.transform.position);
				if (v.z <= 0f)
				{
					continue;
				}
				float sx = v.x;
				float sy = screenH - v.y;
				if (sx < -40f || sy < -40f || sx > screenW + 40f || sy > screenH + 40f)
				{
					continue;
				}

				bool isBoss = c.BossType != BossType.None;
				GUI.color = isBoss ? ColBoss : (c.IsDrip ? ColDrip : ColNormal);
				Rect.x = sx - 3f;
				Rect.y = sy - 3f;
				Rect.width = 6f;
				Rect.height = 6f;
				GUI.DrawTexture(Rect, Texture2D.whiteTexture);

				if (showNames)
				{
					string name = GetName(c);
					if (name != null)
					{
						Content.text = name;
						Rect.x = sx + 8f;
						Rect.y = sy - 9f;
						Rect.width = 240f;
						Rect.height = 18f;
						GUI.Label(Rect, Content, MenuUI.LabelStyleForOverlay);
					}
				}
			}
			GUI.color = Color.white;
		}

		private static string GetName(Creature c)
		{
			// Mono 运行期内 GetInstanceID 恒定可用；GetEntityId 的 int 转换在 Unity 后续版本才会移除，与本游戏无关
#pragma warning disable CS0618
			int id = c.GetInstanceID();
#pragma warning restore CS0618
			string name;
			if (!Names.TryGetValue(id, out name))
			{
				if (Names.Count > 1024)
				{
					Names.Clear();
				}
				name = LocalizedCreatureName(c);
				Names[id] = name;
			}
			return name;
		}

		/// <summary>生物名走游戏本地化（Creature 继承 Item.GetName）；只缓存 miss 时调用一次。</summary>
		private static string LocalizedCreatureName(Creature c)
		{
			try
			{
				string n = c.GetName();
				if (!string.IsNullOrEmpty(n))
				{
					return n;
				}
			}
			catch (Exception)
			{
			}
			string fallback = c.name;
			if (fallback != null && fallback.Length > 7 && fallback.EndsWith("(Clone)", StringComparison.Ordinal))
			{
				return fallback.Substring(0, fallback.Length - 7);
			}
			return fallback;
		}
	}
}
