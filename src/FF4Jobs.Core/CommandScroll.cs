using System;

namespace FF4Jobs.Core;

public static class CommandScroll
{
    // Keep a native row index inside the existing window while moving the
    // viewport through the complete command list.
    public static (int Start, int Row) Move(int start, int row, int count, int rows, int direction, bool loop=false)
    {
        if(count<=0 || rows<=0) return (0,0);
        int selected=start+row+Math.Sign(direction);
        selected=loop ? (selected+count)%count : Math.Clamp(selected,0,count-1);
        start=Math.Clamp(start,0,Math.Max(0,count-rows));
        if(selected<start) start=selected;
        if(selected>=start+rows) start=selected-rows+1;
        return (start,selected-start);
    }
}
