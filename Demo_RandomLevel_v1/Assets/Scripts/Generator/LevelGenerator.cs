using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Delaunay.Geo;
using Delaunay;

[CreateAssetMenu]
public class LevelGenerator : ScriptableObject
{
    public List<GeneratorCell> cells = new List<GeneratorCell>();
    public List<LineSegment> delaunayLines = new List<LineSegment>();
    public List<LineSegment> spanningTree = new List<LineSegment>();

    public List<Path> paths = new List<Path>();

    float widthAvg = 0;
    float heightAvg = 0;

    float mainRoomMeanCutoff = 5;
    float percFromGraphToPaths = 0.1f;  //路径转化百分比  

    public LevelStats levelStats;

    //定义委托
    public delegate void LevelGeneractionComplete();
    public LevelGeneractionComplete OnLevelGenerationComplete;

    float maxX = 0f;
    float maxY = 0f;
    float minX = 0f;
    float minY = 0f;

    public bool debug;

    public void GenerateLevel()
    {
        LevelGeneratorHeader header = LevelGeneratorHeader.singleton;

        if (header == null)
        {
            GameObject go = new GameObject();
            go.name = "Level Generator Header";
            header = go.AddComponent<LevelGeneratorHeader>();
        }

        header.StartCoroutine(Generate());

        //StartCoroutine(Generate());
    }

    public IEnumerator Generate()
    {
        CreatCells();
        if (debug)
        {
            Debug.Log("Cells Generated");
            yield return new WaitForSeconds(1);
        }

        SeparateCells();
        if (debug)
        {
            Debug.Log("Cells Separated");
            yield return new WaitForSeconds(1);
        }

        PickMainRoom();
        if (debug)
        {
            Debug.Log("Main Rooms Picked");
            yield return new WaitForSeconds(1);
        }

        Triangulate();
        if (debug)
        {
            Debug.Log("Triangulated");
            yield return new WaitForSeconds(1);
        }

        SelectPaths();
        if (debug)
        {
            Debug.Log("Paths Selected");
            yield return new WaitForSeconds(1);
        }

        //FindCellLines();
        //if (debug)
        //{
        //    Debug.Log("Find Cell Lines");
        //    yield return new WaitForSeconds(1);
        //}

        AddBossRoom();
        if (debug)
        {
            Debug.Log("Add Boss Room");
            yield return new WaitForSeconds(1);
        }

        FindPathBtwBlocks();
        if (debug)
        {
            Debug.Log("Find Path Between Blocks");
            yield return new WaitForSeconds(1);
        }

        FindPathRoomsBtwMainRooms();
        if (debug)
        {
            Debug.Log("Find Path Room Between Main Rooms");
            yield return new WaitForSeconds(1);
        }

        if (OnLevelGenerationComplete != null)
            OnLevelGenerationComplete();

        yield return null;
    }

    #region Generation Methods

    /// <summary>
    /// 创建房间
    /// </summary>
    void CreatCells()
    {
        RandomFromDistribution.ConfidenceLevel_e confLevel = RandomFromDistribution.ConfidenceLevel_e._80;

        percFromGraphToPaths = levelStats.perFromGraphToPaths;
        mainRoomMeanCutoff = levelStats.mainRoomCutOff;
        int numberOfCells = levelStats.numberOfCells;
        float roomCircleRadius = levelStats.roomCircleRadius;

        float cellMinWidth = levelStats.cellMinWidth;
        float cellMaxWidth = levelStats.cellMaxWidth;
        float cellMinHeight = levelStats.cellMinHeight;
        float cellMaxHeight = levelStats.cellMaxHeight;

        for (int i = 0; i < numberOfCells; i++)
        {
            float minWidthScalar = cellMinWidth;
            float maxWidthScalar = cellMaxWidth;
            float minHeightScalar = cellMinHeight;
            float maxHeightScalar = cellMaxHeight;

            GeneratorCell cell = new GeneratorCell();
            //使房间大小符合随机范围内的正态分布
            cell.width = Mathf.RoundToInt(RandomFromDistribution.RandomRangeNormalDistribution(minWidthScalar, maxWidthScalar, confLevel));
            cell.height = Mathf.RoundToInt(RandomFromDistribution.RandomRangeNormalDistribution(minHeightScalar, maxHeightScalar, confLevel));

            Vector2 pos = GetRandomPointInCircle(roomCircleRadius);
            cell.x = Mathf.RoundToInt(pos.x);
            cell.y = Mathf.RoundToInt(pos.y);
            cell.index = i;
            cells.Add(cell);
            widthAvg += cell.width;
            heightAvg += cell.height;
        }

        widthAvg /= cells.Count;
        heightAvg /= cells.Count;

    }

