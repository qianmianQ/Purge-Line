# 资源命名规范

## 通用原则

1. 使用小写字母和下划线（snake_case）
2. 使用描述性名称，避免缩写
3. 使用一致的命名模式
4. 避免使用空格和特殊字符

## 精灵命名（Sprites）

### 环境元素
```
env_{类型}_{变体}_{方向}
```

示例：
- `env_ground_grass_N` - 草地（北向）
- `env_wall_brick_E` - 砖墙（东向）
- `env_water_river_S` - 河流（南向）
- `env_tree_oak_01` - 橡树（变体1）
- `env_bush_small_02` - 小灌木（变体2）

### 角色
```
char_{角色名}_{动作}_{帧号}
```

示例：
- `char_tower_archer_idle_01` - 弓箭塔待机帧1
- `char_enemy_goblin_walk_02` - 哥布林行走帧2
- `char_hero_mage_attack_03` - 法师攻击帧3

### 物品
```
item_{类型}_{名称}_{变体}
```

示例：
- `item_weapon_sword_01` - 剑（变体1）
- `item_potion_health_small` - 小生命药水
- `item_coin_gold` - 金币

## 瓦片命名（Tiles）

```
tile_{主题}_{类型}_{连接类型}
```

示例：
- `tile_dungeon_floor_center` - 地牢地板（中心）
- `tile_grass_path_NS` - 草地方径（南北向）
- `tile_water_edge_E` - 水边缘（东向）
- `tile_castle_wall_corner_NW` - 城堡墙角落（西北）

## 预制体命名（Prefabs）

```
prefab_{类别}_{名称}_{变体}
```

示例：
- `prefab_tower_archer_L01` - 弓箭塔（等级1）
- `prefab_enemy_goblin_fast` - 快速哥布林
- `prefab_env_tree_oak_01` - 橡树环境物体

## 配置命名（ScriptableObjects）

```
config_{类型}_{名称}
```

示例：
- `config_tower_archer` - 弓箭塔配置
- `config_enemy_goblin` - 哥布林配置
- `config_level_01` - 关卡1配置
- `config_wave_boss_01` - Boss波次配置
- `config_ability_fireball` - 火球技能配置

## 场景命名（Scenes）

```
{类别}_{名称}_{变体}
```

示例：
- `Boot_Initialize` - 启动初始化场景
- `MainMenu_Title` - 主菜单场景
- `Level_01_Forest` - 关卡1（森林）
- `Level_02_Dungeon` - 关卡2（地牢）
- `UI_HUD_Test` - HUD测试场景

## 音频命名（Audio）

### 音乐
```
music_{场景}_{情绪}
```

示例：
- `music_battle_intense` - 战斗音乐（激烈）
- `music_ambient_calm` - 环境音乐（平静）
- `music_menu_theme` - 菜单主题音乐

### 音效
```
sfx_{类别}_{动作}_{变体}
```

示例：
- `fx_tower_archer_shoot_01` - 弓箭塔射击声
- `sfx_enemy_goblin_death` - 哥布林死亡声
- `sfx_ui_button_click` - UI按钮点击声
- `sfx_weapon_sword_hit` - 剑击声

## 材质命名（Materials）

```
mat_{对象}_{属性}
```

示例：
- `mat_ground_grass` - 草地材质
- `mat_wall_stone_damaged` - 损坏的石墙材质
- `mat_water_river_flowing` - 流动河水材质

## 动画命名（Animations）

### 动画片段
```
anim_{对象}_{动作}
```

示例：
- `anim_tower_archer_idle` - 弓箭塔待机动画
- `anim_tower_archer_attack` - 弓箭塔攻击动画
- `anim_enemy_goblin_walk` - 哥布林行走动画
- `anim_enemy_goblin_death` - 哥布林死亡动画
- `anim_effect_explosion` - 爆炸特效动画

### 动画控制器
```
controller_{对象}
```

示例：
- `controller_tower_archer` - 弓箭塔动画控制器
- `controller_enemy_goblin` - 哥布林动画控制器

## 着色器命名（Shaders）

```
shader_{效果}_{变体}
```

示例：
- `shader_sprite_default` - 默认精灵着色器
- `shader_sprite_outline` - 描边精灵着色器
- `shader_water_flowing` - 流动水着色器
- `shader_dissolve_effect` - 溶解效果着色器

## 文件夹命名

文件夹使用大驼峰命名（PascalCase）：

```
FolderName/
```

示例：
- `Sprites/` - 精灵文件夹
- `Tilemaps/` - 瓦片地图文件夹
- `Animations/` - 动画文件夹
- `ScriptableObjects/` - 可编写对象文件夹

## 版本控制标签

对于需要版本区分的资源：

```
{名称}_v{版本号}
```

示例：
- `env_tree_oak_v01` - 橡树版本1
- `env_tree_oak_v02` - 橡树版本2
- `config_level_01_v03` - 关卡1配置版本3

## 临时/测试资源

临时或测试用资源使用前缀：

```
temp_{名称}
test_{名称}
```

示例：
- `temp_placeholder_tree` - 临时占位树
- `test_animation_walk` - 测试行走动画

注意：临时资源不应提交到版本控制，或在提交前删除。

## 命名检查清单

在命名资源时，请确认：

- [ ] 使用了小写字母和下划线
- [ ] 名称具有描述性，能清楚表达资源用途
- [ ] 遵循了对应的命名模式
- [ ] 没有使用空格或特殊字符
- [ ] 长度适中，既不太短（难以理解）也不太长（难以输入）

## 常见错误

❌ 错误：`New Sprite 1.png`
✅ 正确：`env_tree_oak_01.png`

❌ 错误：`tower_archer`
✅ 正确：`prefab_tower_archer_L01`

❌ 错误：`Level 1`
✅ 正确：`Level_01_Forest`

❌ 错误：`audio file`
✅ 正确：`sfx_tower_shoot_01.wav`
