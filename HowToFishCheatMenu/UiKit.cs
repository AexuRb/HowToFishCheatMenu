using System;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>
	/// UI 4.0 "Candy Dock" 主题（E 坞式呼出 × H 糖果街机）：
	/// 底部糖果坞 + 浮出面板；厚墨水描边 + 硬阴影 + 奶油底色。
	/// 规避 GC：纹理/样式/GUIContent 一次构建复用；布局用静态游标推进。
	/// 所有样式从零构建，绝不继承 GUI.skin（游戏自定义皮肤状态会泄漏）。
	/// </summary>
	internal static class Ui
	{
		// ---- 糖果设计令牌 ----
		internal static readonly Color ColCream = new Color(1f, 0.98f, 0.94f, 1f);
		internal static readonly Color ColCreamDim = new Color(0.965f, 0.935f, 0.87f, 1f);
		internal static readonly Color ColInk = new Color(0.23f, 0.17f, 0.125f, 1f);
		internal static readonly Color ColInkSoft = new Color(0.23f, 0.17f, 0.125f, 0.55f);
		internal static readonly Color ColMuted = new Color(0.54f, 0.47f, 0.40f, 1f);
		internal static readonly Color ColCoral = new Color(1f, 0.48f, 0.35f, 1f);
		internal static readonly Color ColMint = new Color(0.24f, 0.81f, 0.65f, 1f);
		internal static readonly Color ColBanana = new Color(1f, 0.79f, 0.24f, 1f);
		internal static readonly Color ColSky = new Color(0.36f, 0.72f, 1f, 1f);
		internal static readonly Color ColLilac = new Color(0.725f, 0.545f, 1f, 1f);
		internal static readonly Color ColWhite = new Color(1f, 1f, 1f, 1f);

		// 游戏内 overlay（深色游戏画面上的浅色文字）用
		internal static readonly Color ColOverlayText = new Color(0.93f, 0.96f, 0.98f, 1f);
		internal static readonly Color ColOverlayGold = new Color(1f, 0.79f, 0.28f, 1f);
		internal const string HostTagHex = "a257e6";

		// 旧引用兼容
		internal static Color ColOn => ColMint;
		internal static Color ColOff => new Color(0.55f, 0.60f, 0.55f, 1f);
		internal static Color ColGold => ColCoral;
		internal static Color ColText => ColInk;
		internal static Color ColDim => ColMuted;

		// ---- 布局常量（乘以缩放） ----
		internal const float RowH = 32f;
		internal const float RowGap = 6f;
		internal const float Pad = 14f;
		internal const float TabH = 32f;

		internal static float Scale = 1f;

		// ---- 样式 ----
		private static bool _ready;
		internal static GUIStyle Panel;        // 奶油卡片
		internal static GUIStyle DockBg;       // 坞底
		internal static GUIStyle DockItem;     // 坞按钮（白）
		internal static GUIStyle DockItemOn;   // 坞按钮（珊瑚）
		internal static GUIStyle Tag;          // 标签牌（纹理按页配色）
		internal static GUIStyle Title;
		internal static GUIStyle Chip;         // 主机徽章
		internal static GUIStyle Tab;          // 分段控件（白胶囊）
		internal static GUIStyle TabActive;    // 分段控件（珊瑚胶囊）
		internal static GUIStyle Btn;          // 行按钮（白，左对齐）
		internal static GUIStyle BtnPrimary;   // 珊瑚主按钮（居中）
		internal static GUIStyle BtnGhost;     // 白幽灵按钮（居中）
		internal static GUIStyle BtnDanger;    // 珊瑚危险按钮
		internal static GUIStyle BtnDisabled;
		internal static GUIStyle Label;        // 墨色粗体行标签
		internal static GUIStyle Small;        // 弱化说明
		internal static GUIStyle Dim;
		internal static GUIStyle Section;      // 分节（墨色 + 珊瑚下划线）
		internal static GUIStyle Value;        // 数值（珊瑚）
		internal static GUIStyle Field;        // 输入框（白 + 墨描边）
		internal static GUIStyle Footer;
		internal static GUIStyle Box;
		// 游戏内 overlay 专用（浅色文字）
		internal static GUIStyle OverlayLabel;
		internal static GUIStyle OverlaySmall;
		internal static GUIStyle OverlayValue;
		internal static GUIStyle OverlayBox;

		internal static Font UiFont;
		internal static bool UseZh;

		private static Texture2D _texCard;
		private static Texture2D _texDock;
		private static Texture2D _texDockItem;
		private static Texture2D _texDockItemOn;
		private static Texture2D[] _texTags;
		private static Texture2D _texChip;
		private static Texture2D _texPillWhite;
		private static Texture2D _texPillCoral;
		private static Texture2D _texBtnWhite;
		private static Texture2D _texBtnCoral;
		private static Texture2D _texBtnMint;
		private static Texture2D _texBtnSky;
		private static Texture2D _texBtnDisabled;
		private static Texture2D _texSwitchOn;
		private static Texture2D _texSwitchOff;
		private static Texture2D _texKnob;
		private static Texture2D _texField;
		private static Texture2D _texRowHover;
		private static Texture2D _texShadowInk;
		private static Texture2D _texWhite;
		private static Texture2D _texSolid;

		private static readonly Color[] TagColors =
		{
			new Color(1f, 0.79f, 0.24f),    // 玩家 banana
			new Color(0.36f, 0.72f, 1f),    // 世界 sky
			new Color(0.725f, 0.545f, 1f),  // 赌场 lilac
			new Color(0.24f, 0.81f, 0.65f), // 钓鱼 mint
			new Color(1f, 0.48f, 0.35f),    // 显示 coral
			new Color(1f, 0.93f, 0.78f),    // 杂项 cream
		};

		internal static bool Ready => _ready;

		/// <summary>必须在 MenuUI.Init（构建 GUIContent）之前调用：UseZh 决定全部标签语言。</summary>
		internal static void InitFont()
		{
			if (UiFont == null)
			{
				UiFont = TryFont("Microsoft YaHei UI") ?? TryFont("Microsoft YaHei") ?? TryFont("SimHei");
				UseZh = UiFont != null;
			}
		}

		/// <summary>缩放变化后强制下一帧重建纹理与样式。</summary>
		internal static void InvalidateStyles()
		{
			if (!_ready)
			{
				return;
			}
			_ready = false;
			Texture2D[] tex = { _texCard, _texDock, _texDockItem, _texDockItemOn, _texChip, _texPillWhite, _texPillCoral, _texBtnWhite, _texBtnCoral, _texBtnMint, _texBtnSky, _texBtnDisabled, _texSwitchOn, _texSwitchOff, _texKnob, _texField, _texRowHover, _texShadowInk };
			for (int i = 0; i < tex.Length; i++)
			{
				if (tex[i] != null)
				{
					UnityEngine.Object.Destroy(tex[i]);
				}
			}
			if (_texTags != null)
			{
				for (int i = 0; i < _texTags.Length; i++)
				{
					if (_texTags[i] != null)
					{
						UnityEngine.Object.Destroy(_texTags[i]);
					}
				}
			}
			_texCard = null; _texDock = null; _texDockItem = null; _texDockItemOn = null; _texTags = null;
			_texChip = null; _texPillWhite = null; _texPillCoral = null; _texBtnWhite = null; _texBtnCoral = null;
			_texBtnMint = null; _texBtnSky = null; _texBtnDisabled = null; _texSwitchOn = null; _texSwitchOff = null;
			_texKnob = null; _texField = null; _texRowHover = null; _texShadowInk = null;
		}

		internal static void EnsureStyles()
		{
			if (_ready)
			{
				return;
			}
			InitFont();

			// 根因修复：游戏自定义皮肤的 scrollView 样式带白色 hover/focused 背景，
			// BeginScrollView 所有公开重载都强制用 skin.scrollView 当背景，
			// 悬停/点击滚动区就会盖出异色光斑。游戏自身完全没有 IMGUI，清空零副作用。
			GUIStyle sv = GUI.skin.scrollView;
			sv.normal.background = null;
			sv.hover.background = null;
			sv.active.background = null;
			sv.focused.background = null;
			sv.onNormal.background = null;
			sv.onHover.background = null;
			sv.onActive.background = null;
			sv.onFocused.background = null;

			int rad = (int)(18f * Scale);
			int bRad = (int)(14f * Scale);
			int pillRad = (int)(22f * Scale);
			int dockRad = (int)(22f * Scale);
			_texCard = RoundedShape(ColCream, rad, ColInk, 3, false, 0f);
			_texDock = RoundedShape(ColCream, dockRad, ColInk, 3, false, 0f);
			_texDockItem = RoundedShape(ColWhite, bRad, ColInk, 3, false, 0f);
			_texDockItemOn = RoundedShape(ColCoral, bRad, ColInk, 3, false, 0f);
			_texTags = new Texture2D[TagColors.Length];
			for (int i = 0; i < TagColors.Length; i++)
			{
				_texTags[i] = RoundedShape(TagColors[i], 32, ColInk, 3, false, 0f);
			}
			_texChip = RoundedShape(ColCreamDim, 32, ColInk, 3, false, 0f);
			_texPillWhite = RoundedShape(ColWhite, 32, ColInk, 3, false, 0f);
			_texPillCoral = RoundedShape(ColCoral, 32, ColInk, 3, false, 0f);
			_texBtnWhite = RoundedShape(ColWhite, bRad, ColInk, 3, false, 0f);
			_texBtnCoral = RoundedShape(ColCoral, bRad, ColInk, 3, false, 0f);
			_texBtnMint = RoundedShape(ColMint, bRad, ColInk, 3, false, 0f);
			_texBtnSky = RoundedShape(ColSky, bRad, ColInk, 3, false, 0f);
			_texBtnDisabled = RoundedShape(new Color(0.93f, 0.90f, 0.84f, 1f), bRad, new Color(0.23f, 0.17f, 0.125f, 0.35f), 3, false, 0f);
			_texSwitchOn = RoundedShape(ColMint, 32, ColInk, 3, false, 0f);
			_texSwitchOff = RoundedShape(new Color(0.91f, 0.87f, 0.79f, 1f), 32, ColInk, 3, false, 0f);
			_texKnob = RoundedShape(ColWhite, 32, ColInk, 3, false, 0f);
			_texField = RoundedShape(ColWhite, (int)(12f * Scale), ColInk, 3, false, 0f);
			_texRowHover = RoundedShape(new Color(1f, 0.79f, 0.24f, 0.16f), (int)(10f * Scale), null, 0, false, 0f);
			_texShadowInk = RoundedShape(ColInk, rad, null, 0, false, 0f);
			_texWhite = Texture2D.whiteTexture;

			Panel = Base(_texCard, 13, ColInk, TextAnchor.UpperLeft, rad);
			Panel.border = new RectOffset(rad, rad, rad, rad);
			Panel.overflow = new RectOffset(0, 0, 0, 0);
			Panel.padding = new RectOffset((int)(18f * Scale), (int)(18f * Scale), (int)(28f * Scale), (int)(14f * Scale));

			DockBg = Base(_texDock, 13, ColInk, TextAnchor.MiddleCenter, dockRad);
			DockBg.border = new RectOffset(dockRad, dockRad, dockRad, dockRad);
			DockBg.overflow = new RectOffset(0, 0, 0, 0);
			DockBg.padding = new RectOffset((int)(10f * Scale), (int)(10f * Scale), (int)(7f * Scale), (int)(7f * Scale));
			DockItem = Base(_texDockItem, (int)(13f * Scale), ColInk, TextAnchor.MiddleCenter, bRad);
			DockItem.fontStyle = FontStyle.Bold;
			DockItemOn = Base(_texDockItemOn, (int)(13f * Scale), ColWhite, TextAnchor.MiddleCenter, bRad);
			DockItemOn.fontStyle = FontStyle.Bold;

			Title = Base(null, (int)(16f * Scale), ColInk, TextAnchor.MiddleLeft);
			Title.fontStyle = FontStyle.Bold;

			Tag = Base(null, (int)(14f * Scale), ColInk, TextAnchor.MiddleCenter);
			Tag.fontStyle = FontStyle.Bold;

			Chip = Base(_texChip, (int)(11f * Scale), ColInk, TextAnchor.MiddleCenter);
			Chip.fontStyle = FontStyle.Bold;

			Tab = Base(_texPillWhite, (int)(13f * Scale), ColInk, TextAnchor.MiddleCenter, pillRad);
			TabActive = Base(_texPillCoral, (int)(13f * Scale), ColWhite, TextAnchor.MiddleCenter, pillRad);
			TabActive.fontStyle = FontStyle.Bold;

			Btn = Base(_texBtnWhite, (int)(13f * Scale), ColInk, TextAnchor.MiddleLeft, bRad);
			Btn.padding.left = (int)(12f * Scale);
			Btn.richText = true;
			Btn.fontStyle = FontStyle.Bold;
			Btn.hover.background = _texRowHover;

			BtnPrimary = Base(_texBtnCoral, (int)(13f * Scale), ColWhite, TextAnchor.MiddleCenter, bRad);
			BtnPrimary.fontStyle = FontStyle.Bold;

			BtnGhost = Base(_texBtnWhite, (int)(13f * Scale), ColInk, TextAnchor.MiddleCenter, bRad);
			BtnGhost.fontStyle = FontStyle.Bold;

			BtnDanger = Base(_texBtnCoral, (int)(13f * Scale), ColWhite, TextAnchor.MiddleCenter, bRad);
			BtnDanger.fontStyle = FontStyle.Bold;

			BtnDisabled = Base(_texBtnDisabled, (int)(13f * Scale), new Color(0.54f, 0.47f, 0.40f, 0.6f), TextAnchor.MiddleCenter, bRad);
			BtnDisabled.richText = true;

			Label = Base(null, (int)(13f * Scale), ColInk, TextAnchor.MiddleLeft);
			Label.fontStyle = FontStyle.Bold;
			Label.richText = true;
			Small = Base(null, (int)(11f * Scale), ColMuted, TextAnchor.MiddleLeft);
			Small.richText = true;
			Dim = Base(null, (int)(12f * Scale), ColMuted, TextAnchor.MiddleLeft);
			Dim.richText = true;
			Section = Base(null, (int)(12f * Scale), ColInk, TextAnchor.MiddleLeft);
			Section.fontStyle = FontStyle.Bold;
			Value = Base(null, (int)(14f * Scale), ColCoral, TextAnchor.MiddleRight);
			Value.fontStyle = FontStyle.Bold;

			Field = Base(_texField, (int)(14f * Scale), ColInk, TextAnchor.MiddleLeft, (int)(12f * Scale));
			Field.padding.left = (int)(10f * Scale);
			Field.fontStyle = FontStyle.Bold;

			Footer = Base(null, (int)(11f * Scale), ColMuted, TextAnchor.MiddleLeft);

			Box = Base(_texCard, (int)(12f * Scale), ColInk, TextAnchor.MiddleLeft, rad);

			// 游戏内 overlay：深色画面上的浅色文字（半透明黑底由调用方先画）
			OverlayLabel = Base(null, (int)(12f * Scale), ColOverlayText, TextAnchor.MiddleLeft);
			OverlayLabel.richText = true;
			OverlaySmall = Base(null, (int)(11f * Scale), ColOverlayText, TextAnchor.MiddleLeft);
			OverlayValue = Base(null, (int)(30f * Scale), ColOverlayGold, TextAnchor.MiddleRight);
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

		/// <summary>画完 MakeSolid 底板后调用，恢复染色。</summary>
		internal static void ClearTint()
		{
			GUI.color = Color.white;
		}

		/// <summary>
		/// 从零构建样式，绝不继承 GUI.skin（游戏可能带自定义皮肤，
		/// 其 onFocused 等未覆盖状态会在窗口获得焦点/点击后被绘制）。
		/// </summary>
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
			s.fontSize = fontSize;
			s.normal.textColor = color;
			s.hover.textColor = color;
			s.active.textColor = color;
			s.focused.textColor = color;
			s.alignment = anchor;
			s.wordWrap = false;
			s.overflow = new RectOffset(radius, radius, radius, radius);
			if (radius > 0)
			{
				s.border = new RectOffset(radius, radius, radius, radius);
			}
			return s;
		}

		/// <summary>
		/// 生成圆角纹理。radius=32 得到胶囊/圆形；outline 墨水描边；
		/// topOnly 只圆上面两角；edgeFade 边缘渐隐（阴影用）。
		/// </summary>
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

		// ---- 布局游标 ----
		private static Rect _cur;
		private static float _left;
		private static float _width;

		internal static void BeginArea(float x, float y, float w)
		{
			_left = x;
			_width = w;
			_cur = new Rect(x, y, w, RowH * Scale);
		}

		internal static void Row(float h)
		{
			_cur.x = _left;
			_cur.width = _width;
			_cur.height = h * Scale;
		}

		internal static void Gap(float g = RowGap)
		{
			_cur.y += g * Scale;
		}

		internal static void Next()
		{
			_cur.y += _cur.height + RowGap * Scale;
			_cur.x = _left;
			_cur.width = _width;
		}

		internal static float CursorY => _cur.y;
		internal static float ContentWidth => _width;

		// ---- 控件 ----

		/// <summary>糖果硬阴影：在 rect 右下偏移处画一层墨色圆角。</summary>
		internal static void HardShadow(Rect r, float offset)
		{
			Color old = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, 0.8f);
			GUI.DrawTexture(new Rect(r.x + offset, r.y + offset, r.width, r.height), _texShadowInk);
			GUI.color = old;
		}

		internal static bool Button(GUIContent c, GUIStyle style = null)
		{
			if (style == null || style == Btn || style == BtnGhost || style == BtnPrimary)
			{
				HardShadow(_cur, 3.5f * Scale);
			}
			return GUI.Button(_cur, c, style ?? Btn);
		}

		/// <summary>可交互开关的行动按钮：interactable=false 时画禁用态且不响应。</summary>
		internal static bool Button(GUIContent c, bool interactable)
		{
			if (!interactable)
			{
				GUI.Button(_cur, c, BtnDisabled);
				return false;
			}
			return Button(c, Btn);
		}

		/// <summary>主操作按钮（珊瑚实底，硬阴影）。</summary>
		internal static bool Primary(GUIContent c, bool interactable = true)
		{
			if (!interactable)
			{
				GUI.Button(_cur, c, BtnDisabled);
				return false;
			}
			return Button(c, BtnPrimary);
		}

		/// <summary>次操作按钮（白底描边）。</summary>
		internal static bool Ghost(GUIContent c, bool interactable = true)
		{
			if (!interactable)
			{
				GUI.Button(_cur, c, BtnDisabled);
				return false;
			}
			return Button(c, BtnGhost);
		}

		/// <summary>
		/// 开关行：整行可点，标签居左，右侧糖果滑块。返回是否被点击。
		/// </summary>
		internal static bool ToggleRow(GUIContent label, bool on, bool interactable = true)
		{
			Rect row = _cur;
			bool hover = interactable && row.Contains(Event.current.mousePosition);
			if (hover)
			{
				GUI.DrawTexture(row, _texRowHover);
			}
			Rect labelRect = new Rect(row.x + 10f * Scale, row.y, row.width - 76f * Scale, row.height);
			GUI.Label(labelRect, label, interactable ? Label : Dim);
			Rect sw = new Rect(row.xMax - (44f + 10f) * Scale, row.y + (row.height - 19f * Scale) * 0.5f, 44f * Scale, 19f * Scale);
			Switch(sw, on);
			return interactable && GUI.Button(row, GUIContent.none, GUIStyle.none);
		}

		/// <summary>糖果滑块开关（墨水描边 + 薄荷开启态）。</summary>
		internal static void Switch(Rect r, bool on)
		{
			HardShadow(r, 2.5f * Scale);
			GUI.DrawTexture(r, on ? _texSwitchOn : _texSwitchOff);
			float k = 15f * Scale;
			Rect knob = new Rect(on ? r.xMax - k - 2f * Scale : r.x + 2f * Scale, r.y + (r.height - k) * 0.5f, k, k);
			GUI.DrawTexture(knob, _texKnob);
		}

		/// <summary>小圆点。</summary>
		internal static void Dot(Rect r, Color color)
		{
			Color old = GUI.color;
			GUI.color = color;
			GUI.DrawTexture(r, _texKnob);
			GUI.color = old;
		}

		internal static void LabelRow(GUIContent c, GUIStyle style = null)
		{
			GUI.Label(_cur, c, style ?? Label);
		}

		internal static void SectionRow(GUIContent c)
		{
			GUI.Label(_cur, c, Section);
			float y = _cur.y + _cur.height - 2f * Scale;
			Color old = GUI.color;
			GUI.color = new Color(ColCoral.r, ColCoral.g, ColCoral.b, 0.8f);
			GUI.DrawTexture(new Rect(_cur.x, y, _cur.width, 2f * Scale), _texWhite);
			GUI.color = old;
		}

		internal static bool TextField(ref string buffer, out string edited)
		{
			string newText = GUI.TextField(_cur, buffer, Field);
			edited = newText;
			if (!ReferenceEquals(newText, buffer))
			{
				buffer = newText;
				return true;
			}
			return false;
		}

		/// <summary>在整行里切分出水平子区域（不动游标）。</summary>
		internal static Rect Slice(float offsetFrac, float widthFrac)
		{
			Rect r = _cur;
			r.x += _cur.width * offsetFrac;
			r.width = _cur.width * widthFrac;
			return r;
		}

		/// <summary>标签牌纹理（按页签配色）。</summary>
		internal static Texture2D TagTex(int tab)
		{
			return _texTags[tab];
		}

		/// <summary>主机角标富文本后缀（初始化期拼接一次）。</summary>
		internal static string HostSuffix => UseZh
			? " <color=#" + HostTagHex + "><size=10>[主机]</size></color>"
			: " <color=#" + HostTagHex + "><size=10>[HOST]</size></color>";

		internal static GUIContent GC(string zh, string en)
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
}
