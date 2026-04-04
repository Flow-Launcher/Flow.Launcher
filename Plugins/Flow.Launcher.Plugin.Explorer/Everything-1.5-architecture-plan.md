# Everything 1.5 SDK 架构实施计划

本文聚焦前一轮 review 中的第 3、4、5 点：
- `EverythingSearchManager` 职责过多
- V1/V3 搜索流程重复，容易漂移
- V3 错误模型过于扁平

## 3. `EverythingSearchManager` 职责拆分计划

### 目标
把当前 `EverythingSearchManager` 中的工厂、运行时初始化、可用性探测、安装引导、Provider 适配几类职责拆开，降低后续维护成本。

### 当前问题
`EverythingSearchManager` 同时负责：
- 选择 `LegacyEverythingApi` / `EverythingApiV3`
- 加载 `Everything.dll` / `Everything3.dll`
- 检测 Everything 是否可连接
- 处理“未安装/未启动”的用户提示与安装引导
- 对外实现 `IIndexProvider` / `IContentIndexProvider` / `IPathIndexProvider`

这会导致任何 SDK 演进都集中修改一个类。

### 拆分方案

#### 3.1 引入 `EverythingApiFactory`
职责：
- 根据设置决定创建 `LegacyEverythingApi` 还是 `EverythingApiV3`
- 统一实例名规范化逻辑

建议接口：
- `IEverythingApi Create(Settings settings)`

迁移内容：
- 从 `EverythingSearchManager` 中移出：
  - `CreateApi(...)`
  - `GetNormalizedInstanceName(...)`

#### 3.2 引入 `EverythingSdkLoader`
职责：
- 负责 native DLL 的一次性加载
- 隐藏 `EverythingApiDllImport.Load(...)` / `Everything3ApiDllImport.Load(...)` 细节
- 维护线程安全

建议接口：
- `void EnsureLoaded(string sdkDirectory, bool useV3Api)`

迁移内容：
- 从 `EverythingSearchManager` 中移出：
  - `_dllSemaphore`
  - `LoadConfiguredDll(...)`

#### 3.3 引入 `EverythingAvailabilityService`
职责：
- 负责“是否可用”的判定
- 根据异常类型生成统一的可用性错误
- 保留安装/启动引导逻辑

建议接口：
- `ValueTask EnsureAvailableAsync(IEverythingApi api, Settings settings, CancellationToken token = default)`

迁移内容：
- 从 `EverythingSearchManager` 中移出：
  - `ThrowIfEverythingNotAvailableAsync(...)`
  - `ClickToInstallEverythingAsync(...)`

#### 3.4 缩减 `EverythingSearchManager`
保留职责：
- 组装 `EverythingSearchOption`
- 调用 `api.SearchAsync(...)`
- 作为 Explorer 插件内的 Everything Provider 门面

最终效果：
- `EverythingSearchManager` 更接近应用层协调器
- SDK、运行时、错误与 UX 引导分别归位

### 落地步骤
1. 新建 `EverythingApiFactory`
2. 新建 `EverythingSdkLoader`
3. 新建 `EverythingAvailabilityService`
4. 让 `EverythingSearchManager` 通过这些组件完成现有逻辑
5. 保持外部调用接口不变，避免影响 `Settings` / `SearchManager`

### 验收标准
- `EverythingSearchManager` 文件长度和私有职责显著减少
- DLL 加载和 API 创建不再散落在 manager 中
- 可用性判断逻辑有独立入口
- `SearchManager` 和 UI 层无须理解 SDK 版本差异

---

## 4. V1/V3 搜索流程去重计划

### 目标
提取 Everything 1.4 与 1.5 共用的查询构造和结果映射逻辑，避免双实现长期漂移。

### 当前重复点
`LegacyEverythingApi.SearchAsync(...)` 与 `EverythingApiV3.SearchAsync(...)` 目前重复了以下逻辑：
- `Offset` / `MaxCount` 参数校验
- `@` 前缀触发 regex
- 关键字、父路径、content search 的查询字符串拼接
- 部分 cancellation 入口
- `SearchResult` 组装思路

### 拆分方案

#### 4.1 引入 `EverythingQueryBuilder`
职责：
- 接收 `EverythingSearchOption`
- 输出标准化查询描述

建议输出模型：
- `SearchText`
- `UseRegex`

建议类型：
- `EverythingPreparedQuery`
- `EverythingQueryBuilder`

示例职责边界：
- 校验 `Offset` / `MaxCount`
- 处理 `@` 前缀
- 构建最终 search text
- 不触碰任何 native API

#### 4.2 引入 `EverythingResultMapper`
职责：
- 统一把 native SDK 返回值映射成 `SearchResult`
- 统一 highlight 字符串转换入口

