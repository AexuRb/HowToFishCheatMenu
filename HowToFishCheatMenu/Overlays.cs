using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>ESP 与性能监视共享的标签内容与矩形，全部复用，零分配。</summary>
	internal static class Overlays
	{
		// 注意：Rect/Content 是 struct，需要就地复用，因此不能声明为 readonly
		private static GUIContent Content = new GUIContent();
		private static GUIContent EmptyContent = new GUIContent();
		private static Rect Rect = new Rect();
		private static readonly Dictionary<int, CreatureLabel> Labels = new Dictionary<int, CreatureLabel>(64);

		private static readonly FieldInfo AliveField = AccessTools.Field(typeof(CreatureManager), "_aliveCreatures");

		private static readonly Color ColNormal = new Color(0.85f, 0.95f, 1f, 0.95f);
		private static readonly Color ColDrip = new Color(1f, 0.78f, 0.1f, 1f);
		private static readonly Color ColBoss = new Color(1f, 0.25f, 0.2f, 1f);
		private static readonly Color ColItem = new Color(0.3f, 0.9f, 1f, 0.9f);

		private static readonly Color ColAmmoOk = new Color(1f, 1f, 1f, 1f);
		private static readonly Color ColAmmoLow = new Color(1f, 0.76f, 0.15f, 1f);
		private static readonly Color ColAmmoEmpty = new Color(1f, 0.32f, 0.2f, 1f);

		private static bool _esp;
		private static bool _perf;

		// ---- 物品雷达：每 2 秒刷新一次场景物品快照 ----
		private const float ItemRefreshInterval = 2f;
		private static float _itemNextRefresh;
		private static Item[] _itemSnapshot = new Item[0];

		private static float _accum;
		private static int _frames;
		private static string _perfText = string.Empty;

		private static int _lastAmmo = -1;
		private static int _lastMag = -1;
		private static string _ammoText = string.Empty;

		/// <summary>每只生物的名字与价值文本缓存；价值变化时才重建字符串。</summary>
		private sealed class CreatureLabel
		{
			internal string Name;
			internal int LastWorth = int.MinValue;
			internal string WorthText = string.Empty;
		}

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
			if (Plugin.ItemEspEnabled.Value)
			{
				DrawItemEsp();
			}
			if (Plugin.AmmoHudEnabled.Value)
			{
				DrawAmmoHud();
			}
			if (_perf && !string.IsNullOrEmpty(_perfText))
			{
				DrawShadowLabel(_perfText, 8f, 8f, 400f, 20f, MenuLabelStyle);
			}
		}

		internal static GUIStyle MenuLabelStyle => Ui.Label;

		internal static GUIStyle HudStyle => Ui.Value;

		internal static GUIStyle BoxStyle => Ui.Box;

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
			GUI.Box(Rect, EmptyContent, BoxStyle);
			GUI.color = ammo == 0 ? ColAmmoEmpty : (mag > 0 && ammo * 3 <= mag ? ColAmmoLow : ColAmmoOk);
			Rect.Set(Screen.width - 238f, Screen.height - 104f, 222f, 54f);
			Content.text = _ammoText;
			GUI.Label(Rect, Content, HudStyle);
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
			bool showWorth = Plugin.EspShowWorth.Value;
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

				if (showNames || showWorth)
				{
					CreatureLabel lbl = GetLabel(c);
					if (lbl != null)
					{
						string text;
						if (showWorth && showNames)
						{
							text = lbl.Name + "  " + lbl.WorthText;
						}
						else if (showWorth)
						{
							text = lbl.WorthText;
						}
						else
						{
							text = lbl.Name;
						}
						if (text != null)
						{
							Content.text = text;
							GUI.color = showWorth && !showNames ? ColDrip : Color.white;
							Rect.x = sx + 8f;
							Rect.y = sy - 9f;
							Rect.width = 280f;
							Rect.height = 18f;
							GUI.Label(Rect, Content, MenuLabelStyle);
						}
					}
				}
			}
			GUI.color = Color.white;
		}

		/// <summary>地面物品雷达：跳过生物（鱼群雷达负责）与已被持有的物品。</summary>
		private static void DrawItemEsp()
		{
			Camera cam = GameInfo.CurCamera;
			if (!cam)
			{
				return;
			}
			if (Time.unscaledTime >= _itemNextRefresh)
			{
				_itemNextRefresh = Time.unscaledTime + ItemRefreshInterval;
				// 打字/刷新期分配可接受：低频快照（无参 FindObjectsByType 不排序，最快）
				_itemSnapshot = UnityEngine.Object.FindObjectsByType<Item>();
			}
			float screenH = Screen.height;
			float screenW = Screen.width;
			Item[] items = _itemSnapshot;
			for (int i = 0; i < items.Length; i++)
			{
				Item it = items[i];
				if (!it || it.Creature || !it.gameObject.activeInHierarchy)
				{
					continue;
				}
				if (it.SyncedHolder)
				{
					continue;
				}
				Vector3 v = cam.WorldToScreenPoint(it.transform.position);
				if (v.z <= 0f)
				{
					continue;
				}
				float sx = v.x;
				float sy = screenH - v.y;
				if (sx < 0f || sy < 0f || sx > screenW || sy > screenH)
				{
					continue;
				}
				GUI.color = ColItem;
				Rect.Set(sx - 2.5f, sy - 2.5f, 5f, 5f);
				GUI.DrawTexture(Rect, Texture2D.whiteTexture);
			}
			GUI.color = Color.white;
		}

		private static CreatureLabel GetLabel(Creature c)
		{
			// Mono 运行期内 GetInstanceID 恒定可用
#pragma warning disable CS0618
			int id = c.GetInstanceID();
#pragma warning restore CS0618
			if (!Labels.TryGetValue(id, out CreatureLabel lbl))
			{
				if (Labels.Count > 1024)
				{
					Labels.Clear();
				}
				lbl = new CreatureLabel
				{
					Name = LocalizedCreatureName(c)
				};
				Labels[id] = lbl;
			}
			if (Plugin.EspShowWorth.Value)
			{
				int worth = c.TotalWorth;
				if (worth != lbl.LastWorth)
				{
					lbl.LastWorth = worth;
					lbl.WorthText = (c.Currency == Currency.Euro ? "€" : "$") + worth;
				}
			}
			return lbl;
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
