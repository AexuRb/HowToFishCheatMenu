# How to Fish 作弊菜单 Mod

[How to Fish](https://store.steampowered.com/)（Dazed Games，Unity 6000.4.4f1 Mono）的 BepInEx 作弊 + 便利功能插件。

## 菜单结构（F1，v1.2 四分类页）

| 页签 | 内容 |
|---|---|
| **玩家** | 内置作弊、上帝模式、一击必杀、无限弹匣、秒咬钩、移速倍率、跳跃倍率、加钱 |
| **世界** | 上一岛/下一岛、秒杀 Boss、击杀全部活物、清空图鉴击杀记录、刷物品（全高列表） |
| **显示** | 鱼群雷达、雷达名字、弹药显示、性能监视 |
| **杂项** | 解锁全部皮肤、解锁全部成就、结束本局 |

### 各功能说明

| 功能 | 说明 | 热键 |
|---|---|---|
| 内置作弊 | 打开游戏自带作弊通道（`ClientSettings.CheatsEnabled`），启动时自动开启 | — |
| 上帝模式 | 无敌 + 免饥饿（游戏原生实现，服务器权威） | F2 |
| 秒咬钩 | 鱼饵入水立即咬钩。prefix `CreatureManager.FindFishForBait`（服务器网络 tick 判定），写零 `<RandomizedCatchTime>`。**鱼饵需沉到水下约 2.5m**（游戏原判定） | F3 |
| 移速倍率 | 1 / 1.5 / 2 / 3 / 5 倍循环（临时放大 `_walkSpeed`/`_sprintSpeed`，游泳同样生效；有平滑过渡） | F4 |
| 加钱 | 默认 +10000（可在配置改金额） | F5 |
| 跳跃倍率 | 1 / 2 / 3 / 5 倍循环（Jump 后缩放上升速度） | 菜单内 |
| 无限弹匣 | 开枪后弹匣立即回满（postfix `Weapon.Shoot`）。弹药是客户端模拟的，持枪者本地无限有效 | F8 |
| 刷物品 | 物品总表（**本地化中文名**，重名变体带 prefab 后缀）一键生成到准星前方（需主机） | 世界页 |
| 鱼群雷达 | 屏幕标记所有存活生物：金色=闪光、红色=Boss、白色=普通；名字为**游戏本地化中文名**（F6） | F6 |
| 弹药 HUD | 手持远程武器时右下角半透明底框大字显示 `当前弹 / 弹匣`：白=充足、黄=≤30%、红=空仓提示 [R]；切拳自动隐藏 | 显示页 |
| 性能监视器 | FPS + Mono GC 堆 + GC0 次数，1Hz 刷新（F7） | F7 |
| 击杀全部活物 | 遍历 `_aliveCreatures` 逐只 `LocalHit(999999)`（与 /killboss 同路径，服务器权威）；尸体留世界，刷怪点约 10 秒自动补齐 | 世界页 |
| 清空图鉴击杀记录 | 即游戏原 `/resetallcreatures`（只影响图鉴，不碰活物） | 世界页 |

所有键位和参数都在 `BepInEx/config/com.htf.cheatmenu.cfg` 里改。

## 多人说明

金钱、刷物品、上帝模式（饥饿 tick）、皮肤解锁由服务器权威 —— **只有当你是主机（或单机）时生效**，以客户端身份点击会收到聊天提示"该功能只有主机可用"。移速/跳跃/秒咬钩/雷达/无限弹匣/弹药 HUD 是本地效果。击杀全部活物以本地玩家为伤害来源，客户端发送 ServerRpc 亦可生效。

## 安装

**方式一（推荐）：** 到 [Releases](../../releases) 下载 `HowToFishCheatMenu-1.2.0.zip`，解压到游戏根目录（`How to Fish.exe` 所在目录），通过 Steam 启动游戏，按 F1 即可。压缩包已内置 BepInEx 5.4.23.2 和修复 Unity 6 崩溃的 doorstop 4.5 代理。

**方式二（已装 BepInEx）：** 只需把 `HowToFishCheatMenu.dll` 放进 `BepInEx/plugins/`。

游戏目录 = `C:\Program Files (x86)\Steam\steamapps\common\How to Fish\How to Fish`

> ⚠️ 本游戏是 Unity 6 构建，BepInEx 5.4.23.2 **自带**的 winhttp 代理（doorstop 4.0）没转发新版 WinHTTP 导出函数，游戏启动时会原生崩溃（`WinHttpWriteProxySettings`）。必须用 **doorstop 4.5.0** 替换（Releases 压缩包内已含）。
>
> ⚠️ 游戏必须从 Steam 客户端启动（有 Steam DRM 校验）。

## 从源码构建

```bash
cd HowToFishCheatMenu
dotnet build -c Release   # 编译成功后自动复制到游戏 plugins 目录
```

需要 .NET SDK（项目用 `Microsoft.NETFramework.ReferenceAssemblies`，无需装 472 Targeting Pack）。游戏 DLL 引用路径写在 csproj 的 `GameDir` 属性里。Harmony 补丁注册必须用 `PatchAll(Assembly)`——`PatchAll(Type)` 对嵌套补丁类会静默跳过（本项目踩过的坑，见提交历史）。

## 内存设计（重点）

针对之前代码内存暴涨的问题，本插件全程按零 GC 分配规范编写：

- **Update/OnGUI 热路径零分配**：只做 `KeyCode` 比较、字段读写、结构体运算
- **IMGUI 全部复用**：`GUIContent`/`GUIStyle`/`GUI.WindowFunction` 委托/`Rect` 全部初始化时缓存；固定 `Rect` 的 `GUI.*`，不用 `GUILayout`（其布局每次调用都分配）
- **不用 LINQ、不用闭包、不订阅实例事件**（无事件泄漏面）
- **字符串只在状态变化或 1Hz 时生成**（性能监视器 1 秒一次；弹药 HUD 只在弹药数变化时重建）
- **反射零装箱**：写 auto-property backing field（咬钩计时、弹药）用**编译一次的表达式委托**（`FieldInfo.SetValue` 会装箱）；移速补丁用 Harmony `ref` 参数注入；`_rig` 是引用类型字段，`FieldInfo.GetValue` 不装箱
- **ESP 名字缓存**：`Dictionary<int,string>` 按实例 ID 缓存，超 1024 条自动清空；Unity 的 `Object.name` 每次调用都分配，因此每只生物只取一次
- 日志只在启动和出错时打，不逐帧打印

可用 F7 性能监视器实测：开关菜单/雷达时 `GC` 数值不应增长（Unity IMGUI 自身有少量内部开销，与插件代码无关）。

## 仓库结构

```
HowToFishCheatMenu/
├── Plugin.cs              # 入口、配置、热键、补丁注册
├── CheatCore.cs           # 游戏 API 封装（全部带主机校验）
├── Patches.cs             # 4 个 Harmony 补丁 + 编译表达式 setter
└── MenuUI.cs              # IMGUI 菜单 + 雷达 + 弹药 HUD + 性能监视
```

## 免责声明

本 mod 仅供学习交流，与 Dazed Games 官方无关。联机使用请征得其他玩家同意。

