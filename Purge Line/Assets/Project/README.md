# _Project 文件夹结构说明

本文件夹包含项目自定义资源，按照功能和类型进行组织。

## 文件夹结构

```
_Project/
├── Art/                    # 美术资源
│   ├── Sprites/           # 2D精灵
│   ├── Tilemaps/          # 瓦片地图
│   ├── Animations/        # 动画资源
│   ├── Materials/         # 材质
│   └── Shaders/           # 着色器
├── Audio/                 # 音频资源
│   ├── Music/             # 背景音乐
│   ├── SFX/               # 音效
│   └── Ambient/           # 环境音
├── Prefabs/               # 预制体
│   ├── Gameplay/          # 游戏玩法
│   ├── UI/                # 用户界面
│   └── VFX/               # 特效
├── ScriptableObjects/     # 配置数据
│   ├── TowerConfigs/      # 防御塔配置
│   ├── EnemyConfigs/      # 敌人配置
│   ├── LevelConfigs/      # 关卡配置
│   └── ...
├── Scenes/                # 场景
└── Settings/              # 项目设置
```

## 资源命名规范

详见 NAMING_CONVENTIONS.md

## 迁移记录

- 2024-03-12: 初始文件夹结构创建
- Double (128px) → Art/Tilemaps/Tiles/Dungeon
- Tiles → Art/Tilemaps/Tiles/Grassland
- Materials → Art/Materials
- LevelConfigs → ScriptableObjects/LevelConfigs
- Data → ScriptableObjects/Data
