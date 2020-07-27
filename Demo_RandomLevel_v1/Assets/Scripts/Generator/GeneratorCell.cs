using System.Collections;
using System.Collections.Generic;
using UnityEngine;
 
public class GeneratorCell
{
    public int index;
    public int x;
    public int y;

    public int width;
    public int height;

    public bool isMainRoom = true;
    //public bool isPathRoom = false;
    public bool isBossRoom = false;

    /// <summary>
    /// AABB类结构，用于检测碰撞
    /// </summary>
    /// <param name="cell"></param>
    /// <returns></returns>
    public bool CollidesWith(GeneratorCell cell)
    {
        bool value = true;
        if (cell.x >= this.x + this.width ||
            cell.y >= this.y + this.height ||
            cell.x + cell.width <= this.x ||
            cell.y + cell.height <= this.y)
        {
            value = false;
        }

        return value;
    }

    /// <summary>
    /// 移动重叠的房间
    /// </summary>
    /// <param name="shiftX"></param>
    /// <param name="shiftY"></param>
    public void Shift(int shiftX, int shiftY)
    {
        x += shiftX;
        y += shiftY;
    }
}