    /// <summary>
    /// 将重叠部分的房间分散
    /// </summary>
    void SeparateCells()
    {
        bool cellCollision = true;
        while (cellCollision)
        {
            cellCollision = false;

            //利用选择排序进行判断房间是否重叠
            for (int i = 0; i < cells.Count; i++)
            {
                GeneratorCell c = cells[i];
                for (int j = i + 1; j < cells.Count; j++)
                {
                    GeneratorCell cb = cells[j];
                    if (c.CollidesWith(cb))
                    {
                        cellCollision = true;

                        int cbX = Mathf.RoundToInt((c.x + c.width) - cb.x);
                        int cbY = Mathf.RoundToInt((c.y + c.height) - cb.y);

                        int cX = Mathf.RoundToInt((cb.x + cb.width) - c.x);
                        int cY = Mathf.RoundToInt((cb.y + cb.height) - c.y);

                        if (cX < cbX)
                        {
                            if (cX < cY)
                                c.Shift(cX, 0);
                            else
                                c.Shift(0, cY);
                        }
                        else
                        {
                            if (cbX < cbY)
                                cb.Shift(cbX, 0);
                            else
                                cb.Shift(0, cbY);
                        }    
                    }
                }
            }
        }

    }

    /// <summary>
    /// mainRoomMeanCutoff的值越大，保留的房间越多
    /// </summary>
    void PickMainRoom()
    {
        foreach (GeneratorCell c in cells)
        {
            if (c.width * mainRoomMeanCutoff < widthAvg || c.height * mainRoomMeanCutoff < heightAvg)
                c.isMainRoom = false;           
        }
    }

    /// <summary>
    /// 对所有生成的房间看作对应的点并进行三角剖分，得到最小生成树，以便后边求路径
    /// </summary>
    void Triangulate()
    {
        List<Vector2> points = new List<Vector2>();
        List<uint> colors = new List<uint>();

        Vector2 min = Vector2.positiveInfinity; // positiveInfinity: 正无穷
         Vector2 max = Vector2.zero;

        foreach (GeneratorCell c in cells)
        {
            if (c.isMainRoom)
            {
                colors.Add(0);
                points.Add(new Vector2(c.x + (c.width / 2), c.y + (c.height / 2)));
                min.x = Mathf.Min(c.x, min.x);
                min.y = Mathf.Min(c.y, min.y);

                max.x = Mathf.Max(c.x, max.x);
                max.y = Mathf.Max(c.y, max.y);
            }
            
        }

        Voronoi v = new Voronoi(points, colors, new Rect(min.x, min.y, max.x, max.y));
        delaunayLines = v.DelaunayTriangulation();
        spanningTree = v.SpanningTree(KruskalType.MINIMUM);
    }

    /// <summary>
    /// 路径选择
    /// </summary>
    void SelectPaths()
    {
        int countOfPaths = Mathf.RoundToInt(delaunayLines.Count * percFromGraphToPaths);
        int pathsAdded = 0;

        List<LineSegment> linesToAdd = new List<LineSegment>();
        for (int i = 0; i < delaunayLines.Count; i++)
        {
            if (pathsAdded >= countOfPaths)
                break;

            LineSegment line = delaunayLines[i];
            bool lineExist = false;

            for (int j = 0; j < spanningTree.Count; j++)
            {
                LineSegment spLine = spanningTree[j];
                if (spLine.p0.Value.Equals(line.p0.Value) && spLine.p1.Value.Equals(line.p1.Value))
                {
                    lineExist = true;
                    break;
                }
            }

            if (!lineExist)
            {
                linesToAdd.Add(line);
                pathsAdded++;
            }

        }

        spanningTree.AddRange(linesToAdd);
        delaunayLines.Clear();
    }

    /// <summary>
    /// 找到两个房间之间路径的起点和终点，暂时不需要
    /// </summary>
    void FindCellLines()
    {
        foreach (LineSegment l in spanningTree)
        {
            GeneratorCell cellStart = GetCellByPoint(l.p0.Value.x, l.p0.Value.y);
            //if (cellStart != null)
            //{

            //}
            //else
            //    Debug.LogError("Could not find cell start for " + l.p0.Value);

            GeneratorCell cellEnd = GetCellByPoint(l.p1.Value.x, l.p1.Value.y);
            //if (cellEnd != null)
            //{

            //}
            //else
            //    Debug.LogError("Could not find cell end for " + l.p1.Value);
        }
    }

