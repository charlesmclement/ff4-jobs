using Il2CppSystem.Input;
using Device=Il2CppSystem.Input.KeyConfig.KeyValue.InputDeviceType;
using ActionType=Il2CppSystem.Input.InputSystemManager.InputActionType;

namespace FF4Jobs;

// Query logical keys through FF4's current key configuration for every device.
// InputListener routes gamepad keys through GetInputActionType(keyName), so
// rebinding is honored immediately. Polling raw Enter/Cancel actions as well
// would keep the default buttons active and make swapped bindings conflict.
// Our modal owns input while open, so bypass only the native UI input block.
internal static class MenuInput
{
    private static Key KeyFor(ActionType action)=>action switch
    {
        ActionType.Enter=>Key.Action,ActionType.Cancel=>Key.Cancel,
        ActionType.Up=>Key.Up,ActionType.Down=>Key.Down,ActionType.Left=>Key.Left,ActionType.Right=>Key.Right,
        _=>throw new ArgumentOutOfRangeException(nameof(action))
    };
    internal static bool Down(ActionType action)=>
        InputListener.Instance?.GetKeyDown(KeyFor(action),Device.None,false,true)==true;
    internal static bool Held(ActionType action)=>
        InputListener.Instance?.GetKey(KeyFor(action),Device.None,true)==true;
}
