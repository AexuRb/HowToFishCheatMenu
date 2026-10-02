using System;
using UnityEngine;

namespace HTF.CheatMenu
{
	/// <summary>
	/// UI 2.0 主题与控件层。规避 GC 的关键点：
	/// - 圆角纹理 / GUIStyle / GUIContent 全部初始化一次并复用
	/// - 布局用静态游标推进（Rect 是 struct，就地复用，不分配）
	/// - 主机角标通过富文本常量拼接，按钮样式 richText=true
	/// </summary>
	internal static class Ui
	{
		// ---- 设计令牌 ----
		// 窗口与面板必须全不透明：半透明底会和游戏暂停菜单遮罩叠出脏色
		internal static readonly Color ColWinBg = new Color(0.065f, 0.085f, 0.125f, 1f);
		internal static readonly Color ColHeader = new Color(0.10f, 0.14f, 0.20f, 1f);
		internal static readonly Color ColPanel = new Color(0.115f, 0.15f, 0.21f, 1f);
		internal static readonly Color ColRowHover = new Color(0.17f, 0.22f, 0.30f, 1f);
		internal static readonly Color ColAccent = new Color(0.24f, 0.78f, 0.86f);
		internal static readonly Color ColAccentDim = new Color(0.15f, 0.45f, 0.52f);
		internal static readonly Color ColOn = new Color(0.30f, 0.82f, 0.52f);
		internal static readonly Color ColOff = new Color(0.42f, 0.47f, 0.54f);
		internal static readonly Color ColDanger = new Color(0.93f, 0.42f, 0.35f);
		internal static readonly Color ColGold = new Color(1f, 0.78f, 0.24f);
		internal static readonly Color ColText = new Color(0.93f, 0.96f, 0.98f);
		internal static readonly Color ColDim = new Color(0.60f, 0.68f, 0.76f);
		internal const string HostTagHex = "6fd0e0";

		// ---- 布局常量（乘以缩放） ----
		internal const float RowH = 32f;
		internal const float RowGap = 6f;
		internal const float Pad = 12f;
		internal const float HeaderH = 36f;
		internal const float TabH = 30f;

		internal static float Scale = 1f;

		// ---- 样式 ----
		private static bool _ready;
		internal static GUIStyle Win;
		internal static GUIStyle Header;
		internal static GUIStyle Title;
		internal static GUIStyle Tab;
		internal static GUIStyle TabActive;
		internal static GUIStyle Btn;
		internal static GUIStyle BtnPrimary;
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
		private static Texture2D _texRow;
		private static Texture2D _texAccent;
		private static Texture2D _texOn;
		private static Texture2D _texOff;
		private static Texture2D _texDanger;
		private static Texture2D _texField;
		private static Texture2D _texWhite;

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
			Texture2D[] tex = { _texWin, _texPanel, _texRow, _texAccent, _texOn, _texOff, _texDanger, _texField };
			for (int i = 0; i < tex.Length; i++)
			{
				if (tex[i] != null && tex[i] != Texture2D.whiteTexture)
				{
					UnityEngine.Object.Destroy(tex[i]);
				}
			}
			_texWin = null;
			_texPanel = null;
			_texRow = null;
			_texAccent = null;
			_texOn = null;
			_texOff = null;
			_texDanger = null;
			_texField = null;
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
			_texWin = Rounded(ColWinBg, rad);
			_texPanel = Rounded(ColPanel, rad);
			_texRow = Rounded(ColRowHover, Mathf.Min(8, rad));
			_texAccent = Rounded(new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.92f), Mathf.Min(8, rad));
			_texOn = Rounded(new Color(ColOn.r, ColOn.g, ColOn.b, 0.24f), Mathf.Min(8, rad));
			_texOff = Rounded(new Color(1f, 1f, 1f, 0.06f), Mathf.Min(8, rad));
			_texDanger = Rounded(new Color(ColDanger.r, ColDanger.g, ColDanger.b, 0.30f), Mathf.Min(8, rad));
			_texField = Rounded(new Color(0f, 0f, 0f, 0.35f), Mathf.Min(6, rad));
			_texWhite = Texture2D.whiteTexture;

			Win = Base(_texWin, 13, ColText, TextAnchor.UpperLeft, rad);
			Win.border = new RectOffset(0, 0, 0, 0);
			Win.padding = new RectOffset(0, 0, 0, 0);

			Header = Base(_texPanel, 13, ColText, TextAnchor.MiddleLeft, Mathf.Min(8, rad));
			Title = Base(null, (int)(15f * Scale), ColText, TextAnchor.MiddleLeft);
			Title.fontStyle = FontStyle.Bold;

			Tab = Base(_texOff, (int)(13f * Scale), ColDim, TextAnchor.MiddleCenter, Mathf.Min(8, rad));
			TabActive = Base(_texAccent, (int)(13f * Scale), new Color(0.03f, 0.10f, 0.12f), TextAnchor.MiddleCenter, Mathf.Min(8, rad));
			TabActive.fontStyle = FontStyle.Bold;

			Btn = Base(_texOff, (int)(13f * Scale), ColText, TextAnchor.MiddleLeft);
			Btn.padding.left = (int)(10f * Scale);
			Btn.richText = true;
			Btn.hover.textColor = ColAccent;

			BtnOn = Base(_texOn, (int)(13f * Scale), ColOn, TextAnchor.MiddleLeft);
			BtnOn.padding.left = (int)(10f * Scale);
			BtnOn.richText = true;
			BtnOn.fontStyle = FontStyle.Bold;

			BtnOff = Btn;

			BtnPrimary = Base(_texAccent, (int)(13f * Scale), new Color(0.03f, 0.10f, 0.12f), TextAnchor.MiddleCenter, Mathf.Min(8, rad));
			BtnPrimary.fontStyle = FontStyle.Bold;

			BtnDanger = Base(_texDanger, (int)(13f * Scale), new Color(1f, 0.82f, 0.80f), TextAnchor.MiddleLeft);
			BtnDanger.padding.left = (int)(10f * Scale);

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

		/// <summary>生成圆角纯色纹理（一次性成本，运行期复用）。</summary>
		private static Texture2D Rounded(Color fill, int radius)
		{
			int size = 64;
			Texture2D t = new Texture2D(size, size, TextureFormat.ARGB32, false);
			t.name = "ui_rounded";
			Color[] px = new Color[size * size];
			float r = radius;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float a = fill.a;
					if (r > 0f)
					{
						float cx = Mathf.Min(x + 0.5f, size - x - 0.5f);
						float cy = Mathf.Min(y + 0.5f, size - y - 0.5f);
						if (cx < r && cy < r)
						{
							float dx = r - cx;
							float dy = r - cy;
							float d = Mathf.Sqrt(dx * dx + dy * dy);
							// 距角圆心超过半径则透明，1px 内做抗锯齿过渡
							float k = Mathf.Clamp01(r - d + 0.5f);
							a *= k;
						}
					}
					Color c = fill;
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

		/// <summary>开关按钮：on 时用绿色态样式。interactable=false 时画禁用态且不响应。</summary>
		internal static bool Toggle(GUIContent c, bool on, bool interactable = true)
		{
			if (!interactable)
			{
				GUI.Button(_cur, c, BtnDisabled);
				return false;
			}
			return GUI.Button(_cur, c, on ? BtnOn : BtnOff);
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
