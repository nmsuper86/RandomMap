using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelGeneratorHeader : MonoBehaviour
{
    public static LevelGeneratorHeader singleton;

    private void Awake()
    {
        singleton = this;
    }
}
