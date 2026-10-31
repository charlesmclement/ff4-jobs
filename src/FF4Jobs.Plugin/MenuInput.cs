using Il2CppSystem.Input;
using Device=Il2CppSystem.Input.KeyConfig.KeyValue.InputDeviceType;
using ActionType=Il2CppSystem.Input.InputSystemManager.InputActionType;

namespace FF4Jobs;

// FF4 uses both its legacy keyboard bindings and its newer controller actions.
// Our modal owns input while open, so query the legacy binding past UI blocking.
internal static class MenuInput
{
    private static Key KeyFor(ActionType action)=>action switch
    {
        ActionType.Enter=>Key.Action,ActionType.Cancel=>Key.Cancel,
        ActionType.Up=>Key.Up,ActionType.Down=>Key.Down,ActionType.Left=>Key.Left,ActionType.Right=>Key.Right,
        _=>throw new ArgumentOutOfRangeException(nameof(action))
    };
    internal static bool Down(ActionType action)=>InputSystemManager.Instance?.GetKeyDown(action)==true
        || InputListener.Instance?.GetKeyDown(KeyFor(action),(Device)3,false,true)==true;
    internal static bool Held(ActionType action)=>InputSystemManager.Instance?.GetKey(action)==true
        || InputListener.Instance?.GetKey(KeyFor(action),(Device)3,true)==true;
}
