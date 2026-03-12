# 新资源导入指南

## 快速开始

1. 确定资源类型（精灵、音频、预制体等）
2. 根据类型放入对应文件夹
3. 按照命名规范重命名
4. 更新相关配置（如需）

## 资源分类速查表

| 资源类型 | 目标文件夹 | 命名示例 |
|---------|-----------|---------|
| 2D精灵（环境） | Art/Sprites/Environment/ | `env_tree_oak_01` |
| 2D精灵（角色） | Art/Sprites/Characters/ | `char_enemy_goblin_01` |
| 瓦片（地牢） | Art/Tilemaps/Tiles/Dungeon/ | `tile_dungeon_floor_center` |
| 瓦片（草地） | Art/Tilemaps/Tiles/Grassland/ | `tile_grass_path_NS` |
| 材质 | Art/Materials/ | `mat_ground_grass` |
| 动画片段 | Art/Animations/Clips/ | `anim_tower_archer_idle` |
| 动画控制器 | Art/Animations/Controllers/ | `controller_tower_archer` |
| 背景音乐 | Audio/Music/ | `music_battle_intense` |
| 音效 | Audio/SFX/ | `sfx_tower_archer_shoot_01` |
| 预制体（防御塔） | Prefabs/Gameplay/Towers/ | `prefab_tower_archer_L01` |
| 预制体（敌人） | Prefabs/Gameplay/Enemies/ | `prefab_enemy_goblin_fast` |
| 预制体（UI） | Prefabs/UI/ | `prefab_ui_button_main` |
| 配置（防御塔） | ScriptableObjects/TowerConfigs/ | `config_tower_archer` |
| 配置（关卡） | ScriptableObjects/LevelConfigs/ | `config_level_01` |
| 场景 | Scenes/ | `Level_01_Forest` |

## 导入步骤详解

### 1. 导入2D精灵

**步骤：**
1. 将图片文件拖入 `Art/Sprites/Environment/` 或相应子文件夹
2. 在Inspector中设置：
   - Texture Type: `Sprite (2D and UI)`
   - Sprite Mode: `Single` 或 `Multiple`（对于图集）
   - Pixels Per Unit: 根据项目设置（通常为32、64或128）
   - Filter Mode: `Point (no filter)`（像素风格）
3. 点击 `Apply`
4. 按照命名规范重命名文件

### 2. 导入瓦片（Tile）

**步骤：**
1. 将瓦片图片放入 `Art/Tilemaps/Tiles/{主题}/`
2. 创建瓦片资源：
   - 在Project窗口右键 → Create → 2D → Tiles → {类型}
   - 常见类型：Rule Tile（规则瓦片）、Animated Tile（动画瓦片）
3. 将精灵分配给瓦片
4. 创建瓦片调色板（Tile Palette）：
   - Window → 2D → Tile Palette
   - 创建新调色板并保存到 `Art/Tilemaps/Palettes/`

### 3. 导入音频

**步骤：**
1. 将音频文件拖入 `Audio/Music/` 或 `Audio/SFX/`
2. 在Inspector中设置：
   
   **背景音乐：**
   - Load Type: `Streaming`（大文件）或 `Compressed In Memory`
   - Compression Format: `Vorbis`
   - Quality: 50-70%
   
   **音效：**
   - Load Type: `Decompress On Load`（小文件，频繁播放）
   - Compression Format: `ADPCM` 或 `PCM`（低延迟）
3. 点击 `Apply`

### 4. 创建预制体

**步骤：**
1. 在场景中组装游戏对象
2. 调整组件和参数
3. 将对象从Hierarchy拖到 `Prefabs/{类别}/`
4. 选择预制体选项：
   - Original Prefab：独立副本
   - Prefab Variant：继承基础预制体的变体
5. 删除场景中的临时对象（如果需要）

### 5. 创建ScriptableObject配置

**步骤：**
1. 确保已创建相应的ScriptableObject脚本
2. 在Project窗口右键 → Create → {配置类别}
3. 保存到 `ScriptableObjects/{类别}/`
4. 在Inspector中配置参数
5. 按照命名规范重命名文件

## 常见问题

### Q: 我应该把资源放在哪个文件夹？

A: 参考上面的"资源分类速查表"。如果不确定，可以先放在 `_Project/` 根目录，然后询问团队成员。

### Q: 如何命名变体资源？

A: 使用编号或描述性后缀：
- 编号变体：`env_tree_oak_01`, `env_tree_oak_02`, `env_tree_oak_03`
- 描述变体：`env_ground_grass_dry`, `env_ground_grass_wet`
- 方向变体：`env_wall_brick_N`, `env_wall_brick_E`, `env_wall_brick_S`, `env_wall_brick_W`

### Q: 可以修改已导入资源的设置吗？

A: 可以。在Project窗口选中资源，在Inspector中修改设置，然后点击 `Apply`。注意：某些更改（如Sprite Mode从Single改为Multiple）可能需要重新配置。

### Q: 如何导入图集（Sprite Sheet）？

A: 
1. 将图集图片导入 `Art/Sprites/{类别}/`
2. 在Inspector中设置 Sprite Mode 为 `Multiple`
3. 点击 `Sprite Editor`
4. 选择切片模式：
   - `Automatic`：自动检测精灵
   - `Grid By Cell Size`：按固定大小切片
   - `Grid By Cell Count`：按行列数切片
5. 点击 `Slice`，然后 `Apply`

### Q: 资源导入后出现模糊/失真？

A: 对于像素艺术风格：
1. 在Inspector中将 Filter Mode 设为 `Point (no filter)`
2. 确保 Compression 设置不会过度压缩
3. 如果精灵显示有缝隙，在 Sprite Editor 中将 Border 设为 0，或在渲染时使用 `Pixel Perfect Camera`

## 相关文档

- [README.md](./README.md) - 文件夹结构说明
- [NAMING_CONVENTIONS.md](./NAMING_CONVENTIONS.md) - 详细命名规范
- 外部资源：
  - [Unity官方文档 - 2D游戏开发](https://docs.unity3d.com/Manual/2DGameDevelopment.html)
  - [Unity官方文档 - 瓦片地图](https://docs.unity3d.com/Manual/Tilemaps.html)
  - [Unity官方文档 - 音频导入](https://docs.unity3d.com/Manual/AudioFiles.html)

## 需要帮助？

如果在导入资源时遇到问题：
1. 查看本文档的"常见问题"部分
2. 查阅Unity官方文档
3. 询问团队成员
4. 在项目管理工具中创建问题（Issue）
