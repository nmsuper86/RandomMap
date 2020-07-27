using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class LevelStats : ScriptableObject
{
    public float mainRoomCutOff = 2;            //看作保留房间阈值
    public float perFromGraphToPaths = 0.1f;    //路径转换率
    public int numberOfCells = 10;              //生成时的房间数量
    public float cellMinWidth = 3;              //房间最小宽度
    public float cellMaxWidth = 6;              //房间最大宽度
    public float cellMinHeight = 3;             //房间最小高度
    public float cellMaxHeight = 6;             //房间最大高度
    public float roomCircleRadius = 10;         //生成范围
}