说明：
- V1/V3 的底层字段读取方式不同，不强行抽象整个 native 读取过程
- 但可把“构建 `SearchResult` 的约定”收敛起来

可拆分成两层：
- 共用层：`CreateResult(string fullPath, ResultType type, int score, List<int> highlightData)`
- SDK 适配层：各自读取 full path / run count / highlight text

#### 4.3 保留各 SDK 的最小差异面
`LegacyEverythingApi` 保留：
- V1 专属 request flags
- `Everything_Reset()` 生命周期
- V1 的 native 错误检查

`EverythingApiV3` 保留：
- search state / result list / client 生命周期
- property request / property sort 转换
- V3 的 native 错误检查

### 落地步骤
1. 新建 `EverythingPreparedQuery` 模型
2. 新建 `EverythingQueryBuilder`
3. 把 V1/V3 中重复的 query 构造逻辑迁移到 builder
4. 提取 `SearchResult` 组装辅助方法或 mapper
5. 保证 V1/V3 两边行为一致后，再清理重复代码

### 验收标准
- `LegacyEverythingApi.SearchAsync(...)` 与 `EverythingApiV3.SearchAsync(...)` 不再各自拼接 query string
- regex 与 content search 规则只有一处定义
- 新增搜索语法时，不需要双份修改

---

## 5. V3 错误模型细化计划

### 目标
把 Everything 1.5 的“连接失败”从单一布尔值扩展为可区分的错误结果，使上层能够给出更准确的恢复策略和提示。

### 当前问题
`TryConnectEverything3(out var client)` 当前只返回 `true/false`，会把以下情况混在一起：
- `Everything3.dll` 丢失
- DLL 版本不兼容，缺少入口点
- 实例名错误或实例不存在
- Everything 1.5 服务未运行
- IPC 断开

上层只能统一处理为“Everything 不可用”，提示粒度不足。

### 细化方案

#### 5.1 引入连接结果模型
建议类型：
- `EverythingConnectionStatus`
- `EverythingConnectionResult`

建议状态：
- `Success`
- `SdkMissing`
- `SdkIncompatible`
- `InstanceNotFound`
- `ServiceUnavailable`
- `Disconnected`
- `UnknownFailure`

建议字段：
- `Status`
- `Client`
- `Exception`
- `Message`（可选）

#### 5.2 替换 `TryConnectEverything3(...)`
当前：
- `bool TryConnectEverything3(out IntPtr client)`

建议替换为：
- `EverythingConnectionResult ConnectEverything3()`

收益：
- `IsEverythingRunningAsync()` 可以区分“没装 SDK”和“服务没启动”
- `IsFastSortOption(...)` 可以在实例名错误时给出更准的异常
- `SearchAsync(...)` 可以把失败转成更合理的业务异常

#### 5.3 建立 V3 错误翻译层
建议新增：
- `Everything3ErrorTranslator`

职责：
- 解析 `Everything3_GetLastError()`
- 把 native error code 翻译成领域异常或连接状态

目标：
- `EverythingApiV3` 不再直接分散处理所有错误码
- 错误语义集中定义

#### 5.4 上层异常映射策略
在 `EverythingSearchManager` / 可用性服务中约定：
- `SdkMissing` / `SdkIncompatible` -> SDK 问题提示
- `InstanceNotFound` -> 提示检查 `Everything15InstanceName`
- `ServiceUnavailable` / `Disconnected` -> 提示启动 Everything 1.5

### 落地步骤
1. 新建连接状态枚举与结果模型
2. 重写 `TryConnectEverything3(...)` 为结构化返回
3. 新建 `Everything3ErrorTranslator`
4. 调整 `IsEverythingRunningAsync` / `SearchAsync` / `IsFastSortOption`
5. 调整上层提示逻辑，使 instance name 配置错误可被识别

### 验收标准
- V3 连接失败不再只表现为 `false`
- UI 层能够区分“SDK 缺失”和“实例不可达”
- 用户配置错实例名时，得到的是针对性提示而不是泛化提示

---

## 推荐实施顺序
1. **先做第 5 点**：先把错误模型立住，否则后续拆分职责时仍会传递模糊状态
2. **再做第 3 点**：将连接、初始化、可用性逻辑拆出 manager
3. **最后做第 4 点**：在职责边界稳定后，再抽查询构造和结果映射，风险更低

## 预期收益
- 降低 `EverythingSearchManager` 的变更面
- 减少 V1/V3 功能演进时的不一致风险
- 为 Everything 1.5 实例名、多实例、兼容性提示提供更稳固的基础设施