    void AddBossRoom()
    {
        int index = cells.Count;

        GeneratorCell cell = new GeneratorCell();
        cell.width = Mathf.RoundToInt(levelStats.cellMaxWidth);
        cell.height = Mathf.RoundToInt(levelStats.cellMaxWidth);

        int roomMaxX = 0;
        int roomMaxY = 0;
        foreach (GeneratorCell c in cells)
        {
            if (c.isMainRoom)
            {
                roomMaxX = Mathf.Max(c.x + c.width, roomMaxX);
                roomMaxY = Mathf.Max(c.y + c.height, roomMaxY);
            }
        }

        float maxRadius = Mathf.Max(roomMaxX, roomMaxY);

        Vector2 pos = GetRandomPointInRing(maxRadius + cell.width);
        cell.x = Mathf.RoundToInt(pos.x);
        cell.y = Mathf.RoundToInt(pos.y);

        AddBossRoomPath(cell);

        cell.index = index;
        cell.isMainRoom = true;
        cell.isBossRoom = true;
        cells.Add(cell);

    }

    /// <summary>
    /// 给Boss房间添加单向的通路
    /// </summary>
    /// <param name="bossCell"></param>
    void AddBossRoomPath(GeneratorCell bossCell)
    {
        int index = 0;
        float dis = 0f;
        float minDis = float.PositiveInfinity;
        Vector2 bPos = new Vector2(bossCell.x, bossCell.y);
        foreach (GeneratorCell c in cells)
        {
            Vector2 cPos = new Vector2(c.x, c.y);
            dis = (cPos - bPos).magnitude;
            if (dis < minDis)
            {
                minDis = dis;
                index = c.index;
            }
        }

        Path path = new Path();

        Vector2 startPoint = new Vector2(cells[index].x + cells[index].width / 2, 
                                         cells[index].y + cells[index].height / 2);
        Vector2 endPoint = new Vector2(bossCell.x + bossCell.width / 2, 
                                       bossCell.y + bossCell.height / 2);

        BlockPath b1 = new BlockPath();
        b1.start = startPoint;
        b1.end = new Vector2(endPoint.x, startPoint.y);

        BlockPath b2 = new BlockPath();
        b2.start = b1.end;
        b2.end = endPoint;

        path.path.Add(b1);
        path.path.Add(b2);
        paths.Add(path);

    }

    /// <summary>
    /// 将生成好的路径转换成曼哈顿距离的路径
    /// </summary>
    void FindPathBtwBlocks()
    {
        foreach (LineSegment l in spanningTree)
        {
            Path path = new Path();

            Vector2 startPoint = l.p0.Value;
            Vector2 endPoint = l.p1.Value;

            BlockPath b1 = new BlockPath();
            b1.start = startPoint;
            b1.end = new Vector2(endPoint.x, startPoint.y);

            BlockPath b2 = new BlockPath();
            b2.start = b1.end;
            b2.end = endPoint;

            path.path.Add(b1);
            path.path.Add(b2);
            paths.Add(path);
        }

        spanningTree.Clear();

    }

    /// <summary>
    /// 调整地图的整体位置
    /// </summary>
    void FindPathRoomsBtwMainRooms()
    {
        //// 寻找主房间路径之间穿过的房间
        //foreach (Path p in paths)
        //{
        //    foreach (GeneratorCell c in cells)
        //    {
        //        if (!c.isMainRoom && !c.isPathRoom)
        //        {
        //            foreach (BlockPath b in p.path)
        //            {
        //                if (LineRectangleIntersection(b, c))
        //                {
        //                    c.isPathRoom = true;
        //                    break;
        //                }
        //            }
        //        }
        //    }
        //}

        int index = 0;
        while (index < cells.Count)
        {
            GeneratorCell c = cells[index];
            if (c.isMainRoom)
            {
                maxX = Mathf.Max(c.x + c.width, maxX);
                maxY = Mathf.Max(c.y + c.height, maxY);
                minX = Mathf.Min(c.x, minX);
                minY = Mathf.Min(c.y, minY);

                index++;
            }
            else
                cells.Remove(c);
        }

        foreach (GeneratorCell c in cells)
        {
            c.x += Mathf.CeilToInt(Mathf.Abs(minX));
            c.y += Mathf.CeilToInt(Mathf.Abs(minY));
            maxX = Mathf.Max(c.x, c.width, maxX);
            maxY = Mathf.Max(c.y + c.height, maxX);
        }

        foreach (Path p in paths)
        {
            foreach (BlockPath b in p.path)
            {
                b.start.x += Mathf.Abs(minX);
                b.start.y += Mathf.Abs(minY);
                b.end.x += Mathf.Abs(minX);
                b.end.y += Mathf.Abs(minY);
            }
        }
    }
    #endregion

    #region Helper Methods

