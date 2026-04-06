# TypeSimulator V8 (Enhanced)

基于 [YinChingZ/TypeSimulator](https://github.com/YinChingZ/TypeSimulator) 的增强版本，聚焦“更像人类输入”的回放体验和更现代化 UI。

## 主要增强

- 可调错字概率（1%~90%）
- 错字后可选额外停顿（0.5s~1s）+ 触发概率
- 偶发双击字符后退格修正
- 整词删改（多次 Backspace 兼容模式）
- 节奏曲线（开头慢 / 中段快 / 结尾慢）可开关 + 强度可调
- 疲劳曲线可开关 + 强度可调
- 实时进度条 + 预计剩余时长
- 设置自动保存/加载
- 全新 V8 UI（卡片化布局、状态信息增强、视觉优化）

## 环境要求

- Windows 10/11
- .NET Framework 4.8
- 建议管理员权限运行（全局热键/输入模拟更稳定）

## 快速使用

1. 打开软件后粘贴文本
2. 调整速度和错字/曲线参数
3. 点击“开始打字”或使用快捷键

## 快捷键提示

- `Ctrl + Alt + T`：打字开始/暂停
- `F10`：映射暂停/恢复
- `F11`：映射完全禁用
- `F12`：重置应用状态

如出现“快捷键注册失败”，通常是快捷键被其它软件占用，可关闭冲突软件或改键后再试。

## 构建方式

1. 用 Visual Studio 打开 `TypeSimulator/TypeSimulator.sln`
2. 选择 `Release | Any CPU`
3. 生成后产物在 `TypeSimulator/TypeSimulator/bin/Release`

## 致谢

- 原项目作者：`YinChingZ`
- 原项目地址：[https://github.com/YinChingZ/TypeSimulator](https://github.com/YinChingZ/TypeSimulator)

---

如果你是从本仓库下载的预编译包，优先使用 Release 页面中的最新版本。
