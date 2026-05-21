# 废土家园 (WastelandHaven)

> 塔防 x 资源采集生产 x 主城建造 | Unity 3D 游戏

## 项目简介

在怪物肆虐的末日世界中，采集资源、生产防御物资、建造你的末日家园。

### 三大核心玩法

| 玩法 | 说明 | 核心机制 |
|------|------|----------|
| **塔防** | 主动挑战关卡，抵御怪物入侵 | 关卡制：每关5-12波，逢5小Boss/逢10大Boss |
| **资源采集** | 种菜、开矿、伐木收集原材料 | **真实时间**产出（支持离线补偿） |
| **物资生产** | 将材料加工为防御塔和弹药等成品 | 生产建筑加工链 |
| **主城建造** | 按图纸自由编辑你的末日家园 | 大Boss关掉落图纸 → 城市编辑界面 |

## 核心循环

```
资源采集(种菜/开矿/伐木) → 物资生产(制造防御塔) 
    → 关卡战斗(获图纸+掉落物) → 主城建造(获加成) → 效率提升 → 循环
```

## 技术栈

- **引擎**: Unity 2022.3 LTS+
- **语言**: C#
- **架构**: 单例管理器 + 事件总线 + 数据驱动配置(ScriptableObject)

## 项目结构

```
Assets/_Project/
├── Scripts/
│   ├── Core/                    # 核心框架
│   │   ├── Managers/            # GameManager, TimeManager, DataManager
│   │   ├── Base/                # Singleton基类, Events事件系统
│   │   └── Utils/               # ObjectPool, 工具函数
│   ├── GameData/                # 枚举定义(ResourceType, BuildingType, etc.)
│   ├── ResourceSystem/          # 资源采集系统 (ResourceHarvestManager)
│   ├── ProductionSystem/        # 物资生产系统 (ProductionManager)
│   ├── TowerDefense/            # 塔防系统 (TowerDefenseManager)
│   ├── CityBuilder/             # 主城建造系统 (CityBuilderManager)
│   ├── UI/                      # 界面系统 (UIManager + 各Panel)
│   └── Configs/                 # 配置表SO模板 (GameConfig/HarvestConfig/etc.)
├── Prefabs/                     # 预制件（待添加）
├── Scenes/                      # 场景文件（待创建）
└── ScriptableObjects/           # 可编程配置数据
```

## 快速开始

### 1. 用Unity Hub打开项目

1. 打开 Unity Hub
2. 点击 "Add" 添加本目录 (`d:\Github\First-Game-Demo`)
3. 选择项目打开（Unity会自动识别为Unity项目）
4. 如果需要，安装 Unity 2022.3 LTS

### 2. 首次运行

1. 创建启动场景 `BootScene`（或从 Scenes 目录打开）
2. 在场景中创建一个空GameObject，挂载 `GameManager` 脚本
3. 运行游戏 - 控制台应显示 `[GameManager] 系统初始化完成`

### 3. 核心脚本说明

| 脚本 | 文件路径 | 职责 |
|------|---------|------|
| GameManager | Core/Managers/ | 游戏入口，生命周期管理，状态切换 |
| TimeManager | Core/Managers/ | 真实时间机制，离线计算，倒计时格式化 |
| DataManager | Core/Managers/ | 存档读写(JSON序列化)，资源增减查 |
| ResourceHarvestManager | ResourceSystem/ | 5种采集建筑管理，一键收获，升级 |
| ProductionManager | ProductionSystem/ | 配方管理，生产队列，订单取消/收取 |
| TowerDefenseManager | TowerDefense/ | 关卡流程，波次生成，评级结算，掉落 |
| CityBuilderManager | CityBuilder/ | 图纸应用，网格放置，加成计算 |
| UIManager | UI/ | 面板注册/切换，HUD更新 |

### 4. 待实现功能（TODO标记）

各脚本中标注了 `// TODO:` 的位置是需要后续完善的功能点：

- [ ] 从JSON/SO加载配置数据（目前有硬编码示例数据）
- [ ] 完善对象池实现（目前是框架代码）
- [ ] 实现UI面板的完整交互逻辑
- [ ] 实现塔防战斗的具体逻辑（寻路、攻击、AI）
- [ ] 实现主城编辑的视觉反馈（网格高亮、拖拽预览等）
- [ ] 接入Newtonsoft.Json包（DataManager依赖）

## 设计文档

详细的游戏设计方案请查看: [`废土家园-游戏设计方案-v1.0.md`](../废土家园-游戏设计方案-v1.0.md)

## 开发路线图

| 阶段 | 目标 | 状态 |
|------|------|------|
| v0.1 | 项目框架搭建 | ✅ 进行中 |
| v0.2 | 最小原型：农田收获→熔炉生产→简单位置塔→打一波怪 | 📋 下一步 |
| v0.3 | 完整采集系统（5种建筑） | 📋 |
| v0.4 | 完整生产系统（8种建筑+配方） | 📋 |
| v0.5 | 完整塔防（5类塔+波次机制+Boss） | 📋 |
| v0.6 | 主城建造（图纸+编辑界面） | 📋 |
| v0.7 | UI完善 + 新手引导 | 📋 |
| v0.8 | 平衡调优 + 内容填充 | 📋 |

---

© 2026 废土家园开发团队