    /// <summary>
    /// 利用极坐标求圆内随机生成点，越靠圆心分布越密
    /// </summary>
    /// <param name="radius"></param>
    /// <returns></returns>
    Vector2 GetRandomPointInCircle(float radius)
    {
        Vector2 value = Vector2.zero;

        float t = 2 * Mathf.PI * UnityEngine.Random.Range(0, 1f);
        float u = UnityEngine.Random.Range(0, 1f) + UnityEngine.Random.Range(0, 1f);

        float r;

        if (u > 1)
            r = 2 - u;
        else
            r = u;

        value.x = radius * r * Mathf.Cos(t);
        value.y = radius * r * Mathf.Sin(t);

        return value;
    }

    /// <summary>
    /// 在指定范围的圆环内随机生成点
    /// </summary>
    /// <param name="inRadius"></param>
    /// <returns></returns>
    Vector2 GetRandomPointInRing(float inRadius)
    {
        Vector2 value = Vector2.zero;

        Vector2 p = UnityEngine.Random.insideUnitCircle * 1;
        Vector2 pos = p.normalized * (inRadius + p.magnitude);

        value = new Vector2(pos.x, pos.y);

        return value;
    }

    /// <summary>
    /// 通过坐标判断获取目标房间
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    GeneratorCell GetCellByPoint(float x, float y)
    {
        GeneratorCell cell = null;

        foreach (GeneratorCell c in cells)
        {
            if (c.x < x && c.y < y && c.x + c.width > x && c.y + c.height > y)
            {
                cell = c;
                break;
            }
        }

        return cell;
    }

    ///// <summary>
    ///// 判断是否相交
    ///// </summary>
    ///// <param name="a1"></param>
    ///// <param name="a2"></param>
    ///// <param name="b1"></param>
    ///// <param name="b2"></param>
    ///// <param name="intersection"></param>
    ///// <returns></returns>
    //bool LineIntersects(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 intersection)
    //{
    //    intersection = Vector2.zero;

    //    Vector2 b = a2 - a1;
    //    Vector2 d = b2 - b1;

    //    float bDotDPerp = b.x * b.y - b.y * d.x;

    //    if (bDotDPerp == 0)
    //        return false;

    //    Vector2 c = b1 - a1;
    //    float t = (c.x * b.y - c.y * b.x) / bDotDPerp;
    //    if (t < 0 || t > 1)
    //        return false;

    //    float u = (c.x * b.y - c.y * b.x) / bDotDPerp;
    //    if (u < 0 || u > 1)
    //        return false;

    //    intersection = a1 + t * b;
    //    return true;
    //}

    //bool LineRectangleIntersection(BlockPath line, GeneratorCell rect)
    //{
    //    bool value = false;
    //    Vector2 intersection;

    //    BlockPath bottomLine = new BlockPath();
    //    bottomLine.start = new Vector2(rect.x, rect.y);
    //    bottomLine.end = new Vector2(rect.x + rect.width, rect.y);
    //    if (LineIntersects(line.start, line.end, bottomLine.start, bottomLine.end, out intersection))
    //        value = true;

    //    BlockPath topLine = new BlockPath();
    //    topLine.start = new Vector2(rect.x, rect.y + rect.height);
    //    topLine.end = new Vector2(rect.x + rect.width, rect.y + rect.height);
    //    if (LineIntersects(line.start, line.end, topLine.start, topLine.end, out intersection))
    //        value = true;

    //    BlockPath leftLine = new BlockPath();
    //    leftLine.start = new Vector2(rect.x, rect.y);
    //    leftLine.end = new Vector2(rect.x, rect.y + rect.height);
    //    if (LineIntersects(line.start, line.end, leftLine.start, leftLine.end, out intersection))
    //        value = true;

    //    BlockPath rightLine = new BlockPath();
    //    rightLine.start = new Vector2(rect.x + rect.width, rect.y + rect.height);
    //    rightLine.end = new Vector2(rect.x + rect.width, rect.y);
    //    if (LineIntersects(line.start, line.end, rightLine.start, rightLine.end, out intersection))
    //        value = true;

    //    return value;

    //}


    /// <summary>
    /// 获取最大坐标值
    /// </summary>
    /// <returns></returns>
    public Vector2 GetMaxXY()
    {
        return new Vector2(maxX, maxY);
    }

    /// <summary>
    /// 获取最小坐标值
    /// </summary>
    /// <returns></returns>
    public Vector2 GetMinXY()
    {
        return new Vector2(minX, minY);
    }

    #endregion
}

public class Path
{
    public GeneratorCell from;
    public GeneratorCell to;
    public List<BlockPath> path = new List<BlockPath>();
}

public class BlockPath
{
    public Vector2 start;
    public Vector2 end;
}
