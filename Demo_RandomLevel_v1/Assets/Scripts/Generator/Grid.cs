using Delaunay;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grid : MonoBehaviour
{
    public LevelGenerator levelGenerator;

    public GameObject nodePrefab;
    public Sprite ground;
    public Sprite wall;
    public Sprite bossGround;

    Node[,] grid;
    int maxX;
    int maxY;
    int minX;
    int minY;
    public float scale = 0.32f;

    void Start()
    {
        levelGenerator.OnLevelGenerationComplete = LevelGenerated;
        levelGenerator.GenerateLevel();
    }

    void LevelGenerated()
    {
        InitGrid();
    }

    /// <summary>
    /// 生成地图
    /// </summary>
    void InitGrid()
    {
        Vector2 maxXY = levelGenerator.GetMaxXY();
        maxX = Mathf.CeilToInt(maxXY.x);
        maxY = Mathf.CeilToInt(maxXY.y);

        Vector2 minXY = levelGenerator.GetMinXY();
        minX = Mathf.CeilToInt(Mathf.Abs(minXY.x));
        minY = Mathf.CeilToInt(Mathf.Abs(minXY.y));

        maxX += minX;
        maxY += minY;

        grid = new Node[maxX + 1, maxY + 1];

        #region Render Cells
        foreach (GeneratorCell c in levelGenerator.cells)
        {
            for (int x = c.x; x <= c.x + c.width; x++)
            {
                for (int y = c.y; y <= c.y + c.height; y++)
                {
                    Node n = grid[x, y];
                    if (n == null)
                        n = CreatAt(x, y, false);

                    bool isWall = false;
                    if (x == c.x || x == c.x + c.width || y == c.y || y == c.y + c.height)
                        isWall = true;

                    n.isWall = isWall;

                    if(c.isBossRoom)
                        n.nodeReferences.render.sprite = bossGround;
                    if (isWall)
                        n.nodeReferences.render.sprite = wall;
                    else if(!isWall && !c.isBossRoom)
                        n.nodeReferences.render.sprite = ground;

                    
                }
            }
        }
        #endregion

        #region Render Paths
        foreach (Path p in levelGenerator.paths)
        {
            foreach (BlockPath b in p.path)
            {
                int startX = Mathf.FloorToInt(b.start.x);
                int startY = Mathf.FloorToInt(b.start.y);
                int endX = Mathf.CeilToInt(b.end.x);
                int endY = Mathf.CeilToInt(b.end.y);

                int temp = startX;
                startX = Mathf.Min(startX, endX);
                endX = Mathf.Max(endX, temp);

                temp = startY;
                startY = Mathf.Min(startY, endY);
                endY = Mathf.Max(endY, temp);

                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        Node n = grid[x, y];
                        if (n == null)
                        {
                            n = CreatAt(x, y, false);
                        }
                        else
                        {
                            if (n.isWall)
                            {
                                n.isWall = false;
                                n.nodeReferences.render.sprite = ground;
                            }
                        }

                        AddPathWalls(x, y);

                        if (startY == endY)
                        {
                            int targetY = y + 1;
                            if (y == maxY)
                                targetY = y - 1;

                            Node nn = grid[x, targetY];
                            if (nn == null)
                                CreatAt(x, targetY, false);
                            else 
                            {
                                if (nn.isWall)
                                {
                                    nn.isWall = false;
                                    nn.nodeReferences.render.sprite = ground;
                                }
                            }

                            AddPathWalls(x, targetY);
                        }
                    }
                }
            }
        }
        #endregion

        #region Add Items
        foreach (GeneratorCell c in levelGenerator.cells)
        {
            AddItemsOnRoom(c);
        }

        void AddItemsOnRoom(GeneratorCell c)
        {
            List<Vector2> itemsPos = new List<Vector2>();

            for (int x = c.x; x <= c.x + c.width; x++)
            {
                for (int y = c.y; y <= c.y + c.height; y++)
                {
                    Node n = grid[x, y];

                    bool isGround = false;
                    if (x > c.x + c.width / 4
                        && x < c.x + c.width * 3 / 4
                        && y > c.y + c.height / 4
                        && y < c.y + c.height * 3 / 4)
                    {
                        isGround = true;
                        itemsPos.Add(new Vector2(x, y));
                    }
                }
            }

            int rand = UnityEngine.Random.Range(0, itemsPos.Count);
            int posX = Mathf.CeilToInt(itemsPos[rand].x);
            int posY = Mathf.CeilToInt(itemsPos[rand].y);

            Node nn = grid[posX, posY];

            nn = new Node();
            nn.x = posX;
            nn.y = posY;

            GameObject go = Instantiate(nodePrefab);
            Vector3 targetPos = Vector3.zero;
            NodeReferences nr = go.GetComponent<NodeReferences>();
            nn.nodeReferences = nr;

            targetPos.x = itemsPos[rand].x * scale;
            targetPos.y = itemsPos[rand].y * scale;
            targetPos.z = itemsPos[rand].y;
            nn.worldPos = targetPos;

            go.transform.position = targetPos;
            go.transform.parent = transform;
            grid[posX, posY] = nn;

            nn.nodeReferences.CreatItems();
        }
        #endregion

        Node CreatAt(int x, int y, bool isWall)
        {
            Node n = grid[x, y];
            if (n == null)
            {
                n = new Node();
                n.x = x;
                n.y = y;

                GameObject go = Instantiate(nodePrefab);
                Vector3 targetPos = Vector3.zero;
                NodeReferences nr = go.GetComponent<NodeReferences>();
                n.nodeReferences = nr;

                targetPos.x = x * scale;
                targetPos.y = y * scale;
                targetPos.z = y;
                n.worldPos = targetPos;

                go.transform.position = targetPos;
                go.transform.parent = transform;
                grid[x, y] = n;
                
            }

            n.isWall = isWall;

            if (isWall)
                n.nodeReferences.render.sprite = wall;
            else
                n.nodeReferences.render.sprite = ground;

            return n;
        }

        void AddPathWalls(int x, int y)
        {
            //上
            if (y < maxY)
            {
                Node n = grid[x, y + 1];
                if (n == null)
                    CreatAt(x, y + 1, true);
            }

            //下
            if (y > 0)
            {
                Node n = grid[x, y - 1];
                if (n == null)
                    CreatAt(x, y - 1, true);
            }

            //左
            if (x > 0)
            {
                Node n = grid[x - 1, y];
                if (n == null)
                    CreatAt(x - 1, y, true);
            }
            
            //右
            if (x < maxX)
            {
                Node n = grid[x + 1, y];
                if (n == null)
                    CreatAt(x + 1, y, true);
            }
        }
  
    }
}

public class Node
{
    public int x;
    public int y;
    public bool isWall;
    public Vector3 worldPos;
    public NodeReferences nodeReferences;
}
