namespace FF4Jobs.Core;

public static class HandEquipment
{
    public const int Empty=0, Shield=-1, Bow=12, Arrow=13;
    public static bool AllowsShields(string main) => main is "paladin" or "engineer" or "dragoon";
    // Empty hands are allowed while assembling/changing equipment. An unloaded
    // bow keeps FF4's native ineffective attack; it never becomes a second bow.
    public static bool PairAllowed(int first,int second,bool firstTwoHanded,bool secondTwoHanded,bool dual,bool shields)
    {
        if(first==Empty || second==Empty) return true;
        if(first is Bow or Arrow || second is Bow or Arrow)
            return (first==Bow && second==Arrow) || (first==Arrow && second==Bow);
        if(firstTwoHanded || secondTwoHanded) return false;
        if(first==Shield && second==Shield) return shields;
        if(first==Shield || second==Shield) return true;
        return dual;
    }
}
