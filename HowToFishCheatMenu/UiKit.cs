using System;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>
	/// UI 3.0 主题与控件层。规避 GC 的关键点：
	/// - 圆角纹理 / GUIStyle / GUIContent 全部初始化一次并复用
	/// - 布局用静态游标推进（Rect 是 struct，就地复用，不分配）
	/// - 所有样式从零构建，绝不继承 GUI.skin（游戏自定义皮肤的状态会泄漏）
	/// </summary>
	internal static class Ui
	{
		// ---- 设计令牌 ----
		// 窗口与面板必须全不透明：半透明底会和游戏暂停菜单遮罩叠出脏色
		internal static readonly Color ColWinBg = new Color(0.052f, 0.066f, 0.098f, 1f);
		internal static readonly Color ColPanel = new Color(0.10f, 0.128f, 0.175f, 1f);
		internal static readonly Color ColRowHover = new Color(0.155f, 0.205f, 0.28f, 1f);
		internal static readonly Color ColAccent = new Color(0.22f, 0.78f, 0.86f);
		internal static readonly Color ColAccentDeep = new Color(0.10f, 0.42f, 0.50f);
		internal static readonly Color ColOn = new Color(0.28f, 0.82f, 0.54f);
		internal static readonly Color ColOff = new Color(0.34f, 0.39f, 0.47f);
		internal static readonly Color ColDanger = new Color(0.93f, 0.42f, 0.35f);
		internal static readonly Color ColGold = new Color(1f, 0.79f, 0.28f);
		internal static readonly Color ColText = new Color(0.93f, 0.96f, 0.98f);
		internal static readonly Color ColDim = new Color(0.585f, 0.665f, 0.745f);
		internal const string HostTagHex = "6fd0e0";

		// ---- 布局常量（乘以缩放） ----
		internal const float RowH = 32f;
		internal const float RowGap = 6f;
		internal const float Pad = 12f;
		internal const float HeaderH = 40f;
		internal const float TabH = 30f;

		internal static float Scale = 1f;

		// ---- 样式 ----
		private static bool _ready;
		internal static GUIStyle Win;
		internal static GUIStyle Header;
		internal static GUIStyle Title;
		internal static GUIStyle Chip;
		internal static GUIStyle Tab;
		internal static GUIStyle TabActive;
		internal static GUIStyle Btn;
		internal static GUIStyle BtnPrimary;
		internal static GUIStyle BtnGhost;
		internal static GUIStyle BtnOn;
		internal static GUIStyle BtnOff;
		internal static GUIStyle BtnDanger;
		internal static GUIStyle BtnDisabled;
		internal static GUIStyle Label;
		internal static GUIStyle Small;
		internal static GUIStyle Dim;
		internal static GUIStyle Section;
		internal static GUIStyle Value;   // 右对齐数值
		internal static GUIStyle Field;   // 输入框
		internal static GUIStyle Footer;
		internal static GUIStyle Box;

		internal static Font UiFont;
		internal static bool UseZh;

		private static Texture2D _texWin;
		private static Texture2D _texPanel;
		private static Texture2D _texRowHover;
		private static Texture2D _texAccent;
		private static Texture2D _texOn;
		private static Texture2D _texOff;
		private static Texture2D _texDanger;
		private static Texture2D _texField;
		private static Texture2D _texWhite;
		private static Texture2D _texGhost;
		private static Texture2D _texHeaderGrad;
		private static Texture2D _texPillOn;
		private static Texture2D _texPillOff;
		private static Texture2D _texKnob;
		private static Texture2D _texShadow;
		private static Texture2D _texChip;

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

		/// <summary>缩放滑条等场景下强制下一帧重建纹理与样式（旧纹理随之销毁，避免泄漏）。</summary>
		internal static void InvalidateStyles()
		{
			if (!_ready)
			{
				return;
			}
			_ready = false;
			Texture2D[] tex = { _texWin, _texPanel, _texRowHover, _texAccent, _texOn, _texOff, _texDanger, _texField, _texGhost, _texHeaderGrad, _texPillOn, _texPillOff, _texKnob, _texShadow, _texChip };
			for (int i = 0; i < tex.Length; i++)
			{
				if (tex[i] != null && tex[i] != Texture2D.whiteTexture)
				{
					UnityEngine.Object.Destroy(tex[i]);
				}
			}
			_texWin = null;
			_texPanel = null;
			_texRowHover = null;
			_texAccent = null;
			_texOn = null;
			_texOff = null;
			_texDanger = null;
			_texField = null;
			_texGhost = null;
			_texHeaderGrad = null;
			_texPillOn = null;
			_texPillOff = null;
			_texKnob = null;
			_texShadow = null;
			_texChip = null;
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
			// 鼠标悬停/点击滚动区就会盖出白色光斑。游戏自身完全没有 IMGUI，清空零副作用。
			GUIStyle sv = GUI.skin.scrollView;
			sv.normal.background = null;
			sv.hover.background = null;
			sv.active.background = null;
			sv.focused.background = null;
			sv.onNormal.background = null;
			sv.onHover.background = null;
			sv.onActive.background = null;
			sv.onFocused.background = null;

			int rad = (int)(10f * Scale);
			Color outline = new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.35f);
			_texWin = RoundedShape(ColWinBg, rad, outline, 1, false, 0f);
			_texPanel = RoundedShape(ColPanel, Mathf.Min(8, rad), new Color(1f, 1f, 1f, 0.05f), 1, false, 0f);
			_texRowHover = RoundedShape(ColRowHover, Mathf.Min(6, rad), null, 0, false, 0f);
			_texAccent = RoundedShape(new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.95f), Mathf.Min(8, rad), null, 0, false, 0f);
			_texOn = RoundedShape(new Color(ColOn.r, ColOn.g, ColOn.b, 0.22f), Mathf.Min(8, rad), new Color(ColOn.r, ColOn.g, ColOn.b, 0.75f), 1, false, 0f);
			_texOff = RoundedShape(new Color(1f, 1f, 1f, 0.05f), Mathf.Min(8, rad), new Color(1f, 1f, 1f, 0.09f), 1, false, 0f);
			_texDanger = RoundedShape(new Color(ColDanger.r, ColDanger.g, ColDanger.b, 0.28f), Mathf.Min(8, rad), new Color(ColDanger.r, ColDanger.g, ColDanger.b, 0.7f), 1, false, 0f);
			_texField = RoundedShape(new Color(0f, 0f, 0f, 0.4f), Mathf.Min(6, rad), new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.3f), 1, false, 0f);
			_texGhost = RoundedShape(new Color(1f, 1f, 1f, 0.03f), Mathf.Min(8, rad), new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.55f), 1, false, 0f);
			_texHeaderGrad = GradientHeader(rad);
			_texPillOn = RoundedShape(ColOn, 32, null, 0, false, 0f);
			_texPillOff = RoundedShape(ColOff, 32, null, 0, false, 0f);
			_texKnob = RoundedShape(new Color(0.96f, 0.98f, 1f), 32, null, 0, false, 0f);
			_texShadow = RoundedShape(new Color(0f, 0f, 0f, 0.62f), 24, null, 0, false, 16f * Scale);
			_texChip = RoundedShape(new Color(ColAccentDeep.r, ColAccentDeep.g, ColAccentDeep.b, 0.85f), 32, null, 0, false, 0f);
			_texWhite = Texture2D.whiteTexture;

			Win = Base(_texWin, 13, ColText, TextAnchor.UpperLeft, rad);
			// 九宫格拉伸：边框切片保持像素宽，烘焙的 1px 描边不会被拉伸成粗带
			Win.border = new RectOffset(rad, rad, rad, rad);
			Win.overflow = new RectOffset(0, 0, 0, 0);
			Win.padding = new RectOffset(0, 0, 0, 0);

			Header = Base(_texHeaderGrad, 13, ColText, TextAnchor.MiddleLeft, Mathf.Min(10, rad));
			Header.border = new RectOffset(Mathf.Min(10, rad), Mathf.Min(10, rad), Mathf.Min(10, rad), 0);
			Header.overflow = new RectOffset(0, 0, 0, 0);

			Title = Base(null, (int)(15f * Scale), ColText, TextAnchor.MiddleLeft);
			Title.fontStyle = FontStyle.Bold;

			Chip = Base(_texChip, (int)(10f * Scale), new Color(0.75f, 0.93f, 0.97f), TextAnchor.MiddleCenter);
			Chip.padding.left = 8;
			Chip.padding.right = 8;

			Tab = Base(null, (int)(13f * Scale), ColDim, TextAnchor.MiddleCenter);
			TabActive = Base(_texAccent, (int)(13f * Scale), new Color(0.02f, 0.09f, 0.11f), TextAnchor.MiddleCenter);
			TabActive.fontStyle = FontStyle.Bold;

			Btn = Base(_texOff, (int)(13f * Scale), ColText, TextAnchor.MiddleLeft);
			Btn.padding.left = (int)(10f * Scale);
			Btn.richText = true;
			Btn.hover.background = _texRowHover;

			BtnOn = Base(_texOn, (int)(13f * Scale), ColOn, TextAnchor.MiddleLeft);
			BtnOn.padding.left = (int)(10f * Scale);
			BtnOn.richText = true;
			BtnOn.fontStyle = FontStyle.Bold;

			BtnOff = Btn;

			BtnPrimary = Base(_texAccent, (int)(13f * Scale), new Color(0.02f, 0.09f, 0.11f), TextAnchor.MiddleCenter);
			BtnPrimary.fontStyle = FontStyle.Bold;

			BtnGhost = Base(_texGhost, (int)(13f * Scale), ColText, TextAnchor.MiddleCenter);

			BtnDanger = Base(_texDanger, (int)(13f * Scale), new Color(1f, 0.84f, 0.82f), TextAnchor.MiddleCenter);

			BtnDisabled = Base(_texOff, (int)(13f * Scale), new Color(0.5f, 0.55f, 0.6f, 0.5f), TextAnchor.MiddleLeft);
			BtnDisabled.padding.left = (int)(10f * Scale);
			BtnDisabled.richText = true;

			Label = Base(null, (int)(12f * Scale), ColText, TextAnchor.MiddleLeft);
			Label.richText = true;
			Small = Base(null, (int)(11f * Scale), ColDim, TextAnchor.MiddleLeft);
			Small.richText = true;
			Dim = Base(null, (int)(12f * Scale), ColDim, TextAnchor.MiddleLeft);
			Dim.richText = true;
			Section = Base(null, (int)(12f * Scale), ColAccent, TextAnchor.MiddleLeft);
			Section.fontStyle = FontStyle.Bold;
			Value = Base(null, (int)(13f * Scale), ColGold, TextAnchor.MiddleRight);
			Value.fontStyle = FontStyle.Bold;

			Field = Base(_texField, (int)(13f * Scale), ColText, TextAnchor.MiddleLeft, Mathf.Min(6, rad));
			Field.padding.left = (int)(8f * Scale);

			Footer = Base(null, (int)(11f * Scale), ColDim, TextAnchor.MiddleLeft);

			Box = Base(_texPanel, (int)(12f * Scale), ColText, TextAnchor.MiddleLeft, Mathf.Min(8, rad));

			_ready = true;
		}

		/// <summary>
		/// 从零构建样式，绝不继承 GUI.skin（游戏可能带自定义皮肤，
		/// 其 onFocused 等未覆盖状态会在窗口获得焦点/点击后被绘制——之前点击后出现
		/// 白色光斑覆盖菜单就是这个原因）。
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
		/// 生成圆角纹理（一次性成本）。radius=32 得到胶囊/圆形。
		/// outline: 1px 描边色（可为 null）；topOnly: 只圆上面两角（标题栏用）；
		/// edgeFade: 从边缘向内 alpha 渐显的像素数（窗口阴影用）。
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
					// s = 到形状边缘的有符号距离（内部为正）
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

		/// <summary>标题栏：青色向右渐隐的水平渐变，只圆上面两角。</summary>
		private static Texture2D GradientHeader(int radius)
		{
			int size = 64;
			Texture2D t = new Texture2D(size, size, TextureFormat.ARGB32, false);
			t.name = "ui_header";
			Color[] px = new Color[size * size];
			Color a = new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.30f);
			Color b = new Color(ColPanel.r, ColPanel.g, ColPanel.b, 1f);
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float cx = Mathf.Min(x + 0.5f, size - x - 0.5f);
					float cy = y + 0.5f;
					float s;
					if (cx < radius && cy < radius)
					{
						float dx = radius - cx;
						float dy = radius - cy;
						s = radius - Mathf.Sqrt(dx * dx + dy * dy);
					}
					else
					{
						s = Mathf.Min(cx, cy);
					}
					float alpha = s <= 0f ? 0f : Mathf.Clamp01(s + 0.5f);
					float tParam = x / (float)(size - 1);
					Color c = Color.Lerp(a, b, tParam);
					c.a *= alpha;
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
		internal static bool Button(GUIContent c, GUIStyle style = null)
		{
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
			return GUI.Button(_cur, c, Btn);
		}

		/// <summary>主操作按钮（青色实底、居中）。</summary>
		internal static bool Primary(GUIContent c, bool interactable = true)
		{
			if (!interactable)
			{
				GUI.Button(_cur, c, BtnDisabled);
				return false;
			}
			return GUI.Button(_cur, c, BtnPrimary);
		}

		/// <summary>次操作按钮（描边幽灵、居中）。</summary>
		internal static bool Ghost(GUIContent c, bool interactable = true)
		{
			if (!interactable)
			{
				GUI.Button(_cur, c, BtnDisabled);
				return false;
			}
			return GUI.Button(_cur, c, BtnGhost);
		}

		/// <summary>
		/// 开关行：整行可点，标签居左，右侧 iOS 式滑块开关。返回是否被点击。
		/// </summary>
		internal static bool ToggleRow(GUIContent label, bool on, bool interactable = true)
		{
			Rect row = _cur;
			bool hover = interactable && row.Contains(Event.current.mousePosition);
			if (hover)
			{
				GUI.DrawTexture(row, _texRowHover);
			}
			Rect labelRect = new Rect(row.x + 10f * Scale, row.y, row.width - 70f * Scale, row.height);
			GUI.Label(labelRect, label, interactable ? Label : BtnDisabled);
			Rect sw = new Rect(row.xMax - (38f + 10f) * Scale, row.y + (row.height - 16f * Scale) * 0.5f, 38f * Scale, 16f * Scale);
			Switch(sw, on);
			return interactable && GUI.Button(row, GUIContent.none, GUIStyle.none);
		}

		/// <summary>iOS 式滑块开关。</summary>
		internal static void Switch(Rect r, bool on)
		{
			GUI.color = on ? Color.white : new Color(1f, 1f, 1f, 0.55f);
			GUI.DrawTexture(r, on ? _texPillOn : _texPillOff);
			GUI.color = Color.white;
			float k = 12f * Scale;
			Rect knob = new Rect(on ? r.xMax - k - 2f * Scale : r.x + 2f * Scale, r.y + (r.height - k) * 0.5f, k, k);
			GUI.DrawTexture(knob, _texKnob);
		}

		/// <summary>小圆点（主机状态指示等）。</summary>
		internal static void Dot(Rect r, Color color)
		{
			GUI.color = color;
			GUI.DrawTexture(r, _texKnob);
			GUI.color = Color.white;
		}

		internal static void LabelRow(GUIContent c, GUIStyle style = null)
		{
			GUI.Label(_cur, c, style ?? Label);
		}

		internal static void SectionRow(GUIContent c)
		{
			// 分节标题带一条下划线
			GUI.Label(_cur, c, Section);
			float y = _cur.y + _cur.height - 2f * Scale;
			Color old = GUI.color;
			GUI.color = new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.45f);
			GUI.DrawTexture(new Rect(_cur.x, y, _cur.width, 1.5f * Scale), _texWhite);
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

		/// <summary>在指定矩形外扩 delta 后绘制窗口阴影（多圈渐淡）。</summary>
		internal static void DrawShadow(Rect r, float delta)
		{
			for (int i = 3; i >= 1; i--)
			{
				float d = delta * (0.5f + 0.5f * i);
				Color old = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, 0.28f * (4 - i) / 3f);
				GUI.DrawTexture(new Rect(r.x - d, r.y - d, r.width + d * 2f, r.height + d * 2f), _texShadow);
				GUI.color = old;
			}
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
