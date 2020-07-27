using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NodeReferences : MonoBehaviour
{
    public SpriteRenderer render;
    public GameObject[] items;

    public void CreatItems()
    {
        int rand = Random.Range(0, items.Length);
        GameObject instance = Instantiate(items[rand], transform.position, Quaternion.identity);
        instance.transform.parent = transform;
    }
}
