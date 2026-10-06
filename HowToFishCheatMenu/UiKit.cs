using System;
using System.Collections.Generic;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>
	/// UI 5.0 "Candy" — GUILayout 自动布局版（H 糖果街机语言）。
	/// - 布局交给 GUILayout：行高/卡高自动计算，结构上不会重叠
	/// - 缩放用 GUI.matrix 统一处理：所有控件（含默认滑条/滚动条）一致缩放
	/// - 厚墨水描边 + 硬阴影烘焙进纹理（九宫格切片保持描边/阴影厚度）
	/// - 所有样式从零构建并显式设置字体（空字体导致 CalcSize NRE 的教训）
	/// </summary>
	internal static class Ui
	{
		// ---- 糖果设计令牌 ----
		internal static readonly Color ColCream = new Color(1f, 0.98f, 0.94f, 1f);
		internal static readonly Color ColCreamDim = new Color(0.965f, 0.935f, 0.87f, 1f);
		internal static readonly Color ColInk = new Color(0.23f, 0.17f, 0.125f, 1f);
		internal static readonly Color ColMuted = new Color(0.54f, 0.47f, 0.40f, 1f);
		internal static readonly Color ColCoral = new Color(1f, 0.48f, 0.35f, 1f);
		internal static readonly Color ColMint = new Color(0.24f, 0.81f, 0.65f, 1f);
		internal static readonly Color ColBanana = new Color(1f, 0.79f, 0.24f, 1f);
		internal static readonly Color ColSky = new Color(0.36f, 0.72f, 1f, 1f);
		internal static readonly Color ColLilac = new Color(0.725f, 0.545f, 1f, 1f);
		internal static readonly Color ColWhite = new Color(1f, 1f, 1f, 1f);
		internal static readonly Color ColOverlayText = new Color(0.93f, 0.96f, 0.98f, 1f);
		internal static readonly Color ColOverlayGold = new Color(1f, 0.79f, 0.28f, 1f);
		internal const string HostTagHex = "a257e6";

		internal static Color ColOn => ColMint;
		internal static Color ColOff => new Color(0.55f, 0.60f, 0.55f, 1f);
		internal static Color ColGold => ColCoral;

		/// <summary>UI 缩放（0.75–1.5）。通过 GUI.matrix 统一生效。</summary>
		internal static float Scale = 1f;

		// ---- 样式 ----
		private static bool _ready;
		internal static GUIStyle WinPanel;
		internal static GUIStyle CardShadow;   // 外层：墨色底 + 右下内边距 = 硬偏移阴影
		internal static GUIStyle CardInner;    // 内层：白底 + 墨描边卡片
		internal static GUIStyle DocklessTitle;
		internal static GUIStyle Chip;         // 主机徽章
		internal static GUIStyle Tab;          // 芯片页签（白）
		internal static GUIStyle TabActive;    // 芯片页签（珊瑚）
		internal static GUIStyle BtnPrimary;   // 珊瑚主按钮
		internal static GUIStyle BtnGhost;     // 白幽灵按钮
		internal static GUIStyle BtnDisabled;
		internal static GUIStyle Label;        // 墨色粗体
		internal static GUIStyle Small;
		internal static GUIStyle Dim;
		internal static GUIStyle Value;        // 珊瑚数值
		internal static GUIStyle Field;
		internal static GUIStyle ListBtn;      // 生成器列表行
		internal static GUIStyle OverlayLabel;
		internal static GUIStyle OverlaySmall;
		internal static GUIStyle OverlayValue;
		internal static GUIStyle OverlayBox;

		private static Texture2D _texWinPanel;
		private static Texture2D _texShadowPad;
		private static Texture2D _texCardWhite;
		private static Texture2D _texChip;
		private static Texture2D _texTabWhite;
		private static Texture2D _texTabCoral;
		private static Texture2D _texBtnCoral;
		private static Texture2D _texBtnWhite;
		private static Texture2D _texBtnDisabled;
		private static Texture2D _texSwitchOn;
		private static Texture2D _texSwitchOff;
		private static Texture2D _texKnob;
		private static Texture2D _texField;
		private static Texture2D _texRowHover;
		private static Texture2D _texKnobSmall;
		private static Texture2D _texWhite;
		private static Texture2D _texSolid;
		private static Texture2D[] _texPlate;     // 6 色标签牌（烘焙硬阴影）
		private static Texture2D _texLogo;        // 香蕉圆徽（烘焙硬阴影）

		internal static Font UiFont;
		internal static bool UseZh;

		internal static bool Ready => _ready;

		internal static void InitFont()
		{
			if (UiFont == null)
			{
				UiFont = TryFont("Microsoft YaHei UI") ?? TryFont("Microsoft YaHei") ?? TryFont("SimHei");
				UseZh = UiFont != null;
			}
		}

		internal static void EnsureStyles()
		{
			if (_ready)
			{
				return;
			}
			InitFont();

			// 游戏自定义皮肤的 scrollView 样式带污染背景，清空（游戏自身零 IMGUI 使用）
			GUIStyle sv = GUI.skin.scrollView;
			sv.normal.background = null;
			sv.hover.background = null;
			sv.active.background = null;
			sv.focused.background = null;
			sv.onNormal.background = null;
			sv.onHover.background = null;
			sv.onActive.background = null;
			sv.onFocused.background = null;

			_texWinPanel = BakedShadow(ColCream, 20, 3);
			_texCardWhite = BakedShadow(ColWhite, 16, 3);
			_texShadowPad = MakeSolid(new Color(0.23f, 0.17f, 0.125f, 0.28f));
			_texChip = BakedShadow(ColCreamDim, 32, 2);
			_texTabWhite = BakedShadow(ColWhite, 32, 2);
			_texTabCoral = BakedShadow(ColCoral, 32, 2);
			_texBtnCoral = BakedShadow(ColCoral, 16, 2);
			_texBtnWhite = BakedShadow(ColWhite, 16, 2);
			_texBtnDisabled = BakedShadow(new Color(0.93f, 0.90f, 0.84f, 1f), 16, 2);
			_texSwitchOn = BakedShadow(ColMint, 32, 2);
			_texSwitchOff = BakedShadow(new Color(0.91f, 0.87f, 0.79f, 1f), 32, 2);
			_texKnob = RoundedShape(ColWhite, 32, ColInk, 2, false, 0f);
			_texKnobSmall = RoundedShape(ColWhite, 32, null, 0, false, 0f);
			_texField = BakedShadow(ColWhite, 12, 2);
			_texRowHover = MakeSolid(new Color(1f, 0.79f, 0.24f, 0.16f));
			_texPlate = new Texture2D[6];
			Color[] plateCols = { ColBanana, ColSky, ColLilac, ColMint, ColCoral, new Color(1f, 0.93f, 0.78f) };
			for (int i = 0; i < 6; i++)
			{
				_texPlate[i] = BakedShadow(plateCols[i], 32, 2);
			}
			_texLogo = BakedShadow(ColBanana, 24, 2);
			_texWhite = Texture2D.whiteTexture;

			// ---- 样式 ----
			WinPanel = Base(_texWinPanel, 13, ColInk, TextAnchor.UpperLeft, 24);
			WinPanel.padding = new RectOffset(16, 16, 14, 14);

			// 卡片 = 外层阴影组（右下 6px 内边距露出墨色）+ 内层白卡
			CardShadow = Base(_texShadowPad, 13, ColInk, TextAnchor.UpperLeft);
			CardShadow.padding = new RectOffset(0, 6, 0, 6);
			CardShadow.margin = new RectOffset(0, 0, 0, 14);
			CardInner = Base(_texCardWhite, 13, ColInk, TextAnchor.UpperLeft, 18);
			CardInner.padding = new RectOffset(14, 14, 22, 12);

			DocklessTitle = Base(null, 17, ColInk, TextAnchor.MiddleLeft);
			DocklessTitle.fontStyle = FontStyle.Bold;

			Chip = Base(_texChip, 12, ColInk, TextAnchor.MiddleCenter, 30);
			Chip.fontStyle = FontStyle.Bold;

			Tab = Base(_texTabWhite, 13, ColInk, TextAnchor.MiddleCenter, 30);
			Tab.fontStyle = FontStyle.Bold;
			TabActive = Base(_texTabCoral, 13, ColWhite, TextAnchor.MiddleCenter, 30);
			TabActive.fontStyle = FontStyle.Bold;

			BtnPrimary = Base(_texBtnCoral, 13, ColWhite, TextAnchor.MiddleCenter, 18);
			BtnPrimary.fontStyle = FontStyle.Bold;
			BtnGhost = Base(_texBtnWhite, 13, ColInk, TextAnchor.MiddleCenter, 18);
			BtnGhost.fontStyle = FontStyle.Bold;
			BtnDisabled = Base(_texBtnDisabled, 13, new Color(0.54f, 0.47f, 0.40f, 0.6f), TextAnchor.MiddleCenter, 18);

			Label = Base(null, 13, ColInk, TextAnchor.MiddleLeft);
			Label.fontStyle = FontStyle.Bold;
			Label.richText = true;
			Small = Base(null, 11, ColMuted, TextAnchor.MiddleLeft);
			Small.richText = true;
			Dim = Base(null, 12, ColMuted, TextAnchor.MiddleLeft);
			Dim.richText = true;
			Value = Base(null, 15, ColCoral, TextAnchor.MiddleRight);
			Value.fontStyle = FontStyle.Bold;

			Field = Base(_texField, 14, ColInk, TextAnchor.MiddleLeft, 14);
			Field.padding.left = 12;
			Field.fontStyle = FontStyle.Bold;

			ListBtn = Base(null, 13, ColInk, TextAnchor.MiddleLeft);
			ListBtn.richText = true;
			ListBtn.fontStyle = FontStyle.Bold;
			ListBtn.hover.background = _texRowHover;
			ListBtn.padding.left = 8;

			// 游戏内 overlay（深色画面上的浅色文字）
			OverlayLabel = Base(null, 12, ColOverlayText, TextAnchor.MiddleLeft);
			OverlayLabel.richText = true;
			OverlaySmall = Base(null, 11, ColOverlayText, TextAnchor.MiddleLeft);
			OverlayValue = Base(null, 30, ColOverlayGold, TextAnchor.MiddleRight);
			OverlayValue.fontStyle = FontStyle.Bold;
			OverlayBox = new GUIStyle();
			OverlayBox.normal.background = MakeSolid(new Color(0f, 0f, 0f, 0.55f));

			_ready = true;
		}

		private static Texture2D MakeSolid(Color c)
		{
			if (_texSolid == null)
			{
				_texSolid = new Texture2D(4, 4, TextureFormat.ARGB32, false);
				Color[] px = new Color[16];
				for (int i = 0; i < 16; i++)
				{
					px[i] = Color.white;
				}
				_texSolid.SetPixels(px);
				_texSolid.Apply(false, false);
				_texSolid.name = "ui_solid";
			}
			GUI.color = c;
			return _texSolid;
		}

		internal static void ClearTint()
		{
			GUI.color = Color.white;
		}

		private static GUIStyle Base(Texture2D bg, int fontSize, Color color, TextAnchor anchor, int radius = 0)
		{
			GUIStyle s = new GUIStyle();
			if (bg != null)
			{
				s.normal.background = bg;
				s.hover.background = bg;
				s.active.background = bg;
				s.focused.background = bg;
			}
			if (UiFont != null)
			{
				s.font = UiFont;
			}
			s.fontSize = fontSize;
			s.normal.textColor = color;
			s.hover.textColor = color;
			s.active.textColor = color;
			s.focused.textColor = color;
			s.alignment = anchor;
			s.wordWrap = false;
			// 九宫格边框：烘焙阴影/描边的厚度在拉伸时保持不变
			if (radius > 0)
			{
				s.border = new RectOffset(radius, radius, radius, radius);
				s.overflow = new RectOffset(0, 0, 0, 0);
			}
			return s;
		}

		/// <summary>
		/// 带烘焙硬阴影的形状纹理：96×96 画布，本体 (0,0)-(80,80)，
		/// 墨色阴影偏移 (8,8)。九宫格 border=28 保持描边/阴影厚度。
		/// </summary>
		private static Texture2D BakedShadow(Color fill, int radius, int borderWidth)
		{
			int size = 96;
			int shape = 80;
			int off = 8;
			Texture2D t = new Texture2D(size, size, TextureFormat.ARGB32, false);
			t.name = "ui_baked";
			Color[] px = new Color[size * size];
			float r = radius;
			float inkA = 0.3f;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float px_ = x + 0.5f;
					float py_ = y + 0.5f;
					// 本体有符号距离（内部为正）
					float sx = Mathf.Min(px_, shape - px_);
					float sy = Mathf.Min(py_, shape - py_);
					float sBody;
					bool cornerBody = sx < r && sy < r;
					if (cornerBody)
					{
						float dx = r - sx;
						float dy = r - sy;
						sBody = r - Mathf.Sqrt(dx * dx + dy * dy);
					}
					else
					{
						sBody = Mathf.Min(sx, sy);
					}
					// 阴影有符号距离（偏移副本）
					float ox = px_ - off;
					float oy = py_ - off;
					float sx2 = Mathf.Min(ox, shape - ox);
					float sy2 = Mathf.Min(oy, shape - oy);
					float sShadow;
					bool cornerShadow = sx2 < r && sy2 < r;
					if (cornerShadow)
					{
						float dx = r - sx2;
						float dy = r - sy2;
						sShadow = r - Mathf.Sqrt(dx * dx + dy * dy);
					}
					else
					{
						sShadow = Mathf.Min(sx2, sy2);
					}
					Color c = Color.clear;
					if (sShadow > 0f && !(sBody > 0f))
					{
						c = new Color(0.23f, 0.17f, 0.125f, inkA * Mathf.Clamp01(sShadow + 0.5f));
					}
					if (sBody > 0f)
					{
						float a = fill.a * Mathf.Clamp01(sBody + 0.5f);
						c = fill;
						c.a = a;
						if (borderWidth > 0 && sBody <= borderWidth + 0.5f)
						{
							c = new Color(ColInk.r, ColInk.g, ColInk.b, Mathf.Max(ColInk.a, a));
						}
					}
					px[y * size + x] = c;
				}
			}
			t.SetPixels(px);
			t.Apply(false, false);
			t.wrapMode = TextureWrapMode.Clamp;
			return t;
		}

		/// <summary>圆角形状纹理（radius=32 胶囊/圆形）。</summary>
		private static Texture2D RoundedShape(Color fill, int radius, Color? outline, int outlineWidth, bool topOnly, float edgeFade)
		{
			int size = 64;
			Texture2D t = new Texture2D(size, size, TextureFormat.ARGB32, false);
			t.name = "ui_shape";
			Color[] px = new Color[size * size];
			float r = radius;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float cx = Mathf.Min(x + 0.5f, size - x - 0.5f);
					float cyTop = y + 0.5f;
					float cyBot = size - y - 0.5f;
					float sEdge = Mathf.Min(cx, Mathf.Min(cyTop, cyBot));
					float s;
					bool corner = cx < r && (topOnly ? cyTop < r : (cyTop < r || cyBot < r));
					if (corner)
					{
						float cornerCenterY = (topOnly || cyTop <= cyBot) ? cyTop : size - cyTop;
						float dy = r - cornerCenterY;
						float dx = r - cx;
						s = r - Mathf.Sqrt(dx * dx + dy * dy);
					}
					else
					{
						s = topOnly ? Mathf.Min(cx, cyTop) : sEdge;
					}
					float a = fill.a;
					if (s <= 0f)
					{
						a = 0f;
					}
					else
					{
						a *= Mathf.Clamp01(s + 0.5f);
						if (edgeFade > 0f)
						{
							a *= Mathf.Clamp01(s / edgeFade);
						}
					}
					Color c = fill;
					if (outline.HasValue && outlineWidth > 0 && s > 0f && s <= outlineWidth + 0.5f)
					{
						Color o = outline.Value;
						c = new Color(o.r, o.g, o.b, Mathf.Max(o.a, a));
					}
					c.a = a;
					px[y * size + x] = c;
				}
			}
			t.SetPixels(px);
			t.Apply(false, false);
			t.wrapMode = TextureWrapMode.Clamp;
			return t;
		}

		/// <summary>在 GUILayout 预留的矩形里绘制糖果开关。</summary>
		internal static void DrawSwitch(Rect r, bool on)
		{
			GUI.DrawTexture(r, on ? _texSwitchOn : _texSwitchOff);
			float k = r.height - 6f;
			Rect knob = new Rect(on ? r.xMax - k - 3f : r.x + 3f, r.y + (r.height - k) * 0.5f, k, k);
			GUI.DrawTexture(knob, _texKnob);
		}

		/// <summary>在 GUILayout 预留的矩形里绘制带阴影的纹理（logo/徽章/标签牌）。</summary>
		internal static void DrawShadowed(Rect r, Texture2D tex)
		{
			Color old = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, 0.9f);
			Rect sh = new Rect(r.x + 3f, r.y + 3f, r.width, r.height);
			GUI.DrawTexture(sh, _texKnobSmall);
			GUI.color = new Color(0.23f, 0.17f, 0.125f, 0.28f);
			GUI.DrawTexture(sh, _texKnobSmall);
			GUI.color = old;
			GUI.DrawTexture(r, tex);
		}

		/// <summary>开关行悬停底色。</summary>
		internal static void DrawRowHover(Rect r)
		{
			GUI.DrawTexture(r, _texRowHover);
		}

		/// <summary>标签牌文字样式。</summary>
		internal static GUIStyle TagLabel
		{
			get
			{
				if (_tagLabel == null)
				{
					_tagLabel = Base(null, 13, ColInk, TextAnchor.MiddleCenter);
					_tagLabel.fontStyle = FontStyle.Bold;
				}
				return _tagLabel;
			}
		}
		private static GUIStyle _tagLabel;

		/// <summary>小圆点。</summary>
		internal static void Dot(Rect r, Color color)
		{
			Color old = GUI.color;
			GUI.color = color;
			GUI.DrawTexture(r, _texKnobSmall);
			GUI.color = old;
		}

		internal static void DrawWhite(Rect r)
		{
			GUI.DrawTexture(r, _texWhite);
		}

		/// <summary>主机角标富文本后缀（初始化期拼接一次）。</summary>
		internal static string HostSuffix => UseZh
			? " <color=#" + HostTagHex + "><size=10>[主机]</size></color>"
			: " <color=#" + HostTagHex + "><size=10>[HOST]</size></color>";

		/// <summary>标签牌纹理（按配色序号）。</summary>
		internal static Texture2D PlateTex(int idx)
		{
			return _texPlate[idx];
		}

		internal static GUIContent GC(string zh, string en)
		{
			return new GUIContent(UseZh ? zh : en);
		}

		internal static GUIContent GC(string text)
		{
			return new GUIContent(text);
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
}
