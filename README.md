# TypeSimulator V9

基于 [YinChingZ/TypeSimulator](https://github.com/YinChingZ/TypeSimulator) 的增强版，目标是把“自动输入回放”做得更接近真人键入节奏，适合录屏演示、脚本回放、直播展示等场景。

## V9 更新重点

- 新增“标点发呆停顿”开关
- 新增“停顿时长”滑条（`100ms ~ 3000ms`）
- 在 `.` `,` `。` `，` 后加入额外随机停顿（`0 ~ 设定值`）
- 设置支持自动保存，重启后继续沿用

## 已有核心功能

- 打字速度可调（CPM）
- 随机延迟开关
- 错字模拟（打错 -> 退格 -> 重打）
- 错字概率可调（`1% ~ 90%`）
- 偶发双击字符后退格修正
- 错字后额外 `0.5s ~ 1s` 停顿（开关 + 触发概率）
- 节奏曲线（开头慢 / 中段快 / 结尾慢，可开关 + 强度可调）
- 疲劳曲线（后半段略慢，可开关 + 强度可调）
- 进度条 + 剩余时长估算
- 按键映射模式与全局快捷键控制

## 运行环境

- Windows 10 / 11
- .NET Framework 4.8
- 建议管理员身份运行（全局热键、键盘钩子更稳定）

## 快速上手

1. 启动程序，粘贴或输入要回放的文本
2. 设置速度与各类拟人参数
3. 点击“开始打字”或按 `Ctrl + Alt + T`
4. 回放完成后可直接再次调整参数继续测试

## 参数建议（实用版）

- 通用演示：`120~180 CPM`，随机延迟开
- 错字概率：`2%~8%` 更自然，超过 `20%` 会明显“演”
- 标点发呆停顿：建议先从 `600~1200ms` 开始
- 长文回放：开启节奏曲线 + 疲劳曲线，观感更像真人

## 全局快捷键

- `Ctrl + Alt + T`：开始/暂停打字
- `F10`：映射暂停/恢复
- `F11`：映射完全禁用
- `F12`：重置程序状态

如果提示“快捷键注册失败”，通常是按键被其他软件占用。关闭冲突软件后重启即可。

## 从源码构建

1. 打开 `TypeSimulator/TypeSimulator.sln`
2. 选择 `Release | Any CPU`
3. 构建输出目录：`TypeSimulator/TypeSimulator/bin/Release`

## 目录说明

- `TypeSimulator/TypeSimulator/Models`：核心逻辑（打字引擎、设置、映射）
- `TypeSimulator/TypeSimulator/Services`：系统交互（剪贴板、键盘钩子、日志）
- `TypeSimulator/TypeSimulator/MainWindow.xaml`：界面布局
- `TypeSimulator/TypeSimulator/MainWindow.xaml.cs`：界面事件与流程控制

## 免责声明

本项目用于输入回放与演示自动化，请在合法、合规和授权范围内使用。

## 致谢

- 原项目作者：`YinChingZ`
- 原项目地址：[YinChingZ/TypeSimulator](https://github.com/YinChingZ/TypeSimulator)
