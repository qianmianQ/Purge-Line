// using R3;
// using UnityEngine;
// using UnityEngine.InputSystem;
//
// namespace Base.BaseSystem.InputSystem
// {
//     // ====================================================================
//     // 全局输入上下文 - 始终启用，处理全局快捷键
//     // ====================================================================
//
//     /// <summary>
//     /// 全局输入上下文
//     /// 始终在栈底，处理全局快捷键（如截屏、调试、暂停等）
//     /// </summary>
//     public class GlobalInputContext : InputContextBase
//     {
//         public GlobalInputContext() : base("Global")
//         {
//         }
//
//         protected override void OnContextActivated()
//         {
//             base.OnContextActivated();
//
//             // 绑定全局快捷键
//             // 这里可以添加全局热键，如 F12 截屏、F5 重载等
//         }
//
//         protected override void OnContextDeactivated()
//         {
//             base.OnContextDeactivated();
//         }
//     }
//
//     // ====================================================================
//     // UI输入上下文 - 用于菜单、对话框等UI场景
//     // ====================================================================
//
//     /// <summary>
//     /// UI输入上下文
//     /// 处理导航、确认、取消等UI交互
//     /// </summary>
//     public class UIInputContext : InputContextBase
//     {
//         private InputAction _navigateAction;
//         private InputAction _confirmAction;
//         private InputAction _cancelAction;
//         private InputAction _tabAction;
//
//         public UIInputContext() : base("UI")
//         {
//             CreateDefaultActions();
//         }
//
//         private void CreateDefaultActions()
//         {
//             // 创建默认的UI操作
//             var map = new InputActionMap("UI");
//
//             _navigateAction = map.AddAction("Navigate", type: InputActionType.Value, binding: "<Gamepad>/leftStick");
//             _navigateAction.AddBinding("<Keyboard>/w");
//             _navigateAction.AddBinding("<Keyboard>/a");
//             _navigateAction.AddBinding("<Keyboard>/s");
//             _navigateAction.AddBinding("<Keyboard>/d");
//             _navigateAction.AddBinding("<Keyboard>/upArrow");
//             _navigateAction.AddBinding("<Keyboard>/downArrow");
//             _navigateAction.AddBinding("<Keyboard>/leftArrow");
//             _navigateAction.AddBinding("<Keyboard>/rightArrow");
//
//             _confirmAction = map.AddAction("Confirm", type: InputActionType.Button, binding: "<Gamepad>/buttonSouth");
//             _confirmAction.AddBinding("<Keyboard>/enter");
//             _confirmAction.AddBinding("<Mouse>/leftButton");
//
//             _cancelAction = map.AddAction("Cancel", type: InputActionType.Button, binding: "<Gamepad>/buttonEast");
//             _cancelAction.AddBinding("<Keyboard>/escape");
//
//             _tabAction = map.AddAction("Tab", type: InputActionType.Button, binding: "<Keyboard>/tab");
//         }
//
//         public InputAction NavigateAction => _navigateAction;
//         public InputAction ConfirmAction => _confirmAction;
//         public InputAction CancelAction => _cancelAction;
//         public InputAction TabAction => _tabAction;
//
//         protected override void OnContextActivated()
//         {
//             base.OnContextActivated();
//
//             // 启用UI操作
//             _navigateAction?.Enable();
//             _confirmAction?.Enable();
//             _cancelAction?.Enable();
//             _tabAction?.Enable();
//         }
//
//         protected override void OnContextDeactivated()
//         {
//             base.OnContextDeactivated();
//
//             // 禁用UI操作
//             _navigateAction?.Disable();
//             _confirmAction?.Disable();
//             _cancelAction?.Disable();
//             _tabAction?.Disable();
//         }
//     }
//
//     // ====================================================================
//     // 游戏玩法输入上下文 - 用于核心游戏操作
//     // ====================================================================
//
//     /// <summary>
//     /// 游戏玩法输入上下文
//     /// 处理移动、攻击、技能等游戏核心操作
//     /// </summary>
//     public class GameplayInputContext : InputContextBase
//     {
//         private InputAction _moveAction;
//         private InputAction _lookAction;
//         private InputAction _attackAction;
//         private InputAction _skillAction;
//         private InputAction _interactAction;
//         private InputAction _pauseAction;
//
//         public GameplayInputContext() : base("Gameplay")
//         {
//             CreateDefaultActions();
//         }
//
//         private void CreateDefaultActions()
//         {
//             var map = new InputActionMap("Gameplay");
//
//             // 移动
//             _moveAction = map.AddAction("Move", type: InputActionType.Value, binding: "<Gamepad>/leftStick");
//             _moveAction.AddBinding("<Keyboard>/w");
//             _moveAction.AddBinding("<Keyboard>/a");
//             _moveAction.AddBinding("<Keyboard>/s");
//             _moveAction.AddBinding("<Keyboard>/d");
//             _moveAction.AddCompositeBinding("2DVector")
//                 .With("Up", "<Keyboard>/w")
//                 .With("Down", "<Keyboard>/s")
//                 .With("Left", "<Keyboard>/a")
//                 .With("Right", "<Keyboard>/d");
//
//             // 视角/瞄准
//             _lookAction = map.AddAction("Look", type: InputActionType.Value, binding: "<Gamepad>/rightStick");
//             _lookAction.AddBinding("<Mouse>/delta");
//
//             // 攻击
//             _attackAction = map.AddAction("Attack", type: InputActionType.Button, binding: "<Gamepad>/rightTrigger");
//             _attackAction.AddBinding("<Mouse>/leftButton");
//
//             // 技能
//             _skillAction = map.AddAction("Skill", type: InputActionType.Button, binding: "<Gamepad>/leftTrigger");
//             _skillAction.AddBinding("<Mouse>/rightButton");
//             _skillAction.AddBinding("<Keyboard>/space");
//
//             // 交互
//             _interactAction = map.AddAction("Interact", type: InputActionType.Button, binding: "<Gamepad>/buttonNorth");
//             _interactAction.AddBinding("<Keyboard>/e");
//             _interactAction.AddBinding("<Keyboard>/f");
//
//             // 暂停
//             _pauseAction = map.AddAction("Pause", type: InputActionType.Button, binding: "<Gamepad>/start");
//             _pauseAction.AddBinding("<Keyboard>/escape");
//             _pauseAction.AddBinding("<Keyboard>/p");
//         }
//
//         public InputAction MoveAction => _moveAction;
//         public InputAction LookAction => _lookAction;
//         public InputAction AttackAction => _attackAction;
//         public InputAction SkillAction => _skillAction;
//         public InputAction InteractAction => _interactAction;
//         public InputAction PauseAction => _pauseAction;
//
//         protected override void OnContextActivated()
//         {
//             base.OnContextActivated();
//
//             // 启用游戏玩法操作
//             _moveAction?.Enable();
//             _lookAction?.Enable();
//             _attackAction?.Enable();
//             _skillAction?.Enable();
//             _interactAction?.Enable();
//             _pauseAction?.Enable();
//         }
//
//         protected override void OnContextDeactivated()
//         {
//             base.OnContextDeactivated();
//
//             // 禁用游戏玩法操作
//             _moveAction?.Disable();
//             _lookAction?.Disable();
//             _attackAction?.Disable();
//             _skillAction?.Disable();
//             _interactAction?.Disable();
//             _pauseAction?.Disable();
//         }
//     }
// }
